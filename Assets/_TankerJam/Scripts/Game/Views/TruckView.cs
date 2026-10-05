// Plays out one truck: bump when blocked, drive the ring road to a bay, VIP-lift arc, park, fill,
// leave. Pure presentation - every decision was already made by GameSession at tap time.
// Motion and spring constants are the web prototype's (see GameTuning).
using DG.Tweening;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    public enum TruckState { Lot, Driving, Lifting, Parked, Filling, Full, Leaving, Gone }

    /// <summary>One-frame notifications from <see cref="TruckView.Tick"/> for the controller (sounds, bays).</summary>
    [System.Flags]
    public enum TruckEvents
    {
        None = 0,
        Parked = 1,          // arrived in the bay (brake)
        BecameFull = 2,      // last unit landed (clunk + ding)
        StartedLeaving = 4,  // horn + engine
        ClearedBay = 8,      // far enough out to free the bay
        Gone = 16,           // left the screen
        BumpEnded = 32,
    }

    public sealed class TruckView
    {
        static readonly int FillId = Shader.PropertyToID("_FillHeight");
        static readonly int TiltId = Shader.PropertyToID("_Tilt");
        static readonly int WobbleId = Shader.PropertyToID("_Wobble");

        public readonly TruckRig Rig;
        public readonly Transform Transform;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly PolylinePath path = new PolylinePath();
        GameTuning tuning;

        public TruckDef Def { get; private set; }
        public int Id => Def.Id;
        public char Color => Def.Color;
        public int Capacity { get; private set; }
        public TruckState State { get; private set; }
        public int Bay { get; private set; } = -1;

        /// <summary>Oil in the tank, in units (0..Capacity). Raised by the pump.</summary>
        public float Fill;
        /// <summary>Units decided by the rules for this truck that the pump hasn't played yet.</summary>
        public int PendingUnits;
        /// <summary>True during frames where the pump is pouring into this truck.</summary>
        public bool Receiving;

        public bool IsBumping => bumpActive;
        public bool IsBusy => State != TruckState.Lot || bumpActive;

        Vector3 home;
        float yaw;
        // Path following.
        float s, v;
        int cursor;
        bool clearedBay;
        // Bump.
        bool bumpActive;
        float bumpT, bumpDist;
        Vector3 bumpDir;
        // VIP lift.
        float liftT, liftYaw0;
        Vector3 liftFrom, liftTo;
        // Full hold.
        float fullT;
        // Springs.
        float slosh, sloshV, pitch, pitchV, wheelAngle, roll;

        readonly Tween punch;

        public TruckView(TruckRig rig)
        {
            Rig = rig;
            Transform = rig.transform;
            // Built once and restarted on demand, so the juice allocates nothing during play.
            // Linked to the truck so it dies with it (scene unload, retry) instead of touching a destroyed object.
            punch = Rig.Body.DOPunchScale(new Vector3(0.08f, 0.14f, 0.08f), 0.35f, 6, 0.6f)
                            .SetAutoKill(false).SetRecyclable(false).SetLink(Rig.gameObject).Pause();
        }

        public void Setup(TruckDef def, BoardLayout layout, MaterialLibrary mats, GameTuning gameTuning)
        {
            Def = def;
            tuning = gameTuning;
            Capacity = def.Capacity;
            State = TruckState.Lot;
            Bay = -1;
            Fill = 0f;
            PendingUnits = 0;
            Receiving = false;
            bumpActive = false;
            clearedBay = false;
            slosh = sloshV = pitch = pitchV = wheelAngle = roll = 0f;

            var h = layout.TruckHome(def);
            home = new Vector3(h.X, 0f, h.Z);
            yaw = YawOf(def.Facing);
            Transform.SetPositionAndRotation(home, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
            punch.Rewind();
            Rig.Body.localRotation = Quaternion.identity;
            Rig.Body.localScale = Vector3.one;

            Rig.BodyRenderer.sharedMaterial = mats.Body(def.Color);
            foreach (var axle in Rig.Axles)
                if (axle.TryGetComponent<MeshRenderer>(out var wheels)) wheels.sharedMaterial = mats.VertexColored;
            Rig.ShellRenderer.sharedMaterial = mats.Shell(def.Color);
            Rig.LiquidRenderer.sharedMaterial = mats.Liquid(def.Color);
            Rig.DecalRenderer.sharedMaterial = mats.Decals;
            Rig.LiquidRenderer.enabled = false;
            Rig.TapCollider.enabled = true;
            Transform.gameObject.SetActive(true);
        }

        /// <summary>Unity yaw (radians) for a facing, from the layout's world directions.</summary>
        public static float YawOf(Facing f)
        {
            var d = BoardLayout.Dir(f);
            return Mathf.Atan2(d.X, d.Z);
        }

        // ---------------- commands ----------------

        /// <summary>Blocked or no bay: lurch forward by the free distance, then bounce back.</summary>
        public void Bump(int freeCells, float extra)
        {
            bumpActive = true;
            bumpT = 0f;
            bumpDist = freeCells + extra;
            var d = BoardLayout.Dir(Def.Facing);
            bumpDir = new Vector3(d.X, 0f, d.Z);
        }

        public void DriveTo(int bay, RouteBuilder routes, float parkZ)
        {
            EndBump();
            Bay = bay;
            var p = Transform.position;
            routes.ToBay(path, new Vec2(p.x, p.z), Def.Facing, bay, parkZ);
            BeginPath(TruckState.Driving);
            Rig.TapCollider.enabled = false;
        }

        public void LiftTo(int bay, Vector3 target)
        {
            EndBump();
            Bay = bay;
            State = TruckState.Lifting;
            liftT = 0f;
            liftFrom = Transform.position;
            liftTo = target;
            liftYaw0 = yaw;
            Rig.TapCollider.enabled = false;
        }

        /// <summary>Hose is down: the pump may start pouring.</summary>
        public void BeginFilling()
        {
            if (State == TruckState.Parked) State = TruckState.Filling;
        }

        /// <summary>Little "I'm full" squash on the body (juice; event-driven, not per frame).</summary>
        public void PunchFull() => punch.Restart();

        public void AddFill(float amount)
        {
            Fill = Mathf.Min(Capacity, Fill + amount);
            sloshV += (Random.value - 0.5f) * amount * 0.4f;
        }

        void BeginPath(TruckState state)
        {
            State = state;
            s = 0f;
            v = 0f;
            cursor = 0;
        }

        void EndBump()
        {
            if (!bumpActive) return;
            bumpActive = false;
            Transform.position = home;
        }

        // ---------------- update ----------------

        public TruckEvents Tick(float dt, float time, RouteBuilder routes)
        {
            var ev = TruckEvents.None;
            if (State == TruckState.Gone) return ev;
            float load = Fill / Capacity;

            sloshV += (-tuning.SloshStiffness * slosh - tuning.SloshDamping * sloshV) * dt;
            slosh += sloshV * dt;
            pitchV += (-tuning.PitchStiffness * pitch - tuning.PitchDamping * pitchV) * dt;
            pitch += pitchV * dt;

            if (bumpActive) ev |= TickBump(dt);

            switch (State)
            {
                case TruckState.Lifting:
                    ev |= TickLift(dt, time);
                    break;
                case TruckState.Driving:
                case TruckState.Leaving:
                    ev |= TickPath(dt, load, routes);
                    break;
                case TruckState.Filling:
                    if (PendingUnits == 0 && Fill >= Capacity - 1e-4f)
                    {
                        Fill = Capacity;
                        State = TruckState.Full;
                        fullT = 0f;
                        ev |= TruckEvents.BecameFull;
                    }
                    break;
                case TruckState.Full:
                    fullT += dt;
                    if (fullT > tuning.FullHoldTime)
                    {
                        var p = Transform.position;
                        routes.Leave(path, new Vec2(p.x, p.z));
                        BeginPath(TruckState.Leaving);
                        clearedBay = false;
                        ev |= TruckEvents.StartedLeaving;
                    }
                    break;
            }

            Rig.Body.localRotation = Quaternion.Euler(-pitch * 0.05f * Mathf.Rad2Deg, 0f, roll * Mathf.Rad2Deg);
            UpdateLiquid(time);
            return ev;
        }

        TruckEvents TickBump(float dt)
        {
            bumpT += dt;
            float u = bumpT / tuning.BumpDuration;
            float off = u < 0.45f
                ? bumpDist * Mathf.Sin(u / 0.45f * Mathf.PI / 2f)
                : bumpDist * (1f - (u - 0.45f) / 0.55f) * Mathf.Cos((u - 0.45f) * 8f);
            Transform.position = home + bumpDir * Mathf.Max(0f, off);
            if (u < 1f) return TruckEvents.None;
            bumpActive = false;
            Transform.position = home;
            pitchV -= 1.5f;
            sloshV += 1f;
            return TruckEvents.BumpEnded;
        }

        TruckEvents TickLift(float dt, float time)
        {
            liftT += dt / tuning.VipDuration;
            float u = Mathf.Min(1f, liftT);
            float e = u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
            var p = Vector3.Lerp(liftFrom, liftTo, e);
            p.y = Mathf.Sin(Mathf.PI * u) * tuning.VipArcHeight;
            float targetYaw = YawOf(Facing.U);
            yaw = liftYaw0 + WrapAngle(targetYaw - liftYaw0) * e;
            roll = Mathf.Sin(time * 6f) * 0.05f * (1f - u);
            Transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
            if (u < 1f) return TruckEvents.None;
            roll = 0f;
            Transform.position = liftTo;
            State = TruckState.Parked;
            sloshV += 1.5f;
            pitchV -= 1f;
            return TruckEvents.Parked;
        }

        TruckEvents TickPath(float dt, float load, RouteBuilder routes)
        {
            bool driving = State == TruckState.Driving;
            float vmax = (driving ? tuning.DriveMaxSpeed : tuning.LeaveMaxSpeed) * (1f - tuning.LoadedSlowdown * load);
            float acc = driving ? tuning.DriveAccel : tuning.LeaveAccel * (1f - tuning.LoadedSlowdown * load);
            float rem = path.Length - s;
            // Acceleration 0 = full speed from the first frame; braking 0 = constant speed up to an exact stop.
            float want = acc > 0f ? Mathf.Min(vmax, v + acc * dt) : vmax;
            if (driving && tuning.BrakeDecel > 0f) want = Mathf.Min(want, Mathf.Sqrt(Mathf.Max(0f, 2f * tuning.BrakeDecel * rem)));
            float a = (want - v) / dt;
            v = want;
            s = Mathf.Min(path.Length, s + v * dt);

            path.Sample(s, ref cursor, out var p, out float heading);
            yaw += WrapAngle(heading - yaw) * Mathf.Min(1f, dt * tuning.HeadingSharpness);
            Transform.SetPositionAndRotation(new Vector3(p.X, 0f, p.Z), Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
            wheelAngle += v * dt / Rig.WheelRadius;
            var wheelRot = Quaternion.Euler(wheelAngle * Mathf.Rad2Deg, 0f, 0f);
            foreach (var axle in Rig.Axles) axle.localRotation = wheelRot;
            sloshV -= a * dt * 0.02f;
            pitchV += a * dt * 0.004f;

            var ev = TruckEvents.None;
            if (!driving)
            {
                if (!clearedBay && s > tuning.ReleaseBayAfter)
                {
                    clearedBay = true;
                    ev |= TruckEvents.ClearedBay;
                }
                if (s >= path.Length - 0.01f)
                {
                    State = TruckState.Gone;
                    Transform.gameObject.SetActive(false);
                    ev |= TruckEvents.Gone;
                }
            }
            else if (path.Length - s < 0.02f)
            {
                State = TruckState.Parked;
                v = 0f;
                yaw = heading; // sit square in the bay
                Transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
                sloshV += 1.4f;
                pitchV -= 0.8f;
                ev |= TruckEvents.Parked;
            }
            return ev;
        }

        void UpdateLiquid(float time)
        {
            bool show = Fill > 0f;
            if (Rig.LiquidRenderer.enabled != show) Rig.LiquidRenderer.enabled = show;
            if (!show) return;
            float r = Rig.TankRadius;
            float level = -r + 0.03f + (2f * r - 0.06f) * (Fill / Capacity);
            float tilt = Mathf.Clamp(slosh * 0.2f, -0.35f, 0.35f);
            block.SetFloat(FillId, level);
            block.SetFloat(TiltId, Mathf.Tan(tilt));
            block.SetFloat(WobbleId, Receiving ? 0.012f : 0f);
            Rig.LiquidRenderer.SetPropertyBlock(block);
        }

        /// <summary>World height of the liquid surface (for the hose stream).</summary>
        public float LiquidSurfaceY
        {
            get
            {
                float r = Rig.TankRadius;
                return Transform.position.y + Rig.TankCenterY - r + 0.03f + (2f * r - 0.06f) * (Fill / Capacity);
            }
        }

        public float HatchY => Transform.position.y + Rig.HoseTargetY;

        public void Hide()
        {
            State = TruckState.Gone;
            bumpActive = false;
            Transform.gameObject.SetActive(false);
        }

        static float WrapAngle(float a)
        {
            while (a > Mathf.PI) a -= 2f * Mathf.PI;
            while (a < -Mathf.PI) a += 2f * Mathf.PI;
            return a;
        }
    }
}
