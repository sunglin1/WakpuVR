using System;
using UnityEngine;

namespace WakpuVR
{
    public readonly struct NoiseEvent
    {
        public NoiseEvent(Vector3 position, float radius, GameObject source)
        {
            Position = position;
            Radius = radius;
            Source = source;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public GameObject Source { get; }
    }

    /// <summary>Extension point for future listeners such as monster hearing.</summary>
    public static class NoiseSystem
    {
        public static event Action<NoiseEvent> NoiseGenerated;

        public static void Generate(Vector3 position, float radius, GameObject source)
        {
            NoiseGenerated?.Invoke(new NoiseEvent(position, Mathf.Max(0f, radius), source));
        }
    }
}
