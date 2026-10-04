using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Every feel number from the web prototype in one place. Defaults match reference-prototype.html.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Game Tuning", fileName = "GameTuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [Header("Driving")]
        public float DriveMaxSpeed = 6.5f;
        public float DriveAccel = 9f;
        public float BrakeDecel = 7f;            // speed = min(vmax, sqrt(2 * decel * remaining))
        public float LeaveMaxSpeed = 5.5f;
        public float LeaveAccel = 5f;
        [Range(0, 1)] public float LoadedSlowdown = 0.3f;
        public float HeadingSharpness = 12f;     // heading += delta * min(1, dt * sharpness)
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
        public float HoseLowerSpeed = 2.4f;      // extension per second (0..1)
        public float HoseRaiseSpeed = 3f;
        public float FullHoldTime = 0.75f;       // pause after full before leaving

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
