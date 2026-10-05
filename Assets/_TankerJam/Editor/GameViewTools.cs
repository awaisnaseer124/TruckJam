// Menu: Tanker Jam > Diagnostics > Game View Portrait 1080x1920 / Capture Game View.
// Portrait preview and screenshots (including overlay UI) for reviews and automation.
// The Game view size API is internal, so it is reached by reflection; failures are logged, never fatal.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class GameViewTools
    {
        public const string CapturePath = "Temp/TankerJamScreen.png";

        [MenuItem("Tanker Jam/Diagnostics/Game View Portrait 1080x1920")]
        public static void SetPortrait() => SetSize(1080, 1920, "Tanker Jam Portrait");

        [MenuItem("Tanker Jam/Diagnostics/Capture Game View")]
        public static void Capture()
        {
            if (System.IO.File.Exists(CapturePath)) System.IO.File.Delete(CapturePath);
            ScreenCapture.CaptureScreenshot(CapturePath);
            EditorApplication.QueuePlayerLoopUpdate();
            Debug.Log("Tanker Jam: capture requested -> " + CapturePath);
        }

        static void SetSize(int width, int height, string label)
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleton.GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
                var groupType = group.GetType();

                int index = -1;
                var getTotal = groupType.GetMethod("GetTotalCount");
                var getSize = groupType.GetMethod("GetGameViewSize");
                int total = (int)getTotal.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    var s = getSize.Invoke(group, new object[] { i });
                    var st = s.GetType();
                    if ((int)st.GetProperty("width").GetValue(s) == width && (int)st.GetProperty("height").GetValue(s) == height) { index = i; break; }
                }
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.Parse(sizeKind, "FixedResolution"), width, height, label });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)getTotal.Invoke(group, null) - 1;
                }

                var gameViewType = asm.GetType("UnityEditor.GameView");
                var view = EditorWindow.GetWindow(gameViewType, false, null, false);
                var select = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                select.Invoke(view, new object[] { index, null });
                Debug.Log($"Tanker Jam: Game view set to {width}x{height}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Tanker Jam: could not set the Game view size (internal API changed?): " + e.Message);
            }
        }
    }
}
