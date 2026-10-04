// Menu: Tanker Jam > Setup > Create Game Scene.
// Builds Scenes/Game.unity from code (camera + fitter, sun + lighting rig, game controller, debug HUD)
// and registers it in Build Settings. Re-running replaces the scene.
using TankerJam.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TankerJam.EditorTools
{
    public static class SceneSetup
    {
        const string ScenePath = "Assets/_TankerJam/Scenes/Game.unity";
        const string ConfigPath = "Assets/_TankerJam/Config/GameConfig.asset";
        const string StartLevelPath = "Assets/_TankerJam/Data/Levels/level_005.json";
        const string AudioCatalogPath = "Assets/_TankerJam/Config/AudioCatalog.asset";
        const string ParticleShaderPath = "Assets/_TankerJam/Art/Shaders/Particle.shader";

        [MenuItem("Tanker Jam/Setup/Create Game Scene")]
        public static void CreateGameScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) == null)
            {
                Debug.LogError("Tanker Jam: run 'Tanker Jam/Setup/Build Greybox Assets' first.");
                return;
            }
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

            // Sun.
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var lighting = new GameObject("Lighting").AddComponent<LightingRig>();
            lighting.Sun = sun;
            lighting.Apply(config.Palette);

            // Game.
            var gameGo = new GameObject("Game");
            var controller = gameGo.AddComponent<GameController>();
            controller.EditorWire(config, cam, fitter, lighting, AssetDatabase.LoadAssetAtPath<TextAsset>(StartLevelPath));
            EditorUtility.SetDirty(controller);
            if (controller.Config == null) Debug.LogError("Tanker Jam: GameConfig reference did not stick.");

            var hud = gameGo.AddComponent<DebugHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("game").objectReferenceValue = controller;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // Audio: listener on the camera, cue-driven manager with an (initially empty) recorded-clip catalog.
            camGo.AddComponent<AudioListener>();
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(catalog, AudioCatalogPath);
            }
            var audio = new GameObject("Audio").AddComponent<AudioManager>();
            audio.EditorWire(controller, catalog);
            EditorUtility.SetDirty(audio);

            // Win confetti.
            var confetti = new GameObject("Confetti").AddComponent<ConfettiFx>();
            confetti.EditorWire(controller, AssetDatabase.LoadAssetAtPath<Shader>(ParticleShaderPath));
            EditorUtility.SetDirty(confetti);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Tanker Jam: Game scene created at " + ScenePath);
        }
    }
}
