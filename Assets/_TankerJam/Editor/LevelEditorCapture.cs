// Menu: Tanker Jam > Diagnostics > Capture Level Editor. Opens the level editor on the reference mandala
// sample, selects one truck (to show its road out and blockers) and saves a screenshot of the window to
// Temp/TankerJamEditor.png for reviews and automation.
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class LevelEditorCapture
    {
        const string CapturePath = "Temp/TankerJamEditor.png";

        [MenuItem("Tanker Jam/Diagnostics/Capture Level Editor")]
        static void Capture()
        {
            var w = LevelEditorWindow.Open();
            w.position = new Rect(80, 80, 1180, 820);
            w.LoadSample("mandala");
            w.SelectTruck(0);
            w.Focus();
            w.Repaint();
            int frames = 0;
            void Tick()
            {
                if (++frames < 10) { w.Repaint(); return; }
                EditorApplication.update -= Tick;
                float ppp = EditorGUIUtility.pixelsPerPoint;
                var r = w.position;
                int width = Mathf.RoundToInt(r.width * ppp), height = Mathf.RoundToInt(r.height * ppp);
                var pixels = InternalEditorUtility.ReadScreenPixel(r.position * ppp, width, height);
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.SetPixels(pixels);
                tex.Apply();
                System.IO.File.WriteAllBytes(CapturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log("Tanker Jam: level editor captured -> " + CapturePath);
            }
            EditorApplication.update += Tick;
        }
    }
}
