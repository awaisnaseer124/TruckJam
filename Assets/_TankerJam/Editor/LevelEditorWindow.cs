// Menu: Tanker Jam > Level Editor (or double-click a level .json under Data/).
// Visual editor for free-form levels on the square lot. All editing logic lives in Core (LevelDraft,
// ShapeTracer, LevelSolver) and is unit-tested; this window only draws the draft and turns mouse/keys into
// draft operations.
//
// Canvas: top = the bays (trucks pointing up drive toward them). Left click selects (shift adds, drag on
// empty space box-selects), drag moves, wheel zooms, middle/alt-drag pans, right click opens a menu.
// Keys (canvas focused): Q/E rotate, F flip, 2/3/4 length, C next color, arrows nudge, Del delete,
// Ctrl+D duplicate, Ctrl+A select all, Ctrl+Z / Ctrl+Y undo/redo, P place tool, S select tool.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TankerJam.App;
using TankerJam.Core;
using TankerJam.Game;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public sealed class LevelEditorWindow : EditorWindow
    {
        public const string DraftFolder = "Assets/_TankerJam/Data/Drafts";
        const string GameScenePath = "Assets/_TankerJam/Scenes/Game.unity";
        const float PanelWidth = 320f, ToolbarHeight = 22f, StatusHeight = 20f;

        enum Tool { Select, Place, Cone }

        // ---- persisted across domain reloads ----
        [SerializeField] string draftJson;
        [SerializeField] string path;
        [SerializeField] bool dirty;
        [SerializeField] Tool tool = Tool.Select;
        [SerializeField] int brushLen = 2;
        [SerializeField] char brushColor = 'P';
        [SerializeField] float brushAngle;
        [SerializeField] bool fineAngles;               // 15-degree steps instead of 45
        [SerializeField] int snapIndex = 1;             // position snap: 0.5 / 0.25 / 0.05
        [SerializeField] int symmetryK = 8;
        [SerializeField] TraceShape traceShape = TraceShape.Ring;
        [SerializeField] TraceFacing traceFacing = TraceFacing.Outward;
        [SerializeField] float traceSize = 4f, traceSpacing = 0.9f;
        [SerializeField] string traceColors = "PYCG";
        [SerializeField] int colorCount = 4;
        [SerializeField] int vesselCount = 4;
        [SerializeField] float targetWinRate = 0.3f;
        [SerializeField] bool useTargetRate;
        [SerializeField] int seed = 1;
        [SerializeField] int genLevel = 20;
        [SerializeField] int genFamily;                 // 0 = any, else PatternFamily + 1
        [SerializeField] float zoom = 1f;
        [SerializeField] Vector2 pan;
        [SerializeField] Vector2 panelScroll;

        static readonly float[] SnapSteps = { 0.5f, 0.25f, 0.05f };
        static readonly string[] SnapLabels = { "0.5", "0.25", "free" };
        static readonly int[] SymmetryOptions = { 2, 3, 4, 5, 6, 8, 10, 12 };

        LevelDraft draft;
        DraftReport report;
        LevelScore lastScore;
        string message;
        MessageType messageType;
        readonly List<string> undo = new List<string>();
        readonly List<string> redo = new List<string>();
        readonly HashSet<DraftTruck> selection = new HashSet<DraftTruck>();
        Palette palette;

        // canvas interaction
        Rect canvas;
        int canvasId;
        bool dragging, dragRecorded, boxSelecting, panning;
        Vector2 boxStart, mouse;
        Vec2 dragStartBoard;
        readonly Dictionary<DraftTruck, Vec2> dragOrigins = new Dictionary<DraftTruck, Vec2>();
        DraftTruck hovered;

        float AngleStep => fineAngles ? 15f : 45f;
        float SnapStep => SnapSteps[Mathf.Clamp(snapIndex, 0, SnapSteps.Length - 1)];

        // ---------------- open ----------------

        [MenuItem("Tanker Jam/Level Editor %#l")]
        public static LevelEditorWindow Open()
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.minSize = new Vector2(820, 560);
            return w;
        }

        /// <summary>Double-clicking a level JSON under Data/ opens it here.</summary>
        [OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            // The double-clicked asset is the current selection (avoids the instance-id API that Unity 6.6 deprecates).
            string p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!p.StartsWith("Assets/_TankerJam/Data/") || !p.EndsWith(".json")) return false;
            Open().Load(p);
            return true;
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            palette = LoadPalette();
            draft = null;
            if (!string.IsNullOrEmpty(draftJson))
            {
                try { draft = LevelDraft.From(LevelJson.Parse(draftJson)); }
                catch { draft = null; }
            }
            if (draft == null) draft = new LevelDraft();
            Changed(invalidate: false);
        }

        static Palette LoadPalette()
        {
            string guid = AssetDatabase.FindAssets("t:Palette").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<Palette>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        IEnumerable<char> OilKeys
        {
            get
            {
                if (palette == null) return "PYCVG";
                return palette.Oils.Where(o => !string.IsNullOrEmpty(o.Key)).Select(o => o.Key[0]);
            }
        }

        Color OilColor(char key) => palette != null ? palette.OilColorOf(key) : Color.magenta;

        // ---------------- state changes ----------------

        /// <summary>Snapshot for undo. Call before every edit.</summary>
        void Record()
        {
            undo.Add(LevelJson.ToJsonV2(draft.ToLevel()));
            if (undo.Count > 200) undo.RemoveAt(0);
            redo.Clear();
        }

        /// <summary>After an edit: re-analyze, persist for domain reloads, repaint.</summary>
        void Changed(bool invalidate = true)
        {
            if (invalidate) { draft.Invalidate(); lastScore = null; dirty = true; }
            report = draft.Analyze();
            draftJson = LevelJson.ToJsonV2(draft.ToLevel());
            selection.RemoveWhere(t => !draft.Trucks.Contains(t));
            Repaint();
        }

        void Restore(string json)
        {
            draft = LevelDraft.From(LevelJson.Parse(json));
            selection.Clear();
            Changed(invalidate: false);
            dirty = true;
        }

        void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(LevelJson.ToJsonV2(draft.ToLevel()));
            string s = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Restore(s);
        }

        void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(LevelJson.ToJsonV2(draft.ToLevel()));
            string s = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Restore(s);
        }

        void Say(string text, MessageType type = MessageType.Info)
        {
            message = text;
            messageType = type;
        }

        // ---------------- file ----------------

        void New()
        {
            if (!ConfirmDiscard()) return;
            Record();
            draft = new LevelDraft();
            path = null;
            dirty = false;
            selection.Clear();
            Changed(invalidate: false);
            Say("New empty level. Pick the Place tool (P) and click on the lot, or trace a shape.");
        }

        bool ConfirmDiscard() =>
            !dirty || draft.Trucks.Count == 0 ||
            EditorUtility.DisplayDialog("Level Editor", "Discard unsaved changes?", "Discard", "Cancel");

        void OpenDialog()
        {
            if (!ConfirmDiscard()) return;
            string abs = EditorUtility.OpenFilePanel("Open level", LevelImporter.LevelFolder, "json");
            if (string.IsNullOrEmpty(abs)) return;
            Load(ToProjectPath(abs));
        }

        public void Load(string assetPath)
        {
            try
            {
                var level = LevelJson.Parse(File.ReadAllText(assetPath));
                Record();
                draft = LevelDraft.From(level);
                path = assetPath;
                dirty = false;
                selection.Clear();
                vesselCount = Mathf.Max(1, draft.Vessels.Count);
                Changed(invalidate: false);
                Say($"Opened {Path.GetFileName(assetPath)}" + (level.IsGrid ? " (grid level, converted to free-form)." : "."));
            }
            catch (System.Exception e)
            {
                Say("Could not open level: " + e.Message, MessageType.Error);
            }
        }

        /// <summary>Opens a built-in sample as a new unsaved draft (a starting point to edit).</summary>
        public void LoadSample(string name)
        {
            if (!ConfirmDiscard()) return;
            var level = name == "heart" ? SampleLevels.Heart() : name == "radial" ? SampleLevels.Radial() : SampleLevels.Mandala();
            Record();
            draft = LevelDraft.From(level);
            path = null;
            dirty = true;
            selection.Clear();
            vesselCount = draft.Vessels.Count;
            Changed(invalidate: false);
            Say($"Sample '{level.Pattern}' loaded as a new draft. Save As to keep it.");
        }

        /// <summary>Selects one truck by index (automation and screenshots).</summary>
        public void SelectTruck(int index)
        {
            selection.Clear();
            if (index >= 0 && index < draft.Trucks.Count) selection.Add(draft.Trucks[index]);
            Repaint();
        }

        static string ToProjectPath(string abs)
        {
            abs = abs.Replace('\\', '/');
            string root = Path.GetFullPath(".").Replace('\\', '/') + "/";
            return abs.StartsWith(root) ? abs.Substring(root.Length) : abs;
        }

        void Save(bool saveAs)
        {
            if (saveAs || string.IsNullOrEmpty(path))
            {
                if (!AssetDatabase.IsValidFolder(DraftFolder)) AssetDatabase.CreateFolder("Assets/_TankerJam/Data", "Drafts");
                string name = string.IsNullOrEmpty(path) ? "draft_" + System.DateTime.Now.ToString("MMdd_HHmm") : Path.GetFileNameWithoutExtension(path);
                string chosen = EditorUtility.SaveFilePanelInProject("Save level", name, "json",
                    "Drafts/ is a scratch area. Saving into Data/Levels adds the level to the campaign catalog.",
                    string.IsNullOrEmpty(path) ? DraftFolder : Path.GetDirectoryName(path));
                if (string.IsNullOrEmpty(chosen)) return;
                path = chosen;
            }

            var level = draft.ToLevel();
            bool campaign = path.Replace('\\', '/').StartsWith(LevelImporter.LevelFolder);
            var errors = new List<string>();
            bool verified = LevelVerifier.Verify(level, errors);
            if (campaign && !verified &&
                !EditorUtility.DisplayDialog("Level Editor",
                    "This level doesn't verify yet, so the campaign catalog will reject it:\n\n" + string.Join("\n", errors.Take(6)) +
                    "\n\nSave anyway?", "Save", "Cancel"))
                return;

            File.WriteAllText(path, LevelJson.ToJsonV2(level));
            AssetDatabase.ImportAsset(path);
            dirty = false;
            Say($"Saved {path}" + (verified ? " (verified)." : " (not verified: " + (errors.FirstOrDefault() ?? "?") + ")"),
                verified ? MessageType.Info : MessageType.Warning);
        }

        // ---------------- play ----------------

        void Play()
        {
            if (draft.Trucks.Count == 0) { Say("Place some trucks first.", MessageType.Warning); return; }
            if (report.ProblemCount > 0 && report.JamDepth < 0)
                Say("Playing with layout problems (red/orange trucks).", MessageType.Warning);
            if (draft.Vessels.Count == 0 || !report.OilBalanced)
            {
                Record();
                var fill = draft.AutoFill(Mathf.Max(vesselCount, draft.SuggestedVesselCount()), seed);
                Changed(invalidate: false);
                lastScore = fill.Score;
                if (!fill.Solved) Say("Vessels filled, but no winning order was found. Playing anyway.", MessageType.Warning);
            }
            else if (draft.Solution.Count == 0)
            {
                lastScore = draft.Solve();
                Changed(invalidate: false);
            }

            string json = LevelJson.ToJsonV2(draft.ToLevel());
            if (EditorApplication.isPlaying)
            {
                var app = FindFirstObjectByType<AppRoot>();
                if (app == null) { Say("No AppRoot in the playing scene.", MessageType.Error); return; }
                app.DevPlayLevel(LevelJson.Parse(json));
                FocusGameView();
                return;
            }
            if (EditorSceneManager.GetActiveScene().path != GameScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(GameScenePath);
            }
            File.WriteAllText(AppRoot.EditorPlayLevelPath, json);
            EditorApplication.isPlaying = true;
        }

        static void FocusGameView()
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (type != null) GetWindow(type, false, null, true);
        }

        // ---------------- GUI ----------------

        void OnGUI()
        {
            if (draft == null) OnEnable();
            if (report == null) report = draft.Analyze();
            canvasId = GUIUtility.GetControlID(FocusType.Keyboard);

            var full = position;
            canvas = new Rect(PanelWidth, ToolbarHeight, full.width - PanelWidth, full.height - ToolbarHeight - StatusHeight);

            HandleCanvasInput();
            if (Event.current.type == EventType.Repaint) DrawCanvas();

            // Panel and bars are drawn after the canvas so they cover anything zoomed past its edge.
            DrawToolbar(new Rect(0, 0, full.width, ToolbarHeight));
            DrawStatus(new Rect(PanelWidth, full.height - StatusHeight, full.width - PanelWidth, StatusHeight));
            var panel = new Rect(0, ToolbarHeight, PanelWidth, full.height - ToolbarHeight);
            EditorGUI.DrawRect(panel, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.78f, 0.78f, 0.78f));
            GUILayout.BeginArea(new Rect(panel.x + 6, panel.y + 4, panel.width - 12, panel.height - 8));
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
            DrawPanel();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawToolbar(Rect r)
        {
            GUILayout.BeginArea(r, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40))) New();
            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(44))) OpenDialog();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(44))) Save(false);
            if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(60))) Save(true);
            if (GUILayout.Button("Samples ▾", EditorStyles.toolbarDropDown, GUILayout.Width(72)))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Mandala (6-fold, reference)"), false, () => LoadSample("mandala"));
                menu.AddItem(new GUIContent("Ring of spokes (8-fold)"), false, () => LoadSample("radial"));
                menu.AddItem(new GUIContent("Heart"), false, () => LoadSample("heart"));
                menu.ShowAsContext();
            }
            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(undo.Count == 0))
                if (GUILayout.Button("Undo", EditorStyles.toolbarButton, GUILayout.Width(44))) Undo();
            using (new EditorGUI.DisabledScope(redo.Count == 0))
                if (GUILayout.Button("Redo", EditorStyles.toolbarButton, GUILayout.Width(44))) Redo();
            GUILayout.Space(8);
            GUILayout.Label((string.IsNullOrEmpty(path) ? "(unsaved draft)" : path) + (dirty ? " *" : ""), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(34))) { zoom = 1f; pan = Vector2.zero; }
            if (GUILayout.Button("Solve", EditorStyles.toolbarButton, GUILayout.Width(50))) SolveNow();
            var old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.45f, 0.9f, 0.5f);
            if (GUILayout.Button("▶ Play", EditorStyles.toolbarButton, GUILayout.Width(60))) Play();
            GUI.backgroundColor = old;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawStatus(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));
            string text;
            if (hovered != null)
            {
                int i = draft.Trucks.IndexOf(hovered);
                var issue = i >= 0 && i < report.Issues.Length ? report.Issues[i] : DraftIssue.None;
                text = $"Truck {i}: length {hovered.Len}, color {hovered.Color}, {hovered.Angle:0} deg, at ({hovered.X:0.##}, {hovered.Z:0.##})" +
                       (i >= 0 && i < report.BlockedBy.Length ? $", blocked by {report.BlockedBy[i]}" : "") +
                       (issue != DraftIssue.None ? $"  [{issue}]" : "");
            }
            else
            {
                var b = ToBoard(mouse);
                text = canvas.Contains(mouse) ? $"({b.X:0.##}, {b.Z:0.##})   tool: {tool}   angle step {AngleStep}   snap {SnapLabels[snapIndex]}" : "";
            }
            GUI.Label(new Rect(r.x + 6, r.y + 2, r.width - 12, r.height), text, EditorStyles.whiteMiniLabel);
        }

        // ---------------- panel ----------------

        void DrawPanel()
        {
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);

            Header("Lot");
            int size = EditorGUILayout.IntSlider("Lot size", draft.LotSize, LevelDraft.MinLotSize, LevelDraft.MaxLotSize);
            if (size != draft.LotSize) { Record(); draft.LotSize = size; Changed(); }
            int slots = EditorGUILayout.IntSlider("Regular bays", draft.Slots, 1, LevelVerifier.MaxRegularBays);
            if (slots != draft.Slots) { Record(); draft.Slots = slots; Changed(); }

            Header("Brush");
            tool = (Tool)GUILayout.Toolbar((int)tool, new[] { "Select (S)", "Place (P)", "Cone" });
            brushLen = LengthButtons("Length", brushLen);
            brushColor = ColorButtons("Color", brushColor);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Angle");
            if (GUILayout.Button($"-{AngleStep:0}", GUILayout.Width(36))) brushAngle = LevelDraft.Norm(brushAngle - AngleStep);
            GUILayout.Label($"{brushAngle:0}°", GUILayout.Width(40));
            if (GUILayout.Button($"+{AngleStep:0}", GUILayout.Width(36))) brushAngle = LevelDraft.Norm(brushAngle + AngleStep);
            EditorGUILayout.EndHorizontal();
            fineAngles = EditorGUILayout.Toggle("15° steps (else 45°)", fineAngles);
            snapIndex = EditorGUILayout.Popup("Position snap", snapIndex, SnapLabels);

            Header(selection.Count > 0 ? $"Selection ({selection.Count})" : "Selection (none)");
            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"-{AngleStep:0}° (Q)")) RotateSelection(-AngleStep);
                if (GUILayout.Button($"+{AngleStep:0}° (E)")) RotateSelection(AngleStep);
                if (GUILayout.Button("Flip F")) RotateSelection(180f);
                if (GUILayout.Button("Delete")) DeleteSelection();
                EditorGUILayout.EndHorizontal();
                int len = LengthButtons("Set length", selection.Count > 0 ? selection.First().Len : 0);
                if (selection.Count > 0 && len != selection.First().Len) ApplyToSelection(t => t.Len = len, "length");
                char c = ColorButtons("Set color", selection.Count > 0 ? selection.First().Color : ' ');
                if (selection.Count > 0 && c != selection.First().Color) ApplyToSelection(t => t.Color = c, "color");
            }

            Header("Symmetry");
            EditorGUILayout.LabelField(selection.Count > 0 ? "Copies the selection." : "Copies every truck (nothing selected).", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mirror ←→")) Symmetry("mirror left/right", src => (draft.MirrorX(src, out int r), r));
            if (GUILayout.Button("Mirror ↑↓")) Symmetry("mirror top/bottom", src => (draft.MirrorZ(src, out int r), r));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            int ki = System.Array.IndexOf(SymmetryOptions, symmetryK);
            ki = EditorGUILayout.Popup(ki < 0 ? 5 : ki, SymmetryOptions.Select(k => $"{k}-fold").ToArray(), GUILayout.Width(80));
            symmetryK = SymmetryOptions[ki];
            if (GUILayout.Button($"Rotate copies ×{symmetryK}"))
                Symmetry($"{symmetryK}-fold rotation", src => (draft.RotateCopies(src, symmetryK, out int r), r));
            EditorGUILayout.EndHorizontal();

            Header("Generate");
            genLevel = EditorGUILayout.IntSlider("Like curve level", genLevel, 11, 50);
            genFamily = EditorGUILayout.Popup("Layout", genFamily, new[] { "Any", "Petals (rotation)", "Mirror", "Traced shape" });
            if (GUILayout.Button($"Generate (seed {seed})")) Generate();

            Header("Trace a shape");
            traceShape = (TraceShape)EditorGUILayout.EnumPopup("Shape", traceShape);
            traceFacing = (TraceFacing)EditorGUILayout.EnumPopup("Trucks point", traceFacing);
            traceSize = EditorGUILayout.Slider("Size", traceSize, 0.8f, draft.Half);
            traceSpacing = EditorGUILayout.Slider("Spacing", traceSpacing, 0.8f, 3f);
            traceColors = EditorGUILayout.TextField("Colors (in order)", traceColors);
            if (GUILayout.Button($"Trace {traceShape} (length {brushLen}, {AngleStep}° steps)")) Trace();

            Header("Colors and oil");
            EditorGUILayout.BeginHorizontal();
            colorCount = EditorGUILayout.IntSlider("Colors", colorCount, 1, OilKeys.Count());
            if (GUILayout.Button("Auto color", GUILayout.Width(80))) AutoColor();
            EditorGUILayout.EndHorizontal();
            DrawOilBalance();
            EditorGUILayout.BeginHorizontal();
            vesselCount = EditorGUILayout.IntSlider("Vessels", vesselCount, 1, LevelVerifier.MaxVessels);
            if (GUILayout.Button("Suggest", GUILayout.Width(60))) vesselCount = Mathf.Min(LevelVerifier.MaxVessels, draft.SuggestedVesselCount());
            EditorGUILayout.EndHorizontal();
            useTargetRate = EditorGUILayout.Toggle("Aim for a difficulty", useTargetRate);
            if (useTargetRate) targetWinRate = EditorGUILayout.Slider("Random win rate", targetWinRate, 0f, 1f);
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Fill vessels (solvable)")) AutoFill();
            DrawVessels();

            Header("Check");
            DrawReport();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Keys: Q/E rotate · F flip · 2/3/4 length · C color · arrows nudge · Del delete · Ctrl+D duplicate · Ctrl+A all · Ctrl+Z/Y undo/redo · wheel zoom · middle-drag pan",
                EditorStyles.wordWrappedMiniLabel);
        }

        static void Header(string text)
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        int LengthButtons(string label, int current)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            for (int len = 2; len <= 4; len++)
            {
                var old = GUI.backgroundColor;
                if (len == current) GUI.backgroundColor = new Color(0.5f, 0.75f, 1f);
                if (GUILayout.Button(len.ToString(), GUILayout.Width(30))) current = len;
                GUI.backgroundColor = old;
            }
            EditorGUILayout.EndHorizontal();
            return current;
        }

        char ColorButtons(string label, char current)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            foreach (char key in OilKeys)
            {
                var r = GUILayoutUtility.GetRect(24, 20, GUILayout.Width(24));
                EditorGUI.DrawRect(r, OilColor(key));
                if (key == current) DrawFrame(r, Color.white, 2);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) current = key;
            }
            EditorGUILayout.EndHorizontal();
            return current;
        }

        static void DrawFrame(Rect r, Color c, float w)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        void DrawOilBalance()
        {
            foreach (var kv in report.UnitsNeeded)
            {
                report.UnitsInVessels.TryGetValue(kv.Key, out int have);
                var r = EditorGUILayout.GetControlRect(false, 16);
                EditorGUI.DrawRect(new Rect(r.x, r.y + 2, 12, 12), OilColor(kv.Key));
                var style = have == kv.Value ? EditorStyles.miniLabel : EditorStyles.boldLabel;
                GUI.Label(new Rect(r.x + 18, r.y, r.width - 18, r.height),
                    $"{(palette != null ? palette.NameOf(kv.Key) : kv.Key.ToString())}: trucks take {kv.Value}, vessels hold {have}", style);
            }
        }

        void DrawVessels()
        {
            for (int v = 0; v < draft.Vessels.Count; v++)
            {
                string s = new string(draft.Vessels[v].ToArray());
                string edited = EditorGUILayout.DelayedTextField($"Vessel {v + 1} (bottom→top)", s);
                if (edited != s)
                {
                    Record();
                    draft.Vessels[v].Clear();
                    draft.Vessels[v].AddRange(edited.ToUpperInvariant().Where(ch => OilKeys.Contains(ch)));
                    Changed();
                }
            }
        }

        void DrawReport()
        {
            int problems = report.ProblemCount;
            EditorGUILayout.LabelField($"{draft.Trucks.Count} trucks, {draft.Cones.Count} cones, {draft.Vessels.Count} vessels, {draft.Slots} bays");
            if (problems > 0)
                EditorGUILayout.HelpBox($"{problems} truck(s) have problems: red = overlaps or off the lot, orange = a cone blocks its road forever.", MessageType.Warning);
            EditorGUILayout.LabelField("Jam depth", report.JamDepth < 0 ? "lot can't be cleared (blocking cycle)" : report.JamDepth.ToString());
            EditorGUILayout.LabelField("Oil", report.OilBalanced ? "balanced" : "not balanced (Fill vessels)");
            if (lastScore != null)
            {
                var s = lastScore.Solve;
                EditorGUILayout.LabelField("Solver", $"{s.Status}, {s.NodesVisited} nodes, {s.Milliseconds:0} ms");
                if (s.Status == SolveStatus.Solved)
                {
                    EditorGUILayout.LabelField("Random win rate", $"{lastScore.RandomWinRate:P0}  ({Difficulty(lastScore.RandomWinRate)})");
                    EditorGUILayout.LabelField("Taps to win", s.Solution.Count.ToString());
                }
            }
            else if (draft.Solution.Count > 0)
                EditorGUILayout.LabelField("Solver", $"solved ({draft.Solution.Count} taps), win rate {draft.RandomWinRate:P0}");
            else
                EditorGUILayout.LabelField("Solver", "not solved yet");
        }

        static string Difficulty(float rate) =>
            rate >= 0.6f ? "easy" : rate >= 0.3f ? "medium" : rate >= 0.1f ? "hard" : "very hard";

        // ---------------- commands ----------------

        void SolveNow()
        {
            if (draft.Vessels.Count == 0 || !report.OilBalanced) { Say("Fill the vessels first (Colors and oil).", MessageType.Warning); return; }
            lastScore = draft.Solve();
            Changed(invalidate: false);
            var s = lastScore.Solve;
            Say(s.Status == SolveStatus.Solved
                    ? $"Solved in {s.Solution.Count} taps. Random players win {lastScore.RandomWinRate:P0} ({Difficulty(lastScore.RandomWinRate)})."
                    : s.Status == SolveStatus.Unsolvable ? "No winning order exists. Change the vessels or the layout." : "Search limit reached.",
                s.Status == SolveStatus.Solved ? MessageType.Info : MessageType.Warning);
        }

        void AutoColor()
        {
            Record();
            draft.AutoColor(OilKeys.Take(colorCount).ToList(), seed);
            Changed();
            Say($"Recolored with {colorCount} colors. Fill the vessels next.");
        }

        void AutoFill()
        {
            if (draft.Trucks.Count == 0) return;
            Record();
            var fill = draft.AutoFill(vesselCount, seed, 40, useTargetRate ? targetWinRate : -1f);
            lastScore = fill.Score;
            Changed(invalidate: false);
            dirty = true;
            if (fill.Solved)
                Say($"Vessels filled after {fill.Attempts} tries: solvable, random win rate {fill.Score.RandomWinRate:P0} ({Difficulty(fill.Score.RandomWinRate)}).");
            else
                Say($"No solvable vessel order in {fill.Attempts} tries. Try another seed, more bays, fewer colors, or a less tangled layout.", MessageType.Warning);
        }

        void Generate()
        {
            if (!ConfirmDiscard()) return;
            PatternCurve curve;
            try { curve = PatternLevelBuilder.LoadCurve(); }
            catch (System.Exception e) { Say("Can't read the curve: " + e.Message, MessageType.Error); return; }
            var spec = curve.SpecFor(genLevel, genFamily == 0 ? (PatternFamily?)null : (PatternFamily)(genFamily - 1), seedOffset: seed);
            var r = PatternGenerator.Generate(spec);
            if (!r.Ok) { Say("Nothing in band: " + r.Failure + ". Try another seed or layout.", MessageType.Warning); return; }
            Record();
            draft = r.Draft;
            draft.Index = 0;
            path = null;
            dirty = true;
            selection.Clear();
            vesselCount = draft.Vessels.Count;
            lastScore = r.Score;
            Changed(invalidate: false);
            Say($"Generated '{r.Pattern}' like level {genLevel}: {draft.Trucks.Count} trucks, random win rate {r.Score.RandomWinRate:P0} " +
                $"({Difficulty(r.Score.RandomWinRate)}), jam depth {r.JamDepth}. Change the seed to re-roll.");
        }

        void Trace()
        {
            Record();
            var center = selection.Count == 1 ? selection.First().Center : new Vec2(0f, 0f);
            var added = ShapeTracer.Trace(draft, new TraceOptions
            {
                Shape = traceShape, Facing = traceFacing, Size = traceSize, Spacing = traceSpacing, Len = brushLen,
                AngleStep = AngleStep, Colors = string.IsNullOrEmpty(traceColors) ? brushColor.ToString() : traceColors.ToUpperInvariant(),
                CenterX = center.X, CenterZ = center.Z,
            });
            selection.Clear();
            foreach (var t in added) selection.Add(t);
            Changed();
            Say($"Traced a {traceShape}: {added.Count} trucks added.");
        }

        void Symmetry(string what, System.Func<IList<DraftTruck>, (List<DraftTruck> added, int rejected)> op)
        {
            var src = selection.Count > 0 ? selection.ToList() : draft.Trucks.ToList();
            if (src.Count == 0) return;
            Record();
            var (added, rejected) = op(src);
            foreach (var t in added) selection.Add(t);
            Changed();
            Say($"{what}: {added.Count} copies added" + (rejected > 0 ? $", {rejected} skipped (no room)." : "."),
                rejected > 0 ? MessageType.Warning : MessageType.Info);
        }

        void RotateSelection(float degrees)
        {
            if (selection.Count == 0) return;
            Record();
            foreach (var t in selection) t.Angle = LevelDraft.Norm(t.Angle + degrees);
            Changed();
        }

        void ApplyToSelection(System.Action<DraftTruck> change, string what)
        {
            Record();
            foreach (var t in selection) change(t);
            Changed();
            Say($"Set {what} on {selection.Count} truck(s).");
        }

        void DeleteSelection()
        {
            if (selection.Count == 0) return;
            Record();
            draft.Trucks.RemoveAll(selection.Contains);
            selection.Clear();
            Changed();
        }

        void Duplicate()
        {
            if (selection.Count == 0) return;
            Record();
            var copies = new List<DraftTruck>();
            foreach (var t in selection)
            {
                var c = t.Copy();
                c.X = LevelDraft.Snap(c.X + 1f);
                draft.Trucks.Add(c);
                copies.Add(c);
            }
            selection.Clear();
            foreach (var c in copies) selection.Add(c);
            Changed();
        }

        void Nudge(float dx, float dz)
        {
            if (selection.Count == 0) return;
            Record();
            foreach (var t in selection) { t.X = LevelDraft.Snap(t.X + dx); t.Z = LevelDraft.Snap(t.Z + dz); }
            Changed();
        }

        // ---------------- canvas input ----------------

        float Scale => Mathf.Min(canvas.width, canvas.height) / (draft.LotSize + 2.5f) * zoom;
        Vector2 Origin => canvas.center + pan;
        Vector2 ToScreen(Vec2 b) => Origin + new Vector2(b.X, b.Z) * Scale;
        Vector2 ToScreen(float x, float z) => Origin + new Vector2(x, z) * Scale;
        Vec2 ToBoard(Vector2 s) => new Vec2((s.x - Origin.x) / Scale, (s.y - Origin.y) / Scale);

        DraftTruck HitTest(Vec2 p)
        {
            for (int i = draft.Trucks.Count - 1; i >= 0; i--)
            {
                var o = draft.Trucks[i].Shape;
                var d = p - o.Center;
                if (Mathf.Abs(Geometry2D.Dot(d, o.Axis)) <= o.HalfLength + 0.05f && Mathf.Abs(Geometry2D.Dot(d, o.Side)) <= o.HalfWidth + 0.05f)
                    return draft.Trucks[i];
            }
            return null;
        }

        int ConeAt(Vec2 p)
        {
            for (int i = 0; i < draft.Cones.Count; i++)
                if ((p - draft.Cones[i]).Length <= Obstacle.ConeHalfSize + 0.1f) return i;
            return -1;
        }

        Vec2 SnapBoard(Vec2 p) => new Vec2(LevelDraft.Snap(LevelDraft.Snap(p.X, SnapStep)), LevelDraft.Snap(LevelDraft.Snap(p.Z, SnapStep)));

        void HandleCanvasInput()
        {
            var e = Event.current;
            mouse = e.mousePosition;
            bool inCanvas = canvas.Contains(e.mousePosition);

            switch (e.type)
            {
                case EventType.MouseMove:
                    if (inCanvas)
                    {
                        var h = HitTest(ToBoard(e.mousePosition));
                        if (h != hovered) hovered = h;
                        Repaint();
                    }
                    break;

                case EventType.ScrollWheel when inCanvas:
                {
                    var before = ToBoard(e.mousePosition);
                    zoom = Mathf.Clamp(zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.4f, 4f);
                    var after = ToScreen(before);
                    pan += e.mousePosition - after;
                    e.Use();
                    break;
                }

                case EventType.MouseDown when inCanvas:
                    GUIUtility.keyboardControl = canvasId;
                    if (e.button == 2 || (e.button == 0 && e.alt)) { panning = true; e.Use(); break; }
                    if (e.button == 1) { ContextMenu(ToBoard(e.mousePosition)); e.Use(); break; }
                    if (e.button == 0) { MouseDown(e); e.Use(); }
                    break;

                case EventType.MouseDrag:
                    if (panning) { pan += e.delta; e.Use(); Repaint(); break; }
                    if (dragging) { DragTo(e.mousePosition); e.Use(); }
                    else if (boxSelecting) { Repaint(); e.Use(); }
                    break;

                case EventType.MouseUp:
                    if (panning) { panning = false; e.Use(); }
                    if (dragging)
                    {
                        dragging = false;
                        if (dragRecorded) Changed();
                        e.Use();
                    }
                    if (boxSelecting)
                    {
                        boxSelecting = false;
                        BoxSelect(boxStart, e.mousePosition, e.shift);
                        e.Use();
                    }
                    break;

                case EventType.KeyDown when GUIUtility.keyboardControl == canvasId || (inCanvas && GUIUtility.keyboardControl == 0):
                    if (HandleKey(e)) e.Use();
                    break;

                case EventType.ValidateCommand when GUIUtility.keyboardControl == canvasId:
                    if (e.commandName == "Delete" || e.commandName == "SoftDelete" || e.commandName == "Duplicate" || e.commandName == "SelectAll") e.Use();
                    break;

                case EventType.ExecuteCommand when GUIUtility.keyboardControl == canvasId:
                    if (e.commandName == "Delete" || e.commandName == "SoftDelete") { DeleteSelection(); e.Use(); }
                    else if (e.commandName == "Duplicate") { Duplicate(); e.Use(); }
                    else if (e.commandName == "SelectAll") { SelectAll(); e.Use(); }
                    break;
            }
        }

        void MouseDown(Event e)
        {
            var b = ToBoard(e.mousePosition);
            var hit = HitTest(b);

            if (tool == Tool.Cone && hit == null)
            {
                int cone = ConeAt(b);
                Record();
                if (cone >= 0) draft.Cones.RemoveAt(cone);
                else draft.Cones.Add(SnapBoard(b));
                Changed();
                return;
            }

            if (tool == Tool.Place && hit == null)
            {
                var p = SnapBoard(b);
                Record();
                var t = draft.TryAdd(p.X, p.Z, brushAngle, brushLen, brushColor);
                if (t == null) { undo.RemoveAt(undo.Count - 1); Say("No room there.", MessageType.Warning); }
                else { selection.Clear(); selection.Add(t); Changed(); }
                return;
            }

            if (hit == null)
            {
                if (!e.shift) selection.Clear();
                boxSelecting = true;
                boxStart = e.mousePosition;
                Repaint();
                return;
            }

            if (e.shift) { if (!selection.Remove(hit)) selection.Add(hit); Repaint(); return; }
            if (!selection.Contains(hit)) { selection.Clear(); selection.Add(hit); }

            // Start moving the selection (undo is recorded on the first actual move).
            dragging = true;
            dragRecorded = false;
            dragStartBoard = b;
            dragOrigins.Clear();
            foreach (var t in selection) dragOrigins[t] = t.Center;
            Repaint();
        }

        void DragTo(Vector2 m)
        {
            var b = ToBoard(m);
            float dx = LevelDraft.Snap(b.X - dragStartBoard.X, SnapStep), dz = LevelDraft.Snap(b.Z - dragStartBoard.Z, SnapStep);
            if (!dragRecorded)
            {
                if (dx == 0f && dz == 0f) return;
                Record();
                dragRecorded = true;
            }
            foreach (var kv in dragOrigins)
            {
                kv.Key.X = LevelDraft.Snap(kv.Value.X + dx);
                kv.Key.Z = LevelDraft.Snap(kv.Value.Z + dz);
            }
            report = draft.Analyze();
            Repaint();
        }

        void BoxSelect(Vector2 a, Vector2 b, bool add)
        {
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            if (r.width < 3 && r.height < 3) { Repaint(); return; }
            if (!add) selection.Clear();
            foreach (var t in draft.Trucks)
                if (r.Contains(ToScreen(t.Center))) selection.Add(t);
            Repaint();
        }

        void SelectAll()
        {
            selection.Clear();
            foreach (var t in draft.Trucks) selection.Add(t);
            Repaint();
        }

        bool HandleKey(Event e)
        {
            bool ctrl = e.control || e.command;
            switch (e.keyCode)
            {
                case KeyCode.Z when ctrl: Undo(); return true;
                case KeyCode.Y when ctrl: Redo(); return true;
                case KeyCode.D when ctrl: Duplicate(); return true;
                case KeyCode.A when ctrl: SelectAll(); return true;
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteSelection(); return true;
                case KeyCode.Q:
                    if (selection.Count > 0) RotateSelection(-AngleStep); else brushAngle = LevelDraft.Norm(brushAngle - AngleStep);
                    Repaint(); return true;
                case KeyCode.E:
                    if (selection.Count > 0) RotateSelection(AngleStep); else brushAngle = LevelDraft.Norm(brushAngle + AngleStep);
                    Repaint(); return true;
                case KeyCode.F: RotateSelection(180f); return true;
                case KeyCode.Alpha2: case KeyCode.Alpha3: case KeyCode.Alpha4:
                {
                    int len = e.keyCode - KeyCode.Alpha0;
                    brushLen = len;
                    if (selection.Count > 0) ApplyToSelection(t => t.Len = len, "length");
                    Repaint(); return true;
                }
                case KeyCode.C when !ctrl:
                {
                    var keys = OilKeys.ToList();
                    char next = keys[(keys.IndexOf(selection.Count > 0 ? selection.First().Color : brushColor) + 1) % keys.Count];
                    brushColor = next;
                    if (selection.Count > 0) ApplyToSelection(t => t.Color = next, "color");
                    Repaint(); return true;
                }
                case KeyCode.P when !ctrl: tool = Tool.Place; Repaint(); return true;
                case KeyCode.S when !ctrl: tool = Tool.Select; Repaint(); return true;
                case KeyCode.Escape: selection.Clear(); tool = Tool.Select; Repaint(); return true;
                case KeyCode.LeftArrow: Nudge(-SnapStep, 0); return true;
                case KeyCode.RightArrow: Nudge(SnapStep, 0); return true;
                case KeyCode.UpArrow: Nudge(0, -SnapStep); return true;
                case KeyCode.DownArrow: Nudge(0, SnapStep); return true;
            }
            return false;
        }

        void ContextMenu(Vec2 b)
        {
            var hit = HitTest(b);
            var menu = new GenericMenu();
            if (hit != null)
            {
                if (!selection.Contains(hit)) { selection.Clear(); selection.Add(hit); }
                menu.AddItem(new GUIContent("Flip direction"), false, () => RotateSelection(180f));
                menu.AddItem(new GUIContent("Duplicate"), false, Duplicate);
                menu.AddItem(new GUIContent("Delete"), false, DeleteSelection);
            }
            else
            {
                var p = SnapBoard(b);
                menu.AddItem(new GUIContent($"Add truck here (length {brushLen}, {brushAngle:0}°)"), false, () =>
                {
                    Record();
                    if (draft.TryAdd(p.X, p.Z, brushAngle, brushLen, brushColor) == null) { undo.RemoveAt(undo.Count - 1); Say("No room there.", MessageType.Warning); }
                    else Changed();
                });
                int cone = ConeAt(b);
                if (cone >= 0) menu.AddItem(new GUIContent("Remove cone"), false, () => { Record(); draft.Cones.RemoveAt(cone); Changed(); });
                else menu.AddItem(new GUIContent("Add cone here"), false, () => { Record(); draft.Cones.Add(p); Changed(); });
                menu.AddItem(new GUIContent("Center of shapes here"), false, () => Say("Select a single truck to trace a shape around it; otherwise shapes center on the lot."));
            }
            menu.ShowAsContext();
        }

        // ---------------- canvas drawing ----------------

        void DrawCanvas()
        {
            EditorGUI.DrawRect(canvas, new Color(0.13f, 0.14f, 0.16f));

            float h = draft.Half;
            var lotColor = palette != null ? palette.Lot : new Color(0.64f, 0.68f, 0.75f);
            var tl = ToScreen(-h, -h);
            var br = ToScreen(h, h);
            var lot = Rect.MinMaxRect(tl.x, tl.y, br.x, br.y);
            EditorGUI.DrawRect(new Rect(lot.x - 3, lot.y - 3, lot.width + 6, lot.height + 6), Color.white);
            EditorGUI.DrawRect(lot, lotColor);

            // Unit grid (placement aid only; the game lot is plain), center lines = symmetry axes.
            var grid = new Color(1f, 1f, 1f, 0.12f);
            for (int i = 1; i < draft.LotSize; i++)
            {
                float u = -h + i;
                EditorGUI.DrawRect(new Rect(ToScreen(u, -h).x, lot.y, 1, lot.height), grid);
                EditorGUI.DrawRect(new Rect(lot.x, ToScreen(-h, u).y, lot.width, 1), grid);
            }
            var axis = new Color(1f, 1f, 1f, 0.3f);
            EditorGUI.DrawRect(new Rect(ToScreen(0, 0).x, lot.y, 1, lot.height), axis);
            EditorGUI.DrawRect(new Rect(lot.x, ToScreen(0, 0).y, lot.width, 1), axis);
            GUI.Label(new Rect(lot.center.x - 60, lot.y - 22, 120, 18), "▲ BAYS ▲", CenteredLabel);

            // Cones.
            Handles.color = new Color(1f, 0.48f, 0.1f);
            foreach (var c in draft.Cones)
                Handles.DrawSolidDisc(ToScreen(c), Vector3.forward, Obstacle.ConeHalfSize * Scale);

            // Corridor and blockers of the single selected (or hovered) truck.
            var focus = selection.Count == 1 ? selection.First() : hovered;
            HashSet<DraftTruck> blockers = null;
            if (focus != null && !dragging) blockers = DrawCorridor(focus);

            for (int i = 0; i < draft.Trucks.Count; i++)
            {
                var t = draft.Trucks[i];
                var issue = i < report.Issues.Length ? report.Issues[i] : DraftIssue.None;
                DrawTruck(t, issue, selection.Contains(t), t == hovered, blockers != null && blockers.Contains(t));
            }

            // Placement ghost.
            if (tool == Tool.Place && canvas.Contains(mouse) && !dragging && HitTest(ToBoard(mouse)) == null)
            {
                var p = SnapBoard(ToBoard(mouse));
                var ghost = new DraftTruck { X = p.X, Z = p.Z, Angle = brushAngle, Len = brushLen, Color = brushColor };
                bool fits = draft.Fits(ghost);
                DrawBody(ghost, OilColor(brushColor) * new Color(1, 1, 1, 0.45f), fits ? new Color(1, 1, 1, 0.8f) : Color.red, 2f);
            }

            if (boxSelecting)
            {
                var r = Rect.MinMaxRect(Mathf.Min(boxStart.x, mouse.x), Mathf.Min(boxStart.y, mouse.y), Mathf.Max(boxStart.x, mouse.x), Mathf.Max(boxStart.y, mouse.y));
                EditorGUI.DrawRect(r, new Color(0.4f, 0.7f, 1f, 0.15f));
                DrawFrame(r, new Color(0.4f, 0.7f, 1f, 0.8f), 1);
            }
            Handles.color = Color.white;
        }

        static GUIStyle centered;
        static GUIStyle CenteredLabel => centered ??= new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleCenter };

        void DrawTruck(DraftTruck t, DraftIssue issue, bool selected, bool hover, bool isBlocker)
        {
            Color outline = new Color(0, 0, 0, 0.6f);
            float width = 1.5f;
            if ((issue & (DraftIssue.Overlap | DraftIssue.OutOfLot)) != 0) { outline = Color.red; width = 3f; }
            else if ((issue & DraftIssue.Stuck) != 0) { outline = new Color(1f, 0.55f, 0f); width = 3f; }
            if (isBlocker) { outline = Color.yellow; width = 3f; }
            if (selected) { outline = Color.white; width = 3.5f; }
            else if (hover) { outline = new Color(1, 1, 1, 0.8f); width = 2.5f; }
            DrawBody(t, OilColor(t.Color), outline, width);
        }

        readonly Vector3[] quad = new Vector3[4];
        readonly Vector3[] loop = new Vector3[5];

        void DrawBody(DraftTruck t, Color fill, Color outline, float outlineWidth)
        {
            var o = t.Shape;
            for (int k = 0; k < 4; k++) quad[k] = ToScreen(o.Corner(k));
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(quad);

            // Cab: the front 0.55 units, darker.
            var cab = new Obb(o.Center + o.Axis * (o.HalfLength - 0.275f), o.Axis, 0.275f, o.HalfWidth);
            for (int k = 0; k < 4; k++) quad[k] = ToScreen(cab.Corner(k));
            Handles.color = Color.Lerp(fill, Color.black, 0.35f);
            Handles.DrawAAConvexPolygon(quad);

            // Arrow along the heading.
            Handles.color = new Color(1, 1, 1, 0.9f);
            var tail = ToScreen(o.Center - o.Axis * (o.HalfLength * 0.55f));
            var tip = ToScreen(o.Center + o.Axis * (o.HalfLength * 0.45f));
            var side = new Vector2(o.Side.X, o.Side.Z) * (0.18f * Scale);
            var back = (Vector2)(tip - tail).normalized * (0.25f * Scale);
            Handles.DrawAAPolyLine(2.5f, tail, tip);
            Handles.DrawAAPolyLine(2.5f, tip, (Vector3)((Vector2)tip - back + side));
            Handles.DrawAAPolyLine(2.5f, tip, (Vector3)((Vector2)tip - back - side));

            for (int k = 0; k < 4; k++) loop[k] = ToScreen(o.Corner(k));
            loop[4] = loop[0];
            Handles.color = outline;
            Handles.DrawAAPolyLine(outlineWidth, loop);
        }

        /// <summary>Draws the road a truck would take out of the lot; returns the trucks in the way.</summary>
        HashSet<DraftTruck> DrawCorridor(DraftTruck t)
        {
            var o = t.Shape;
            // Far enough to leave any square lot.
            float reach = draft.LotSize * 1.5f;
            var front = o.Center + o.Axis * o.HalfLength;
            var blockers = new HashSet<DraftTruck>();
            float nearest = reach;
            foreach (var other in draft.Trucks)
            {
                if (other == t) continue;
                if (Geometry2D.TimeOfImpact(o, o.Axis, other.Shape, out float hit))
                {
                    blockers.Add(other);
                    nearest = Mathf.Min(nearest, hit);
                }
            }
            // Clip the drawn band at the lot edge (or the first blocker).
            float toEdge = DistanceToLotEdge(front, o.Axis);
            float len = Mathf.Min(toEdge, nearest);
            var band = new Obb(front + o.Axis * (len / 2f), o.Axis, len / 2f, o.HalfWidth);
            for (int k = 0; k < 4; k++) quad[k] = ToScreen(band.Corner(k));
            Handles.color = blockers.Count == 0 ? new Color(0.3f, 1f, 0.4f, 0.25f) : new Color(1f, 0.3f, 0.3f, 0.25f);
            Handles.DrawAAConvexPolygon(quad);
            return blockers;
        }

        float DistanceToLotEdge(Vec2 from, Vec2 dir)
        {
            float h = draft.Half, best = float.MaxValue;
            if (dir.X > 1e-4f) best = Mathf.Min(best, (h - from.X) / dir.X);
            if (dir.X < -1e-4f) best = Mathf.Min(best, (-h - from.X) / dir.X);
            if (dir.Z > 1e-4f) best = Mathf.Min(best, (h - from.Z) / dir.Z);
            if (dir.Z < -1e-4f) best = Mathf.Min(best, (-h - from.Z) / dir.Z);
            return Mathf.Max(0f, best);
        }
    }
}
