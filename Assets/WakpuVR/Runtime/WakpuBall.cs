using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WakpuVR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody), typeof(Collider))]
    public sealed class WakpuBall : MonoBehaviour
    {
        [Header("Uses")]
        [SerializeField, Min(1)] private int minimumUses = 3;
        [SerializeField, Min(1)] private int maximumUses = 5;

        [Header("Gameplay")]
        [SerializeField, Min(0f)] private float fearReductionPerUse = 10f;
        [SerializeField, Min(0f)] private float noiseRadius = 6f;
        [SerializeField] private FearSystem fearSystem;

        [Header("Feedback")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer ballRenderer;
        [SerializeField] private GameObject[] damageStages;
        [SerializeField] private ParticleSystem crumbleParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip useSound;
        [SerializeField, Range(0.5f, 1f)] private float finalScaleMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float squeezeDuration = 0.13f;
        [SerializeField, Range(0.01f, 0.4f)] private float squeezeAmount = 0.14f;
        [SerializeField, Min(0f)] private float destroyDelay = 0.18f;

        private XRGrabInteractable grabInteractable;
        private Vector3 initialVisualScale;
        private Color initialColor = Color.white;
        private Coroutine feedbackRoutine;
        private bool depleted;

        public static event Action<WakpuBall> HeldBallChanged;
        public event Action<WakpuBall> UsesChanged;
        public event Action<WakpuBall> Used;
        public event Action<WakpuBall> Depleted;

        public int RemainingUses { get; private set; }
        public int InitialUses { get; private set; }
        public float NoiseRadius => noiseRadius;
        public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            visualRoot = visualRoot != null ? visualRoot : transform;
            ballRenderer = ballRenderer != null ? ballRenderer : GetComponentInChildren<Renderer>();
            audioSource = audioSource != null ? audioSource : GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.playOnAwake = false;
            }

            if (fearSystem == null)
                fearSystem = FindFirstObjectByType<FearSystem>();

            maximumUses = Mathf.Max(minimumUses, maximumUses);
            InitialUses = UnityEngine.Random.Range(minimumUses, maximumUses + 1);
            RemainingUses = InitialUses;
            initialVisualScale = visualRoot.localScale;

            if (ballRenderer != null && ballRenderer.sharedMaterial != null)
                initialColor = ballRenderer.sharedMaterial.color;

            SetDamageStage(0);
        }

        private void OnEnable()
        {
            grabInteractable.activated.AddListener(OnActivated);
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void Start()
        {
            UsesChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            if (grabInteractable == null)
                return;

            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        private void OnActivated(ActivateEventArgs args)
        {
            TryUse();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            HeldBallChanged?.Invoke(this);
            UsesChanged?.Invoke(this);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            HeldBallChanged?.Invoke(null);
        }

        public bool TryUse()
        {
            if (depleted || RemainingUses <= 0 || !IsHeld)
                return false;

            RemainingUses--;
            fearSystem?.ReduceFear(fearReductionPerUse);
            NoiseSystem.Generate(transform.position, noiseRadius, gameObject);
            UpdateDamageStage();
            PlaySound();
            PlayFeedback();
            Used?.Invoke(this);
            UsesChanged?.Invoke(this);

            if (RemainingUses == 0)
            {
                depleted = true;
                grabInteractable.enabled = false;
                HeldBallChanged?.Invoke(null);
                Depleted?.Invoke(this);
                Destroy(gameObject, Mathf.Max(destroyDelay, squeezeDuration));
            }

            return true;
        }

        private void PlaySound()
        {
            AudioClip clip = useSound != null ? useSound : ProceduralCrunchClip.Value;
            audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(clip);
        }

        private void PlayFeedback()
        {
            if (crumbleParticles != null)
                crumbleParticles.Play();

            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);
            feedbackRoutine = StartCoroutine(SqueezeFeedback());
        }

        private IEnumerator SqueezeFeedback()
        {
            float damage = 1f - (float)RemainingUses / InitialUses;
            float damageScale = Mathf.Lerp(1f, finalScaleMultiplier, damage);
            Vector3 settledScale = initialVisualScale * damageScale;
            Vector3 squeezedScale = Vector3.Scale(settledScale, new Vector3(1f + squeezeAmount, 1f - squeezeAmount, 1f + squeezeAmount));

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer stageRenderer in renderers)
            {
                Color damagedColor = Color.Lerp(initialColor, new Color(0.18f, 0.06f, 0.22f, 1f), damage);
                foreach (Material material in stageRenderer.materials)
                {
                    if (material.name.Contains("Interior"))
                        continue;
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", damagedColor);
                    else if (material.HasProperty("_Color"))
                        material.color = damagedColor;
                }
            }

            float halfDuration = squeezeDuration * 0.5f;
            for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
            {
                visualRoot.localScale = Vector3.Lerp(settledScale, squeezedScale, elapsed / halfDuration);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
            {
                visualRoot.localScale = Vector3.Lerp(squeezedScale, settledScale, elapsed / halfDuration);
                yield return null;
            }

            visualRoot.localScale = settledScale;
            feedbackRoutine = null;
        }

        private void UpdateDamageStage()
        {
            if (damageStages == null || damageStages.Length == 0)
                return;

            float damage = 1f - (float)RemainingUses / InitialUses;
            int stage = Mathf.Clamp(Mathf.CeilToInt(damage * (damageStages.Length - 1)), 0, damageStages.Length - 1);
            SetDamageStage(stage);
        }

        private void SetDamageStage(int activeStage)
        {
            if (damageStages == null)
                return;

            for (int i = 0; i < damageStages.Length; i++)
            {
                if (damageStages[i] != null)
                    damageStages[i].SetActive(i == activeStage);
            }
        }

        private static class ProceduralCrunchClip
        {
            private static AudioClip clip;

            public static AudioClip Value
            {
                get
                {
                    if (clip == null)
                        clip = Create();
                    return clip;
                }
            }

            private static AudioClip Create()
            {
                const int sampleRate = 22050;
                const float duration = 0.12f;
                int sampleCount = Mathf.CeilToInt(sampleRate * duration);
                float[] samples = new float[sampleCount];
                var random = new System.Random(7421);

                for (int i = 0; i < sampleCount; i++)
                {
                    float time = (float)i / sampleRate;
                    float envelope = Mathf.Pow(1f - i / (float)sampleCount, 2f);
                    float tone = Mathf.Sin(2f * Mathf.PI * (150f - 500f * time) * time);
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                    samples[i] = (tone * 0.32f + noise * 0.2f) * envelope;
                }

                AudioClip generated = AudioClip.Create("WakpuBall Procedural Crunch", sampleCount, 1, sampleRate, false);
                generated.SetData(samples, 0);
                return generated;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, noiseRadius);
        }
    }
}
