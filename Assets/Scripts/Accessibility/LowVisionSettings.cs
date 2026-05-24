using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Accessibility
{
    [RequireComponent(typeof(Volume))]
    public class LowVisionSettings : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float intensity = 1f;

        [Header("Valores máximos (quando intensity = 1)")]
        [SerializeField] private float maxAperture = 32f;
        [SerializeField] private float maxBloomIntensity = 1.5f;
        [SerializeField] private float minContrast = -25f;
        [SerializeField] private float minSaturation = -15f;
        [SerializeField] private float maxVignette = 0.2f;

        private Volume _volume;
        private DepthOfField _dof;
        private Bloom _bloom;
        private ColorAdjustments _color;
        private Vignette _vignette;

        void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.profile.TryGet(out _dof);
            _volume.profile.TryGet(out _bloom);
            _volume.profile.TryGet(out _color);
            _volume.profile.TryGet(out _vignette);
            Apply();
        }

        public void SetIntensity(float t)
        {
            intensity = Mathf.Clamp01(t);
            Apply();
        }

        private void Apply()
        {
            if (_dof != null) _dof.aperture.value = Mathf.Lerp(16f, maxAperture, intensity);
            if (_bloom != null) _bloom.intensity.value = Mathf.Lerp(0f, maxBloomIntensity, intensity);
            if (_color != null)
            {
                _color.contrast.value = Mathf.Lerp(0f, minContrast, intensity);
                _color.saturation.value = Mathf.Lerp(0f, minSaturation, intensity);
            }
            if (_vignette != null) _vignette.intensity.value = Mathf.Lerp(0f, maxVignette, intensity);
        }

        void OnValidate()
        {
            if (Application.isPlaying && _volume != null) Apply();
        }
    }
}
