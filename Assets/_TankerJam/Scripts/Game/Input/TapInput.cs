using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TankerJam.Game
{
    /// <summary>Touch-down / mouse-down raycast against the Trucks layer. Polled once per frame by the controller.</summary>
    public sealed class TapInput
    {
        readonly int mask;
        readonly RaycastHit[] hits = new RaycastHit[4];

        public TapInput(string truckLayer)
        {
            mask = LayerMask.GetMask(truckLayer);
        }

        public bool TryGetTappedTruck(Camera cam, out TruckRig rig)
        {
            rig = null;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            var ray = cam.ScreenPointToRay(pointer.position.ReadValue());
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
            return rig != null;
        }
    }
}
