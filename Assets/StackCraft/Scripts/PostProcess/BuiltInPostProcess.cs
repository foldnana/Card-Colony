using UnityEngine;
using DG.Tweening;

namespace CryingSnow.StackCraft
{
    [ExecuteInEditMode]
    public class BuiltInPostProcess : MonoBehaviour
    {
        [SerializeField, Tooltip("Material that applies the post-processing shader during rendering.")]
        private Material effectMaterial;

        [SerializeField, Range(0f, 1f), Tooltip("Current vignette intensity (0 = disabled, 1 = full).")]
        private float vignetteIntensity = 0f;

        [SerializeField, Range(0f, 1f), Tooltip("Current grayscale intensity (0 = color, 1 = grayscale).")]
        private float grayscaleIntensity = 0f;

        private Tweener vignetteTween;
        private Tweener grayscaleTween;

        private void OnValidate()
        {
            Initialize();
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimePaceChanged += HandleTimePaceChanged;
                TimeManager.Instance.OnDayEnded += HandleDayEnded;
                TimeManager.Instance.OnDayStarted += HandleDayStarted;
            }

            Initialize();
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnTimePaceChanged -= HandleTimePaceChanged;
                TimeManager.Instance.OnDayEnded -= HandleDayEnded;
                TimeManager.Instance.OnDayStarted -= HandleDayStarted;
            }
        }

        private void Initialize()
        {
            if (effectMaterial != null)
            {
                effectMaterial.SetFloat("_VignettePower", vignetteIntensity);
                effectMaterial.SetFloat("_GrayscaleAmount", grayscaleIntensity);
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (effectMaterial != null)
            {
                Graphics.Blit(source, destination, effectMaterial);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }

        private void HandleTimePaceChanged(TimePace pace)
        {
            if (pace == TimePace.Paused) FadeInGrayscale();
            else FadeOutGrayscale();
        }

        private void HandleDayEnded(int _)
        {
            FadeInVignette();
        }

        private void HandleDayStarted(int _)
        {
            FadeOutVignette();
        }

        private void FadeInGrayscale(float duration = 0.3f, float target = 1f)
        {
            grayscaleTween?.Kill();
            grayscaleTween = DOTween.To(
                () => grayscaleIntensity,
                x => { grayscaleIntensity = x; effectMaterial.SetFloat("_GrayscaleAmount", x); },
                target,
                duration
            ).SetUpdate(true);
        }

        private void FadeOutGrayscale(float duration = 0.3f)
        {
            grayscaleTween?.Kill();
            grayscaleTween = DOTween.To(
                () => grayscaleIntensity,
                x => { grayscaleIntensity = x; effectMaterial.SetFloat("_GrayscaleAmount", x); },
                0f,
                duration
            ).SetUpdate(true);
        }

        private void FadeInVignette(float duration = 0.5f, float target = 1.2f)
        {
            vignetteTween?.Kill();
            vignetteTween = DOTween.To(
                () => vignetteIntensity,
                x => { vignetteIntensity = x; effectMaterial.SetFloat("_VignettePower", x); },
                target,
                duration
            ).SetUpdate(true);
        }

        private void FadeOutVignette(float duration = 0.5f)
        {
            vignetteTween?.Kill();
            vignetteTween = DOTween.To(
                () => vignetteIntensity,
                x => { vignetteIntensity = x; effectMaterial.SetFloat("_VignettePower", x); },
                0f,
                duration
            ).SetUpdate(true);
        }
    }
}
