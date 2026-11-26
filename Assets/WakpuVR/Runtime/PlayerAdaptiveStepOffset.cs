using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace WakpuVR
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerAdaptiveStepOffset : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float climbingStepOffset = 0.32f;
        [SerializeField, Min(0f)] private float levelAndDescendingStepOffset = 0.12f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 0.18f;
        [SerializeField, Min(0f)] private float minimumRise = 0.08f;

        private CharacterController characterController;
        private DynamicMoveProvider moveProvider;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            moveProvider = GetComponentInChildren<DynamicMoveProvider>(true);
        }

        private void Update()
        {
            if (moveProvider == null || !characterController.enabled) return;

            Vector3 motion = moveProvider.transformation.motion;
            motion.y = 0f;
            float stepOffset = levelAndDescendingStepOffset;
            if (motion.sqrMagnitude > 0.000001f)
            {
                Vector3 feet = transform.position + characterController.center;
                feet.y -= characterController.height * 0.5f + characterController.skinWidth;
                Vector3 rayStart = feet + Vector3.up * 0.55f;
                int groundMask = moveProvider.gravityProvider != null
                    ? moveProvider.gravityProvider.sphereCastLayerMask : Physics.DefaultRaycastLayers;
                float currentHeight = feet.y;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit currentGround, 0.9f,
                        groundMask, QueryTriggerInteraction.Ignore) && currentGround.normal.y > 0.5f)
                    currentHeight = currentGround.point.y;
                if (Physics.Raycast(rayStart + motion.normalized * lookAheadDistance, Vector3.down,
                        out RaycastHit aheadGround, 0.9f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    float rise = aheadGround.point.y - currentHeight;
                    if (aheadGround.normal.y > 0.5f && rise > minimumRise && rise <= climbingStepOffset)
                        stepOffset = climbingStepOffset;
                }
            }

            if (!Mathf.Approximately(characterController.stepOffset, stepOffset))
                characterController.stepOffset = stepOffset;
        }
    }
}
