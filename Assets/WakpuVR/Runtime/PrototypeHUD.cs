using UnityEngine;
using UnityEngine.UI;

namespace WakpuVR
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        [SerializeField] private FearSystem fearSystem;
        [SerializeField] private Slider fearSlider;
        [SerializeField] private Text fearText;
        [SerializeField] private Text ballText;
        [SerializeField] private Text hintText;
        [SerializeField] private Image fearFill;
        [SerializeField] private Text gameOverText;

        private WakpuBall heldBall;

        public void ConfigureGameOver(Image fill, Text label)
        {
            fearFill = fill;
            gameOverText = label;
            if (gameOverText != null) gameOverText.gameObject.SetActive(false);
        }

        public void ShowGameOver()
        {
            if (gameOverText != null) gameOverText.gameObject.SetActive(true);
        }

        public void Configure(FearSystem fear, Slider slider, Text fearLabel, Text ballLabel, Text hintLabel)
        {
            fearSystem = fear;
            fearSlider = slider;
            fearText = fearLabel;
            ballText = ballLabel;
            hintText = hintLabel;
        }

        private void OnEnable()
        {
            WakpuBall.HeldBallChanged += OnHeldBallChanged;
        }

        private void Start()
        {
            if (fearSystem == null)
                fearSystem = FindFirstObjectByType<FearSystem>();

            if (fearSystem != null)
            {
                fearSystem.FearChanged += RefreshFear;
                RefreshFear(fearSystem.CurrentFear, fearSystem.MaximumFear);
            }

            RefreshBall();
        }

        private void OnDisable()
        {
            WakpuBall.HeldBallChanged -= OnHeldBallChanged;
            if (fearSystem != null)
                fearSystem.FearChanged -= RefreshFear;
            UnsubscribeFromBall();
        }

        private void OnHeldBallChanged(WakpuBall ball)
        {
            UnsubscribeFromBall();
            heldBall = ball;
            if (heldBall != null)
                heldBall.UsesChanged += OnUsesChanged;
            RefreshBall();
        }

        private void OnUsesChanged(WakpuBall ball)
        {
            RefreshBall();
        }

        private void UnsubscribeFromBall()
        {
            if (heldBall != null)
                heldBall.UsesChanged -= OnUsesChanged;
            heldBall = null;
        }

        private void RefreshFear(float current, float maximum)
        {
            if (fearSlider != null)
            {
                fearSlider.minValue = 0f;
                fearSlider.maxValue = maximum;
                fearSlider.value = current;
            }

            if (fearText != null)
                fearText.text = $"FEAR  {Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}";
            if (fearFill != null)
                fearFill.color = Color.Lerp(new Color(0.82f, 0.15f, 0.32f), Color.red, current / maximum);
        }

        private void RefreshBall()
        {
            if (ballText != null)
            {
                ballText.text = heldBall == null
                    ? "WAKPU BALL  --"
                    : $"WAKPU BALL  Uses: {heldBall.RemainingUses} / {heldBall.InitialUses}";
            }

            if (hintText != null)
                hintText.text = heldBall == null ? "Grab a Wakpu Ball" : "Press Activate / Trigger to squeeze";
        }
    }
}
