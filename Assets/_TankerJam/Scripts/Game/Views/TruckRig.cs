using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// The contract between a truck prefab (greybox or final art) and TruckView. Prefabs face +Z (cab at +Z),
    /// length along Z, 1 unit = 1 grid cell. Final-art prefabs only need to fill these fields.
    /// </summary>
    public sealed class TruckRig : MonoBehaviour
    {
        [Tooltip("Length in grid cells (2, 3 or 4).")]
        public int Length = 2;

        [Header("Parts")]
        [Tooltip("Pitches and sloshes; everything except the wheels.")]
        public Transform Body;
        [Tooltip("Spin around local X while driving.")]
        public Transform[] Axles;
        [Tooltip("Uses the per-color body material (tinted parts).")]
        public Renderer BodyRenderer;
        public Renderer ShellRenderer;
        public Renderer LiquidRenderer;
        public Renderer DecalRenderer;
        public Collider TapCollider;

        [Header("Tank (local space)")]
        [Tooltip("Local Z of the tank center/hatch; parked so this sits under the hose.")]
        public float TankCenterZ = -0.36f;
        public float TankCenterY = 0.63f;
        public float TankRadius = 0.32f;
        public float TankLength = 1.0f;
        [Tooltip("World height above the ground where the hose nozzle meets the tank.")]
        public float HoseTargetY = 1.15f;
        public float WheelRadius = 0.17f;
    }
}
