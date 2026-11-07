using UnityEngine;
using UnityEngine.InputSystem;

namespace WakpuVR
{
    /// <summary>Drives the bundled Animated Hands animator from the rig's existing XRI actions.</summary>
    public sealed class AnimatedHandInputDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private InputActionProperty gripAction;
        [SerializeField] private InputActionProperty triggerAction;
        [SerializeField, Min(0f)] private float responseSpeed = 14f;

        private static readonly int GripParameter = Animator.StringToHash("Grip");
        private static readonly int TriggerParameter = Animator.StringToHash("Trigger");
        private float gripValue;
        private float triggerValue;

        public void Configure(Animator handAnimator, InputActionReference grip, InputActionReference trigger)
        {
            animator = handAnimator;
            gripAction = new InputActionProperty(grip);
            triggerAction = new InputActionProperty(trigger);
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (animator == null)
                return;

            float targetGrip = ReadValue(gripAction);
            float targetTrigger = ReadValue(triggerAction);
            float blend = 1f - Mathf.Exp(-responseSpeed * Time.unscaledDeltaTime);
            gripValue = Mathf.Lerp(gripValue, targetGrip, blend);
            triggerValue = Mathf.Lerp(triggerValue, targetTrigger, blend);
            animator.SetFloat(GripParameter, gripValue);
            animator.SetFloat(TriggerParameter, triggerValue);
        }

        private static float ReadValue(InputActionProperty property)
        {
            InputAction action = property.action;
            return action != null ? Mathf.Clamp01(action.ReadValue<float>()) : 0f;
        }
    }
}
