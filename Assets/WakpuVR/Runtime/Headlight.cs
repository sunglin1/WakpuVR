using UnityEngine;

namespace WakpuVR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class Headlight : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float intensity = 18f;
        [SerializeField, Min(0f)] private float range = 20f;
        [SerializeField, Range(1f, 179f)] private float spotAngle = 50f;
        [SerializeField] private Color lightColor = new Color(1f, 0.95f, 0.82f);
        [SerializeField] private LightShadows shadows = LightShadows.Soft;

        private Light spot;
        public bool IsOn => (spot != null ? spot : GetComponent<Light>()).enabled;

        public void Configure()
        {
            ApplySettings();
            spot.enabled = true;
        }

        private void Awake()
        {
            spot = GetComponent<Light>();
            ApplySettings();
            spot.enabled = true;
        }

        private void OnEnable()
        {
            if (spot == null) spot = GetComponent<Light>();
            spot.enabled = true;
        }

        private void ApplySettings()
        {
            if (spot == null) spot = GetComponent<Light>();
            if (spot == null) return;
            spot.type = LightType.Spot;
            spot.intensity = intensity;
            spot.range = range;
            spot.spotAngle = spotAngle;
            spot.color = lightColor;
            spot.shadows = shadows;
        }

        private void OnValidate()
        {
            ApplySettings();
            if (spot != null) spot.enabled = true;
        }
    }
}
