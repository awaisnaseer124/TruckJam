using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Editable wrapper around Core's LayoutParams (board spacing), plus camera framing.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Layout Config", fileName = "LayoutConfig")]
    public sealed class LayoutConfig : ScriptableObject
    {
        public LayoutParams Board = new LayoutParams();

        [Header("Camera")]
        public float FieldOfView = 36f;
        [Range(20, 89)] public float Pitch = 53f;
        [Tooltip("Extra world-space margin around the board when fitting the camera.")]
        public float FitMargin = 0.4f;
    }
}
