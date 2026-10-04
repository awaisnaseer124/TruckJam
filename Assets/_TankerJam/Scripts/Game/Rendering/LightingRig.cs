using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// Applies the prototype's lighting: hemisphere ambient (sky white, ground #A8B6C8, 0.72) as shader
    /// globals read by the Tanker Jam shaders, and one shadow-casting directional "sun".
    /// </summary>
    public sealed class LightingRig : MonoBehaviour
    {
        static readonly int SkyId = Shader.PropertyToID("_TJ_SkyColor");
        static readonly int GroundId = Shader.PropertyToID("_TJ_GroundColor");
        static readonly int HemiId = Shader.PropertyToID("_TJ_HemiIntensity");

        public Light Sun;
        [Tooltip("Where the sun sits relative to the board (prototype: -5, 20, 8).")]
        public Vector3 SunPosition = new Vector3(-5f, 20f, 8f);

        public void Apply(Palette palette)
        {
            Shader.SetGlobalColor(SkyId, palette.HemiSky);
            Shader.SetGlobalColor(GroundId, palette.HemiGround);
            Shader.SetGlobalFloat(HemiId, palette.HemiIntensity);

            if (Sun != null)
            {
                Sun.type = LightType.Directional;
                Sun.color = Color.white;
                Sun.intensity = palette.SunIntensity;
                Sun.shadows = LightShadows.Soft;
                Sun.transform.rotation = Quaternion.LookRotation(-SunPosition.normalized, Vector3.up);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black; // ambient comes from the hemisphere globals
        }
    }
}
