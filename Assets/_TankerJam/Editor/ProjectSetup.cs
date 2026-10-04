// Menu: Tanker Jam > Setup > Apply Mobile Project Settings.
// Applies the player, quality and URP settings from Docs/ImplementationPlan.md §7 so they are reproducible
// and reviewable in code instead of being clicked by hand. Safe to re-run.
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TankerJam.EditorTools
{
    public static class ProjectSetup
    {
        const string MobileUrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        const string MobileQualityName = "Mobile";

        [MenuItem("Tanker Jam/Setup/Apply Mobile Project Settings")]
        public static void Apply()
        {
            ApplyPlayer();
            ApplyQuality();
            ApplyUrp();
            Time.fixedDeltaTime = 0.02f;
            AssetDatabase.SaveAssets();
            Debug.Log("Tanker Jam: mobile project settings applied.");
        }

        static void ApplyPlayer()
        {
            PlayerSettings.productName = "Tanker Jam";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 24)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            if (string.CompareOrdinal(PlayerSettings.iOS.targetOSVersionString, "13.0") < 0)
                PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Medium);
        }

        static void ApplyQuality()
        {
            string[] names = QualitySettings.names;
            int mobile = System.Array.IndexOf(names, MobileQualityName);
            if (mobile < 0)
            {
                Debug.LogWarning($"Tanker Jam: quality level '{MobileQualityName}' not found; skipping quality defaults.");
                return;
            }
            QualitySettings.SetQualityLevel(mobile, true);

            // No public API for per-platform defaults; edit the serialized QualitySettings asset.
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (asset.Length == 0) return;
            var so = new SerializedObject(asset[0]);
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform == null) return;
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                var entry = perPlatform.GetArrayElementAtIndex(i);
                string platform = entry.FindPropertyRelative("first").stringValue;
                if (platform == "Android" || platform == "iPhone")
                    entry.FindPropertyRelative("second").intValue = mobile;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplyUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(MobileUrpAssetPath);
            if (urp == null)
            {
                Debug.LogWarning($"Tanker Jam: URP asset not found at {MobileUrpAssetPath}.");
                return;
            }
            Undo.RecordObject(urp, "Tanker Jam URP settings");
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.renderScale = 1f;
            urp.shadowDistance = 25f;
            urp.shadowCascadeCount = 1;
            urp.mainLightShadowmapResolution = 1024;
            urp.useSRPBatcher = true;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(urp);
        }
    }
}
