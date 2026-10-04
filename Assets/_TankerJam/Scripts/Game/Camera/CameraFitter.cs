using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// Perspective camera looking down at a fixed pitch toward the bays (−z). Moves back along its view axis
    /// until the whole board fits the screen in both width and height, inside the area not covered by HUD
    /// insets. Re-fits when the screen size changes. Replaces the prototype's resize().
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Tooltip("Viewport fraction reserved at the top for the HUD (top bar).")]
        [Range(0, 0.4f)] public float TopInset = 0.08f;
        [Tooltip("Viewport fraction reserved at the bottom for the HUD (booster bar).")]
        [Range(0, 0.4f)] public float BottomInset = 0.12f;

        Camera cam;
        BoardLayout layout;
        LayoutConfig config;
        int lastW, lastH;
        readonly Vector3[] corners = new Vector3[8];

        void Awake() => cam = GetComponent<Camera>();

        public void Fit(BoardLayout boardLayout, LayoutConfig layoutConfig)
        {
            layout = boardLayout;
            config = layoutConfig;
            Refit();
        }

        void LateUpdate()
        {
            if (layout != null && (Screen.width != lastW || Screen.height != lastH)) Refit();
        }

        void Refit()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            cam.fieldOfView = config.FieldOfView;

            layout.GroundBounds(out float minX, out float maxX, out float minZ, out float maxZ);
            float m = config.FitMargin;
            minX -= m; maxX += m; minZ -= m; maxZ += m;
            float top = layout.TopY;
            int i = 0;
            foreach (float x in new[] { minX, maxX })
                foreach (float y in new[] { 0f, top })
                    foreach (float z in new[] { minZ, maxZ })
                        corners[i++] = new Vector3(x, y, z);

            var rotation = Quaternion.Euler(config.Pitch, 180f, 0f);
            var back = rotation * Vector3.back;
            var target = new Vector3((minX + maxX) / 2f, 0.6f, (minZ + maxZ) / 2f);
            transform.rotation = rotation;

            // Binary search the smallest distance where every corner is inside the usable viewport.
            float lo = 1f, hi = 200f;
            for (int iter = 0; iter < 40; iter++)
            {
                float mid = (lo + hi) / 2f;
                transform.position = target + back * mid;
                if (AllInside()) hi = mid; else lo = mid;
            }
            transform.position = target + back * hi;

            // Center the board vertically in the usable band (between the HUD insets).
            float usableCenter = (BottomInset + (1f - TopInset)) / 2f;
            float minV = float.MaxValue, maxV = float.MinValue;
            foreach (var c in corners)
            {
                float v = cam.WorldToViewportPoint(c).y;
                minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }
            float shift = usableCenter - (minV + maxV) / 2f;
            float dist = hi;
            float worldPerViewport = 2f * dist * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2f);
            transform.position -= transform.up * shift * worldPerViewport;
        }

        bool AllInside()
        {
            foreach (var c in corners)
            {
                var v = cam.WorldToViewportPoint(c);
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < BottomInset || v.y > 1f - TopInset) return false;
            }
            return true;
        }
    }
}
