using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace WakpuVR
{
    public sealed class PrototypeGameOver : MonoBehaviour
    {
        [SerializeField] private FearSystem fear;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private Image fadeImage;
        [SerializeField] private Behaviour[] controlsToDisable;
        [SerializeField, Min(0f)] private float fadeDuration = 1f;
        public bool IsGameOver { get; private set; }

        public void Configure(FearSystem fearSystem, PrototypeHUD prototypeHud, Image fade, Behaviour[] controls)
        {
            fear = fearSystem; hud = prototypeHud; fadeImage = fade; controlsToDisable = controls;
        }

        private void Start()
        {
            if (fear != null) fear.FearChanged += OnFearChanged;
        }

        private void OnDestroy()
        {
            if (fear != null) fear.FearChanged -= OnFearChanged;
        }

        private void OnFearChanged(float current, float maximum)
        {
            if (current >= maximum) EndGame();
        }

        public void EndGame()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            fear?.SetFear(fear.MaximumFear);
            if (controlsToDisable != null)
                foreach (Behaviour control in controlsToDisable)
                    if (control != null) control.enabled = false;
            StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            if (fadeImage != null)
            {
                Color color = fadeImage.color;
                for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
                {
                    color.a = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));
                    fadeImage.color = color;
                    yield return null;
                }
                color.a = 1f;
                fadeImage.color = color;
            }
            hud?.ShowGameOver();
        }
    }
}
