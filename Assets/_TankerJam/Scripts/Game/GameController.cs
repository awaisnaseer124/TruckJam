// Owns one level at a time: builds the board from the level data, turns taps into GameSession commands,
// and runs the single update loop that plays out the decided results (pump -> vessels -> trucks -> hoses),
// then checks for win/jam once everything has been calm for a moment.
using System;
using System.Collections.Generic;
using System.Text;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] Camera gameCamera;
        [SerializeField] CameraFitter cameraFitter;
        [SerializeField] LightingRig lighting;

        [Header("Standalone start (until the meta layer drives level loading)")]
        [SerializeField] TextAsset startLevel;
        [SerializeField] int startVipBoosters = 1;
        [SerializeField] int startExtraBoosters = 1;

        /// <summary>Short hint for the status pill ("Blocked", "All bays busy", "Ready to flow: ...").</summary>
        public event Action<string> StatusChanged;
        /// <summary>Raised once when the level is won or jammed (after the calm delay).</summary>
        public event Action<EndState> LevelEnded;
        /// <summary>Booster counts, VIP armed state or bay state changed.</summary>
        public event Action BoostersChanged;

        public GameSession Session { get; private set; }
        public LevelDef Level { get; private set; }
        public EndState EndState { get; private set; }
        public string Status { get; private set; } = "";
        public GameConfig Config => config;

        BoardLayout layout;
        RouteBuilder routes;
        MaterialLibrary mats;
        SceneryBuilder scenery;
        TapInput input;
        Transform boardRoot, truckRoot;
        PumpView pump;
        BayRowView bayRow;
        readonly List<VesselView> vessels = new List<VesselView>();
        readonly List<HoseView> hoses = new List<HoseView>();
        readonly List<TruckView>[] truckPool = { new List<TruckView>(), new List<TruckView>(), new List<TruckView>() };
        readonly List<TruckView> trucks = new List<TruckView>(); // indexed by truck id
        readonly List<char> bottomColors = new List<char>(8);
        readonly StringBuilder sb = new StringBuilder(64);

        int levelVip, levelExtra;
        int pumpCursor;
        bool unitActive;
        float unitProgress;
        float time, calm;

        // ---------------- lifecycle ----------------

#if UNITY_EDITOR
        /// <summary>Scene setup tooling: wires references from code.</summary>
        public void EditorWire(GameConfig gameConfig, Camera cam, CameraFitter fitter, LightingRig lightingRig, TextAsset level)
        {
            config = gameConfig;
            gameCamera = cam;
            cameraFitter = fitter;
            lighting = lightingRig;
            startLevel = level;
        }
