using System;
using UnityEngine;

namespace WakpuVR
{
    /// <summary>Owns the prototype's bounded fear value.</summary>
    public sealed class FearSystem : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumFear = 100f;
        [SerializeField, Min(0f)] private float initialFear = 50f;
        [SerializeField, Min(0f)] private float baseIncreasePerSecond = 1f;
        [SerializeField, Min(0f)] private float chaseIncreasePerSecond = 2f;
        [SerializeField] private GhostAI ghost;
        [SerializeField] private PrototypeGameOver gameOver;

        public event Action<float, float> FearChanged;

        public float CurrentFear { get; private set; }
        public float MaximumFear => maximumFear;

        private void Awake()
        {
            maximumFear = Mathf.Max(1f, maximumFear);
            CurrentFear = Mathf.Clamp(initialFear, 0f, maximumFear);
        }

        private void Start()
        {
            FearChanged?.Invoke(CurrentFear, maximumFear);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || (gameOver != null && gameOver.IsGameOver))
                return;
            float rate = baseIncreasePerSecond + (ghost != null && ghost.IsChasing ? chaseIncreasePerSecond : 0f);
            SetFear(CurrentFear + rate * Time.deltaTime);
        }

        public void Configure(GhostAI ghostAI, PrototypeGameOver gameState)
        {
            ghost = ghostAI;
            gameOver = gameState;
        }

        public void ReduceFear(float amount)
        {
            if (amount <= 0f)
                return;

            SetFear(CurrentFear - amount);
        }

        public void SetFear(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, maximumFear);
            if (Mathf.Approximately(CurrentFear, clamped))
                return;

            CurrentFear = clamped;
            FearChanged?.Invoke(CurrentFear, maximumFear);
        }
    }
}
