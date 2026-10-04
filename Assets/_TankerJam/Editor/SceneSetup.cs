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

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Tanker Jam: Game scene created at " + ScenePath);
        }
    }
}
