using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// A visual theme around the board: a prop layout prefab (decoration only, outside the play area) and the
    /// ground/lot colors it goes with. Swap themes per world later (e.g. every 10 levels) without touching rules.
    /// </summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Environment Theme", fileName = "EnvironmentTheme")]
    public sealed class EnvironmentTheme : ScriptableObject
    {
        [Tooltip("Props around the board. Edit the prefab in the Scene view; it is batched into a few draw calls at runtime.")]
        public GameObject Layout;

        [Header("Colors (override the Palette while this theme is active)")]
        public Color Ground = new Color(0.80f, 0.62f, 0.44f);
        public Color LotLight = new Color(0.95f, 0.89f, 0.78f);
        public Color LotDark = new Color(0.92f, 0.85f, 0.72f);

        [Tooltip("Props cast shadows. Off keeps the triangle budget for the game itself.")]
        public bool CastShadows;
    }
}