#endif

        void Awake()
        {
            if (config == null)
            {
                Debug.LogError("GameController has no GameConfig assigned.", this);
                enabled = false;
                return;
            }
            mats = new MaterialLibrary(config);
            scenery = new SceneryBuilder();
            input = new TapInput(config.TruckLayer);
            boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(transform, false);
            truckRoot = new GameObject("Trucks").transform;
            truckRoot.SetParent(transform, false);
            pump = new PumpView(boardRoot, config.Palette, mats);
            bayRow = new BayRowView(boardRoot, mats);
            if (gameCamera != null) gameCamera.backgroundColor = config.Palette.Sky;
            if (lighting != null) lighting.Apply(config.Palette);
        }

        void Start()
        {
            if (startLevel != null && Level == null)
                Load(LevelJson.Parse(startLevel.text), startVipBoosters, startExtraBoosters);
        }

        void OnDestroy() => mats?.Dispose();

        public void Load(LevelDef level, int vipBoosters, int extraBoosters)
        {
            var errors = new List<string>();
            if (!LevelJson.Validate(level, errors))
                Debug.LogError("Level is invalid:\n" + string.Join("\n", errors));

            Level = level;
            levelVip = vipBoosters;
            levelExtra = extraBoosters;
            layout = new BoardLayout(level, config.Layout.Board);
            routes = new RouteBuilder(layout);
            Session = new GameSession(level, vipBoosters, extraBoosters);
            EndState = EndState.Playing;
            pumpCursor = 0;
            unitActive = false;
            time = calm = 0f;

            scenery.Build(boardRoot, level, layout, config.Palette, mats);
            BuildVessels();
            BuildHoses();
            bayRow.Rebuild(Session.Bays, layout, config.Palette);
            pump.Place(new Vector3(layout.Pump.X, 0f, layout.Pump.Z));
            SpawnTrucks();
            if (cameraFitter != null) cameraFitter.Fit(layout, config.Layout);

            SetStatus("Tap a truck to drive it out. Oil drains from the bottom of the vessels into matching trucks.");
            BoostersChanged?.Invoke();
            DevLog($"Tanker Jam: level loaded ({level.Trucks.Count} trucks, {level.Vessels.Count} vessels, {level.Slots} bays)");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        static void DevLog(string message) => Debug.Log(message);

        public void Retry()
        {
            if (Level != null) Load(Level, levelVip, levelExtra);
        }

        void BuildVessels()
        {
            var p = layout.P;
            while (vessels.Count < Level.Vessels.Count) vessels.Add(new VesselView(boardRoot, $"Vessel{vessels.Count}"));
            for (int i = 0; i < vessels.Count; i++)
            {
                if (i >= Level.Vessels.Count) { vessels[i].Clear(); continue; }
                vessels[i].Setup(new Vector3(layout.VesselX(i), 0f, p.VesselZ), Level.Vessels[i],
                                 p.VesselRadius, p.VesselUnitHeight, p.VesselBaseY, mats);
            }
        }

        void BuildHoses()
        {
            while (hoses.Count < layout.BayCount) hoses.Add(new HoseView(boardRoot, hoses.Count, config.Palette, mats));
            for (int i = 0; i < hoses.Count; i++)
            {
                if (i < layout.BayCount) hoses[i].Place(layout.BayX(i), layout.P.HoseZ, layout.P.HeaderY);
                else hoses[i].Place(-100f, -100f, 0f); // park unused hoses off-screen
            }
        }

        void SpawnTrucks()
        {
            foreach (var t in trucks) t.Hide();
            for (int i = 0; i < truckPool.Length; i++)
                foreach (var t in truckPool[i]) t.Hide();
            trucks.Clear();
            var used = new int[3];
            foreach (var def in Level.Trucks)
            {
                int k = Mathf.Clamp(def.Len - 2, 0, 2);
                var pool = truckPool[k];
                if (used[k] >= pool.Count)
                {
                    var rig = Instantiate(config.TruckPrefab(def.Len), truckRoot);
                    pool.Add(new TruckView(rig));
                }
                var view = pool[used[k]++];
                view.Setup(def, layout, mats, config.Tuning);
                trucks.Add(view);
            }
        }

        // ---------------- commands (UI / input) ----------------

        public void TapTruck(int truckId)
        {
            if (Session == null || EndState != EndState.Playing) return;
            var t = trucks[truckId];
            if (t.IsBusy) return;

            var r = Session.Tap(truckId);
            DevLog($"Tanker Jam: tap truck {truckId} -> {r.Outcome} bay {r.Bay} units {r.UnitCount}");
            var tuning = config.Tuning;
            switch (r.Outcome)
            {
                case TapOutcome.Blocked:
                    t.Bump(r.FreeCells, tuning.BumpExtra);
                    SetStatus("Blocked. Something is in its way.");
                    break;
                case TapOutcome.NoFreeBay:
                    t.Bump(0, tuning.NoBayBump);
                    SetStatus("All bays are busy. Wait for a truck to fill up, or use a booster.");
                    break;
                case TapOutcome.Assigned:
                    RegisterUnits(r);
                    t.DriveTo(r.Bay, routes, layout.ParkZ(t.Rig.TankCenterZ));
                    SetStatus("");
                    break;
                case TapOutcome.VipLifted:
                    RegisterUnits(r);
                    t.LiftTo(r.Bay, new Vector3(layout.BayX(r.Bay), 0f, layout.ParkZ(t.Rig.TankCenterZ)));
                    bayRow.Sync(Session.Bays, layout, config.Palette);
                    SetStatus("VIP lift! The truck goes straight to the VIP bay.");
                    BoostersChanged?.Invoke();
                    break;
            }
        }

        public bool ToggleVip()
        {
            if (Session == null) return false;
            if (!Session.ToggleVip())
            {
                SetStatus(Session.VipLeft == 0 ? "No VIP lifts left on this level." : "The VIP bay is busy.");
                return false;
            }
            if (Session.VipArmed)
            {
                Resume();
                SetStatus("Tap any truck in the lot, even a blocked one.");
            }
            else SetStatus("");
            BoostersChanged?.Invoke();
            return true;
        }

        public bool OpenExtraBay()
        {
            if (Session == null || !Session.OpenExtraBay())
            {
                SetStatus("The extra bay is already open.");
                return false;
            }
            bayRow.Sync(Session.Bays, layout, config.Palette);
            Resume();
            SetStatus("Extra bay open.");
            BoostersChanged?.Invoke();
            return true;
        }

        /// <summary>Continue after a Jammed result once a booster changed the situation.</summary>
        void Resume()
        {
            if (EndState == EndState.JammedBaysFull || EndState == EndState.JammedNoExit) EndState = EndState.Playing;
            calm = 0f;
        }

        void RegisterUnits(TapResult r)
        {
            for (int i = r.FirstUnit; i < r.FirstUnit + r.UnitCount; i++)
                trucks[Session.Units[i].TruckId].PendingUnits++;
        }

        // ---------------- loop ----------------

        void Update()
        {
            if (Session == null) return;
            if (EndState == EndState.Playing && gameCamera != null && input.TryGetTappedTruck(gameCamera, out var rig))
            {
                int id = IndexOf(rig);
                if (id >= 0) TapTruck(id);
            }

            Advance(Time.deltaTime);
        }

        /// <summary>
        /// Advances the simulation by one frame of <paramref name="frameDelta"/> seconds (clamped and sub-stepped
        /// like the prototype). Called by Update; tests call it directly for deterministic, frame-rate-free runs.
        /// </summary>
        public void Advance(float frameDelta)
        {
            if (Session == null) return;
            var tuning = config.Tuning;
            float dt = Mathf.Min(frameDelta, tuning.MaxFrameDelta);
            if (dt <= 0f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / tuning.MaxStep));
            float sub = dt / steps;
            for (int i = 0; i < steps; i++) Step(sub);
            CheckEnd(dt);
        }

        int IndexOf(TruckRig rig)
        {
            for (int i = 0; i < trucks.Count; i++) if (trucks[i].Rig == rig) return i;
            return -1;
        }

        void Step(float dt)
        {
            time += dt;
            TickPump(dt);
            for (int i = 0; i < Level.Vessels.Count; i++) vessels[i].Tick(dt, time);

            for (int i = 0; i < trucks.Count; i++)
            {
                var t = trucks[i];
                var ev = t.Tick(dt, time, routes);
                if ((ev & TruckEvents.ClearedBay) != 0)
                {
                    Session.ReleaseBay(t.Bay);
                    bayRow.Sync(Session.Bays, layout, config.Palette);
                    BoostersChanged?.Invoke();
                }
            }

            TickHoses(dt);
            pump.Tick(dt, time, unitActive);
        }

        void TickPump(float dt)
        {
            var units = Session.Units;
            if (!unitActive && pumpCursor < units.Count && trucks[units[pumpCursor].TruckId].State == TruckState.Filling)
            {
                unitActive = true;
                unitProgress = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var check = units[pumpCursor];
                if (vessels[check.Vessel].BottomColor != check.Color)
                    Debug.LogError($"Vessel {check.Vessel} bottom is {vessels[check.Vessel].BottomColor} but unit {check} expects {check.Color}.");
#endif
            }
            if (!unitActive) return;

            var u = units[pumpCursor];
            var t = trucks[u.TruckId];
            var vessel = vessels[u.Vessel];
            float available = vessel.BottomAmount > 0f ? vessel.BottomAmount : 1f;
            float amt = Mathf.Min(dt / config.Tuning.UnitDuration, Mathf.Min(1f - unitProgress, available));
            unitProgress += amt;
            t.AddFill(amt);
            t.Receiving = true;
            vessel.DrainBottom(amt);

            if (unitProgress >= 1f - 1e-4f)
            {
                pumpCursor++;
                t.PendingUnits--;
                t.Fill = Mathf.Round(t.Fill);
                t.Receiving = false;
                vessel.TrimBottom();
                unitActive = false;
            }
        }

        void TickHoses(float dt)
        {
            var tuning = config.Tuning;
            var bays = Session.Bays;
            TruckView pouring = unitActive ? trucks[Session.Units[pumpCursor].TruckId] : null;
            for (int i = 0; i < bays.Count; i++)
            {
                var hose = hoses[i];
                TruckView t = bays[i].TruckId >= 0 ? trucks[bays[i].TruckId] : null;
                bool present = t != null && (t.State == TruckState.Parked || t.State == TruckState.Filling || t.State == TruckState.Full);
                if (!present)
                {
                    hose.Extension = Mathf.Max(0f, hose.Extension - dt * tuning.HoseRaiseSpeed);
                    hose.Apply(-1f);
                    hose.ShowStream(false, null, 0f, time);
                    continue;
                }
                if (t.State == TruckState.Parked)
                {
                    hose.Extension = Mathf.Min(1f, hose.Extension + dt * tuning.HoseLowerSpeed);
                    if (hose.Extension >= 1f) t.BeginFilling();
                }
                else if (t.State == TruckState.Full)
                    hose.Extension = Mathf.Max(0f, hose.Extension - dt * tuning.HoseRaiseSpeed);
                hose.Apply(t.HatchY);
                hose.ShowStream(pouring == t, mats.OilSurface(t.Color), t.LiquidSurfaceY, time);
            }
        }

        bool AnythingMoving()
        {
            if (unitActive) return true;
            var units = Session.Units;
            if (pumpCursor < units.Count)
            {
                var next = trucks[units[pumpCursor].TruckId].State;
                if (next != TruckState.Filling && next != TruckState.Gone) return true;
            }
            foreach (var t in trucks)
            {
                if (t.IsBumping) return true;
                switch (t.State)
                {
                    case TruckState.Driving:
                    case TruckState.Lifting:
                    case TruckState.Parked:
                    case TruckState.Full:
                    case TruckState.Leaving:
                        return true;
                }
            }
            return false;
        }

        void CheckEnd(float dt)
        {
            if (EndState != EndState.Playing) return;
            if (AnythingMoving()) { calm = 0f; return; }
            calm += dt;
            if (calm < config.Tuning.CalmTime) return;

            var state = Session.Evaluate();
            if (state == EndState.Playing)
            {
                Session.Rules.BottomColors(bottomColors);
                if (bottomColors.Count > 0) SetStatus("Ready to flow: " + BottomColorNames() + ".");
                return;
            }
            EndState = state;
            switch (state)
            {
                case EndState.Won:
                    SetStatus("Level clear! Every truck filled up and drove off.");
                    break;
                case EndState.JammedBaysFull:
                    Session.Rules.BottomColors(bottomColors);
                    SetStatus("Jammed: every bay holds a truck whose color isn't at the bottom of any vessel. Bottom colors now: " + BottomColorNames() + ".");
                    break;
                case EndState.JammedNoExit:
                    SetStatus("Jammed: no truck in the lot has a clear road out.");
                    break;
            }
            LevelEnded?.Invoke(state);
        }

        string BottomColorNames()
        {
            sb.Clear();
            for (int i = 0; i < bottomColors.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(config.Palette.NameOf(bottomColors[i]));
            }
            return sb.ToString();
        }

        void SetStatus(string message)
        {
            if (message == Status) return;
            Status = message;
            StatusChanged?.Invoke(message);
        }

        // ---------------- queries (UI, debug, tests) ----------------

        public bool IsBusy => Session != null && AnythingMoving();
        public TruckView Truck(int id) => trucks[id];
        public int TruckCount => trucks.Count;
    }
}
