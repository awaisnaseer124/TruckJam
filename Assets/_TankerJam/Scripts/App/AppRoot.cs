// Composition root and app flow. The only place that knows the game, the meta layer and the UI together:
// home -> level -> win (coins, stars, next) / jammed (offer boosters, then retry); booster buying and
// spending; booster introductions (free use + tutorial hint); first-level tutorial hand.
using System.Collections.Generic;
using TankerJam.Core;
using TankerJam.Game;
using TankerJam.Meta;
using TankerJam.UI;
using UnityEngine;

namespace TankerJam.App
{
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] GameController game;
        [SerializeField] LevelCatalog catalog;
        [SerializeField] EconomyConfig economy;
        [SerializeField] Hud hud;
        [SerializeField] ResultPopup popup;
        [SerializeField] HomeOverlay home;
        [SerializeField] TutorialHand tutorial;

        PlayerProgress progress;
        int position;              // 0-based catalog position being played
        LevelCatalog.Entry entry;
        int boostersUsed;
        bool firstTapTutorial;
        string pendingIntro;       // "vip" / "extra" hint to show once the level is on screen
        LevelDef testLevel;        // a level from the level editor / dev menu: replayable, never touches progress
        readonly List<ResultPopup.Option> options = new List<ResultPopup.Option>(4);

        public PlayerProgress Progress => progress;
        public HomeOverlay Home => home;
        public ResultPopup Popup => popup;
        public TutorialHand Tutorial => tutorial;

        /// <summary>Test seam: when set before the scene loads, used instead of PlayerPrefs.</summary>
        public static IPersistence PersistenceOverride;
        public int Position => position;

#if UNITY_EDITOR
        public void EditorWire(GameController g, LevelCatalog c, EconomyConfig e, Hud h, ResultPopup p, HomeOverlay ho, TutorialHand t)
        {
            game = g; catalog = c; economy = e; hud = h; popup = p; home = ho; tutorial = t;
        }
