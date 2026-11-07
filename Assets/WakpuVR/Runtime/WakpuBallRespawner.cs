using System.Collections;
using UnityEngine;

namespace WakpuVR
{
    /// <summary>Restores a destroyed prototype ball without adding inventory or pooling.</summary>
    public sealed class WakpuBallRespawner : MonoBehaviour
    {
        [SerializeField] private WakpuBall ballPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private WakpuBall currentBall;
        [SerializeField, Min(0f)] private float respawnDelay = 2.5f;

        public void Configure(WakpuBall prefab, Transform point, WakpuBall initialBall)
        {
            ballPrefab = prefab;
            spawnPoint = point;
            currentBall = initialBall;
        }

        private void Start()
        {
            spawnPoint = spawnPoint != null ? spawnPoint : transform;
            if (currentBall == null)
                currentBall = GetComponentInChildren<WakpuBall>();

            if (currentBall != null)
                Bind(currentBall);
            else
                Spawn();
        }

        private void OnDestroy()
        {
            if (currentBall != null)
                currentBall.Depleted -= OnBallDepleted;
        }

        private void Bind(WakpuBall ball)
        {
            currentBall = ball;
            currentBall.Depleted += OnBallDepleted;
        }

        private void OnBallDepleted(WakpuBall ball)
        {
            ball.Depleted -= OnBallDepleted;
            currentBall = null;
            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            Spawn();
        }

        private void Spawn()
        {
            if (ballPrefab == null)
                return;

            WakpuBall ball = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation, transform);
            ball.name = "WakpuBall";
            Bind(ball);
        }
    }
}
