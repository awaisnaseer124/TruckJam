using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Every feel number in one place. Started from reference-prototype.html; driving is tuned faster (2026-10-05).</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Game Tuning", fileName = "GameTuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [Header("Driving (uniform: constant speed from start to stop)")]
        public float DriveMaxSpeed = 11f;
        [Tooltip("0 = full speed from the first frame (responsive). >0 ramps up like the web prototype (9).")]
        public float DriveAccel = 0f;
        [Tooltip("0 = constant speed up to an exact stop. >0 eases into the bay: speed = min(vmax, sqrt(2 * decel * remaining)).")]
        public float BrakeDecel = 0f;
        public float LeaveMaxSpeed = 11f;
        [Tooltip("0 = full speed from the first frame.")]
        public float LeaveAccel = 0f;
        [Tooltip("Speed reduction when full (prototype: 0.3). 0 keeps the speed uniform.")]
        [Range(0, 1)] public float LoadedSlowdown = 0f;
        public float HeadingSharpness = 16f;     // heading += delta * min(1, dt * sharpness)
        public float WheelRadius = 0.17f;
        public float ReleaseBayAfter = 1.6f;     // metres into the leave path before the bay frees up

        [Header("Bump (blocked tap)")]
        public float BumpDuration = 0.42f;
        public float BumpExtra = 0.12f;
        public float NoBayBump = 0.12f;

        [Header("VIP lift")]
        public float VipDuration = 1.5f;
        public float VipArcHeight = 3.2f;

        [Header("Pump and filling")]
        public float UnitDuration = 0.34f;
        public float HoseLowerSpeed = 5f;        // extension per second (0..1); prototype 2.4
        public float HoseRaiseSpeed = 3f;
        public float FullHoldTime = 0.35f;       // pause after full before leaving; prototype 0.75

        [Header("Springs")]
        public float SloshStiffness = 55f, SloshDamping = 3f;
        public float PitchStiffness = 160f, PitchDamping = 10f;

        [Header("End check")]
        public float CalmTime = 0.4f;

        [Header("Simulation")]
        public float MaxStep = 0.034f;           // sub-step size for springs and motion
        public float MaxFrameDelta = 0.05f;
    }
}
