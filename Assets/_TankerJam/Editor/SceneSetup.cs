// Menu: Tanker Jam > Setup > Create Game Scene.
// Builds Scenes/Game.unity from code: camera + fitter, sun + lighting rig, game controller, audio, confetti,
// the UI canvas (HUD, popups, home, tutorial), the EventSystem and the AppRoot that runs the app flow.
// Registers the scene in Build Settings. Re-running replaces the scene (UI prefabs are reused, not rebuilt).
using TankerJam.App;
using TankerJam.Game;
using TankerJam.Meta;
using TankerJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TankerJam.EditorTools
{
    public static class SceneSetup
    {
        const string ScenePath = "Assets/_TankerJam/Scenes/Game.unity";
        const string ConfigPath = "Assets/_TankerJam/Config/GameConfig.asset";
        const string StartLevelPath = "Assets/_TankerJam/Tests/EditMode/Fixtures/reference_level.json";
        const string AudioCatalogPath = "Assets/_TankerJam/Config/AudioCatalog.asset";
        const string EconomyPath = "Assets/_TankerJam/Config/EconomyConfig.asset";
        const string ParticleShaderPath = "Assets/_TankerJam/Art/Shaders/Particle.shader";

        [MenuItem("Tanker Jam/Setup/Create Game Scene")]
        public static void CreateGameScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) == null)
            {
                Debug.LogError("Tanker Jam: run 'Tanker Jam/Setup/Build Greybox Assets' first.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{UiBuilder.Dir}/HUD.prefab") == null) UiBuilder.BuildAll();
            if (AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelImporter.CatalogPath) == null) LevelImporter.RebuildCatalog();

            System.IO.Directory.CreateDirectory("Assets/_TankerJam/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: switching scenes unloads unreferenced assets loaded before it.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);

            // Camera.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.Palette.Sky;
            cam.fieldOfView = config.Layout.FieldOfView;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            var fitter = camGo.AddComponent<CameraFitter>();
            fitter.TopInset = 0.13f;    // top bar + coin pill
            fitter.BottomInset = 0.14f; // booster bar
            camGo.AddComponent<AudioListener>();

            // Sun.
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var lighting = new GameObject("Lighting").AddComponent<LightingRig>();
            lighting.Sun = sun;
            lighting.Apply(config.Palette);

            // Game (levels are loaded by AppRoot; the start level is only for running the controller alone).
            var gameGo = new GameObject("Game");
            var controller = gameGo.AddComponent<GameController>();
            controller.EditorWire(config, cam, fitter, lighting, AssetDatabase.LoadAssetAtPath<TextAsset>(StartLevelPath), autoStart: false);
            EditorUtility.SetDirty(controller);

            var debug = gameGo.AddComponent<DebugHud>();
            var debugSo = new SerializedObject(debug);
            debugSo.FindProperty("game").objectReferenceValue = controller;
            debugSo.ApplyModifiedPropertiesWithoutUndo();
            debug.ShowGameplayControls = false;

            // Audio and confetti.
            var audioCatalog = LoadOrCreate<AudioCatalog>(AudioCatalogPath);
            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.EditorWire(controller, audioCatalog);
            var confetti = new GameObject("Confetti").AddComponent<ConfettiFx>();
            confetti.EditorWire(controller, AssetDatabase.LoadAssetAtPath<Shader>(ParticleShaderPath));

            // UI.
            var canvasGo = new GameObject("UI", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var hud = Instantiate<Hud>("HUD", canvasGo.transform);
            var tutorial = Instantiate<TutorialHand>("TutorialHand", canvasGo.transform);
            var popup = Instantiate<ResultPopup>("ResultPopup", canvasGo.transform);
            var home = Instantiate<HomeOverlay>("HomeOverlay", canvasGo.transform);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            // App flow.
            var app = new GameObject("App").AddComponent<AppRoot>();
            app.EditorWire(controller,
                           AssetDatabase.LoadAssetAtPath<LevelCatalog>(LevelImporter.CatalogPath),
                           LoadOrCreate<EconomyConfig>(EconomyPath),
                           hud, popup, home, tutorial);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Tanker Jam: Game scene created at " + ScenePath);
        }

        static T Instantiate<T>(string prefabName, Transform parent) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{UiBuilder.Dir}/{prefabName}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            return go.GetComponent<T>();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
