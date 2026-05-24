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
            _volume.isGlobal = true;
            _volume.priority = 100f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "LowVisionProfile (runtime)";

            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Bokeh);
            _dof.focusDistance.Override(0.3f);
            _dof.aperture.Override(maxAperture);
            _dof.focalLength.Override(50f);

            _bloom = profile.Add<Bloom>(true);
            _bloom.intensity.Override(maxBloomIntensity);
            _bloom.threshold.Override(0.8f);
            _bloom.scatter.Override(0.7f);

            _color = profile.Add<ColorAdjustments>(true);
            _color.contrast.Override(minContrast);
            _color.saturation.Override(minSaturation);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(maxVignette);
            _vignette.smoothness.Override(0.4f);

            _volume.sharedProfile = profile;
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
