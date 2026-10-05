using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TankerJam.Game
{
    /// <summary>
    /// Touch-down / mouse-down raycast against the Trucks layer. Polled once per frame by the controller.
    /// Dense free-form boards use small trucks, so a miss falls back to the nearest lot truck whose
    /// on-screen center line is within a finger radius of the tap.
    /// </summary>
    public sealed class TapInput
    {
        readonly int mask;
        readonly RaycastHit[] hits = new RaycastHit[4];

        public TapInput(string truckLayer)
        {
            mask = LayerMask.GetMask(truckLayer);
        }

        /// <summary>Finger radius for the fallback pick, as a share of screen height.</summary>
        public const float ToleranceOfScreenHeight = 0.035f;

        public bool TryGetTappedTruck(Camera cam, IReadOnlyList<TruckView> lotTrucks, out TruckRig rig)
        {
            rig = null;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            Vector2 screen = pointer.position.ReadValue();
            var ray = cam.ScreenPointToRay(screen);
            int n = Physics.RaycastNonAlloc(ray, hits, 200f, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance >= best) continue;
                var r = hits[i].collider.GetComponent<TruckRig>();
                if (r == null) continue;
                best = hits[i].distance;
                rig = r;
            }
            if (rig == null) rig = Nearest(cam, screen, lotTrucks);
            return rig != null;
        }

        /// <summary>The lot truck closest to <paramref name="screen"/> (distance to its projected front-back segment), within tolerance.</summary>
        public static TruckRig Nearest(Camera cam, Vector2 screen, IReadOnlyList<TruckView> trucks)
        {
            float best = ToleranceOfScreenHeight * cam.pixelHeight;
            best *= best;
            TruckRig pick = null;
            for (int i = 0; i < trucks.Count; i++)
            {
                var t = trucks[i];
                if (t.State != TruckState.Lot || t.Def == null) continue;
                var tr = t.Transform;
                var half = tr.forward * ((t.Def.Len - 0.1f) * 0.5f);
                Vector2 a = cam.WorldToScreenPoint(tr.position + half);
                Vector2 b = cam.WorldToScreenPoint(tr.position - half);
                float d = SegmentDistanceSq(screen, a, b);
                if (d < best) { best = d; pick = t.Rig; }
            }
            return pick;
        }

        static float SegmentDistanceSq(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;
            float s = len > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return (a + ab * s - p).sqrMagnitude;
        }
    }
}