#endif

        void Awake()
        {
            progress = new PlayerProgress(PersistenceOverride ?? new PlayerPrefsPersistence(), economy);
            Application.targetFrameRate = 60;
        }

        void OnEnable()
        {
            game.StatusChanged += OnStatus;
            game.BoostersChanged += RefreshBoosters;
            game.BoosterUsed += OnBoosterUsed;
            game.LevelEnded += OnLevelEnded;
            game.Cue += OnCue;
            hud.RetryClicked += Retry;
            hud.SoundClicked += ToggleSound;
            hud.VipClicked += OnVipClicked;
            hud.ExtraClicked += OnExtraClicked;
            home.PlayClicked += OnPlay;
            progress.Changed += OnProgressChanged;
        }

        void OnDisable()
        {
            game.StatusChanged -= OnStatus;
            game.BoostersChanged -= RefreshBoosters;
            game.BoosterUsed -= OnBoosterUsed;
            game.LevelEnded -= OnLevelEnded;
            game.Cue -= OnCue;
            hud.RetryClicked -= Retry;
            hud.SoundClicked -= ToggleSound;
            hud.VipClicked -= OnVipClicked;
            hud.ExtraClicked -= OnExtraClicked;
            home.PlayClicked -= OnPlay;
            progress.Changed -= OnProgressChanged;
        }

        /// <summary>Where the level editor leaves a level for the next Play (editor only; consumed on start).</summary>
        public const string EditorPlayLevelPath = "Temp/TankerJamPlayLevel.json";

        void Start()
        {
            hud.SetSound(!AudioManager.Muted);
            OnProgressChanged();
#if UNITY_EDITOR
            if (System.IO.File.Exists(EditorPlayLevelPath))
            {
                string json = System.IO.File.ReadAllText(EditorPlayLevelPath);
                System.IO.File.Delete(EditorPlayLevelPath);
                DevPlayLevel(LevelJson.Parse(json));
                return;
            }
#endif
            // Show the next level behind the home screen.
            StartLevel(progress.Level);
            game.InputEnabled = false;
            home.Show(progress.Level + 1, progress.Coins);
        }

        // ---------------- flow ----------------

        void OnPlay()
        {
            home.Hide();
            game.InputEnabled = true;
            ShowLevelTutorials();
        }

        public void StartLevel(int levelPosition)
        {
            testLevel = null;
            position = levelPosition;
            entry = catalog.At(position);
            var level = LevelJson.Parse(entry.Json.text);
            boostersUsed = 0;
            popup.Hide();
            tutorial.Hide();

            pendingIntro = null;
            if (entry.Introduces == "vip" && progress.Introduce(BoosterKind.Vip)) pendingIntro = "vip";
            if (entry.Introduces == "extra" && progress.Introduce(BoosterKind.Extra)) pendingIntro = "extra";

            game.Load(level, progress.Boosters(BoosterKind.Vip), progress.Boosters(BoosterKind.Extra));
            game.InputEnabled = true;
            hud.SetLevel(position + 1, entry.Hard);
            firstTapTutorial = position == 0 && progress.StarsFor(0) == 0;
            RefreshBoosters();
            if (!home.IsOpen) ShowLevelTutorials();
        }

        void ShowLevelTutorials()
        {
            if (pendingIntro != null || firstTapTutorial) hud.SetStatus("");
            if (pendingIntro == "vip")
                tutorial.PointAtUi(hud.Vip.Rect, "New booster! VIP lift moves any truck straight to the gold VIP bay, even a blocked one.");
            else if (pendingIntro == "extra")
                tutorial.PointAtUi(hud.Extra.Rect, "New booster! Extra bay opens one more bay for this level.");
            else if (firstTapTutorial && game.Level.Solution.Count > 0)
            {
                var truck = game.Truck(game.Level.Solution[0]);
                tutorial.PointAtWorld(game.GameCamera, truck.Transform.position + Vector3.up * 1.1f,
                                      "Tap a truck to drive it out the way its arrow points.");
            }
        }

        void Retry()
        {
            if (home.IsOpen) return;
            if (testLevel != null) DevPlayLevel(testLevel);
            else StartLevel(position);
        }

        void OnLevelEnded(EndState state)
        {
            tutorial.Hide();
            game.InputEnabled = false;
            if (state == EndState.Won && testLevel != null)
            {
                popup.ShowWin(PlayerProgress.StarsForBoosters(boostersUsed), 0, false, () => DevPlayLevel(testLevel));
                return;
            }
            if (state == EndState.Won)
            {
                int stars = PlayerProgress.StarsForBoosters(boostersUsed);
                int coins = progress.CompleteLevel(position, entry.Hard, boostersUsed);
                popup.ShowWin(stars, coins, entry.Hard, () => StartLevel(progress.Level));
                return;
            }

            // Jammed: offer what can actually help, then Retry.
            options.Clear();
            var session = game.Session;
            if (state == EndState.JammedBaysFull && session.CanOpenExtraBay)
                options.Add(new ResultPopup.Option(BoosterLabel(BoosterKind.Extra, "Open extra bay"), PopupButtonStyle.Primary,
                                                   () => { game.InputEnabled = true; OnExtraClicked(); }));
            if (session.Bays[session.VipBay].TruckId < 0 && session.Rules.TrucksInLot > 0)
                options.Add(new ResultPopup.Option(BoosterLabel(BoosterKind.Vip, "Use VIP lift"), PopupButtonStyle.Gold,
                                                   () => { game.InputEnabled = true; OnVipClicked(); }));
            options.Add(new ResultPopup.Option("Try again", options.Count > 0 ? PopupButtonStyle.Secondary : PopupButtonStyle.Primary, Retry));
            string reason = state == EndState.JammedBaysFull
                ? "Every bay holds a truck whose color isn't at the bottom of any vessel."
                : "No truck in the lot has a clear road out.";
            popup.ShowJam(reason, options);
        }

        string BoosterLabel(BoosterKind kind, string action)
        {
            int owned = kind == BoosterKind.Vip ? game.Session.VipLeft : game.Session.ExtraLeft;
            return owned > 0 ? $"{action} ({owned})" : $"{action} - {economy.Price(kind)} coins";
        }

        // ---------------- boosters ----------------

        void OnVipClicked()
        {
            if (home.IsOpen || game.Session == null) return;
            if (pendingIntro == "vip") { pendingIntro = null; tutorial.Hide(); }
            var s = game.Session;
            if (!s.VipArmed && s.VipLeft == 0 && !TryBuy(BoosterKind.Vip)) return;
            game.ToggleVip();
        }

        void OnExtraClicked()
        {
            if (home.IsOpen || game.Session == null) return;
            if (pendingIntro == "extra") { pendingIntro = null; tutorial.Hide(); }
            var s = game.Session;
            if (s.Bays[s.ExtraBay].Open) { game.OpenExtraBay(); return; } // shows "already open"
            if (s.ExtraLeft == 0 && !TryBuy(BoosterKind.Extra)) return;
            game.OpenExtraBay();
        }

        /// <summary>Spends coins for one booster and hands it to the running level.</summary>
        bool TryBuy(BoosterKind kind)
        {
            if (!progress.BuyBooster(kind))
            {
                hud.SetStatus($"Not enough coins. {economy.Price(kind)} needed, you have {progress.Coins}.");
                return false;
            }
            if (kind == BoosterKind.Vip) game.AddBoosters(1, 0);
            else game.AddBoosters(0, 1);
            return true;
        }

        void OnBoosterUsed(Booster booster)
        {
            boostersUsed++;
            progress.UseBooster(booster == Booster.Vip ? BoosterKind.Vip : BoosterKind.Extra);
            game.InputEnabled = true;
        }

        void RefreshBoosters()
        {
            var s = game.Session;
            if (s == null) return;
            hud.Vip.Show(s.VipLeft, economy.VipPrice, s.CanArmVip || s.VipArmed || (s.VipLeft == 0 && s.Rules.TrucksInLot > 0), s.VipArmed);
            hud.Extra.Show(s.ExtraLeft, economy.ExtraPrice, !s.Bays[s.ExtraBay].Open, false);
        }

        // ---------------- misc ----------------

        /// <summary>Game hints go to the status pill, except while a tutorial bubble is explaining things.</summary>
        void OnStatus(string message)
        {
            if (tutorial.IsShowing && !string.IsNullOrEmpty(message)) return;
            hud.SetStatus(message);
        }

        void OnCue(GameCue cue, float arg)
        {
            if (firstTapTutorial && cue == GameCue.Depart)
            {
                firstTapTutorial = false;
                tutorial.Hide();
            }
        }

        int shownCoins = -1;

        void OnProgressChanged()
        {
            if (progress.Coins == shownCoins) return;
            hud.SetCoins(progress.Coins, punch: shownCoins >= 0);
            shownCoins = progress.Coins;
        }

        void ToggleSound()
        {
            AudioManager.Muted = !AudioManager.Muted;
            hud.SetSound(!AudioManager.Muted);
        }

        // ---------------- dev ----------------

        /// <summary>Development helper: same as pressing Play on the home screen.</summary>
        public void DevPlay()
        {
            if (home.IsOpen) OnPlay();
        }

        /// <summary>Development helper: jump to a level (1-based).</summary>
        public void DevJumpTo(int levelNumber)
        {
            progress.SetLevel(levelNumber - 1);
            home.Hide();
            StartLevel(progress.Level);
        }

        /// <summary>Development helper: preview the end popups without finishing the level (no rewards).</summary>
        public void DevPreviewPopup(bool win)
        {
            home.Hide();
            if (win) popup.ShowWin(3, progress.RewardFor(entry.Hard), entry.Hard, () => StartLevel(position));
            else
            {
                options.Clear();
                options.Add(new ResultPopup.Option(BoosterLabel(BoosterKind.Extra, "Open extra bay"), PopupButtonStyle.Primary, () => { }));
                options.Add(new ResultPopup.Option(BoosterLabel(BoosterKind.Vip, "Use VIP lift"), PopupButtonStyle.Gold, () => { }));
                options.Add(new ResultPopup.Option("Try again", PopupButtonStyle.Secondary, Retry));
                popup.ShowJam("Every bay holds a truck whose color isn't at the bottom of any vessel.", options);
            }
        }

        /// <summary>Development helper: play a level that isn't in the catalog (e.g. a free-form sample).</summary>
        public void DevPlayLevel(LevelDef level)
        {
            testLevel = level;
            boostersUsed = 0;
            pendingIntro = null;
            firstTapTutorial = false;
            home.Hide();
            popup.Hide();
            tutorial.Hide();
            game.Load(level, progress.Boosters(BoosterKind.Vip), progress.Boosters(BoosterKind.Extra));
            game.InputEnabled = true;
            hud.SetTitle("TEST LEVEL");
            RefreshBoosters();
        }

        public void DevResetProgress()
        {
            progress.Reset();
            StartLevel(progress.Level);
        }
    }
}
