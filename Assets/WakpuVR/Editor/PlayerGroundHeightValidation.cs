#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace WakpuVR.Editor
{
    [InitializeOnLoad]
    public static class PlayerGroundHeightValidation
    {
        private const string ActiveKey = "WakpuVR.PlayerGroundHeightValidation";
        private static readonly Vector3[] Stairs =
        {
            new Vector3(445.35f, 0.05f, 655.75f),
            new Vector3(445.35f, 0.05f, 654.75f),
            new Vector3(445.35f, 1.00f, 653.46f),
            new Vector3(445.35f, 1.97f, 652.18f),
            new Vector3(445.35f, 1.97f, 651.60f),
            new Vector3(444.00f, 1.97f, 651.60f),
            new Vector3(444.00f, 1.97f, 652.18f),
            new Vector3(444.00f, 2.50f, 653.10f),
            new Vector3(444.00f, 3.03f, 654.02f),
            new Vector3(444.00f, 3.03f, 654.55f),
            new Vector3(445.35f, 3.03f, 654.55f),
            new Vector3(445.35f, 3.03f, 653.77f),
            new Vector3(445.35f, 3.82f, 652.49f),
            new Vector3(445.35f, 4.61f, 651.20f)
        };

        private static XROrigin origin;
        private static CharacterController controller;
        private static GravityProvider gravity;
        private static DynamicMoveProvider move;
        private static XRInputValueReader<Vector2> reader;
        private static Transform heading;
        private static int waypoint;
        private static int direction;
        private static float started;
        private static float lastProgress;
        private static float lastDistance;
        private static float startOriginY;
        private static float startCameraY;
        private static float highestOriginY;
        private static float highestCameraY;
        private static int startFrame;
        private static float summedDelta;
        private static int appliedFrames;
        private static float previousZ;
        private static float forwardDistance;
        private static float backwardDistance;
        private static PlayerGroundCollisionLogger collisionLogger;

        static PlayerGroundHeightValidation()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += AttachDriver;
        }

        public static void Begin()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Wakpu_Map.unity");
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.update -= AttachDriver;
            EditorApplication.update += AttachDriver;
            EditorApplication.isPlaying = true;
        }

        private static void AttachDriver()
        {
            if (!EditorApplication.isPlaying) return;
            var rig = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (rig == null) return;
            rig.gameObject.AddComponent<PlayerGroundHeightDriver>();
            EditorApplication.update -= AttachDriver;
        }

        internal static void Tick()
        {
            summedDelta += Time.deltaTime;
            try
            {
                if (origin == null) Initialize();
                var shift = origin.transform.position.z - previousZ;
                if (shift < 0f) forwardDistance -= shift;
                else backwardDistance += shift;
                previousZ = origin.transform.position.z;
                if (Time.time - started > 65f) throw new InvalidOperationException("Stair traversal timed out.");

                highestOriginY = Mathf.Max(highestOriginY, origin.transform.position.y);
                highestCameraY = Mathf.Max(highestCameraY, origin.Camera.transform.position.y);
                Vector3 current = origin.transform.position;
                Vector3 goal = Stairs[waypoint];
                Vector3 horizontal = goal - current;
                horizontal.y = 0f;
                float distance = horizontal.magnitude;
                if (distance < lastDistance - 0.02f) lastProgress = Time.time;
                if (Time.time - lastProgress > 5f)
                {
                    Debug.Log(DescribeClearance());
                    var inAir = typeof(UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider)
                        .GetField("m_InAirVelocity", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(move);
                    var directFlags = controller.Move(Vector3.forward * 0.25f);
                    Physics.SphereCast(origin.Camera.transform.position, gravity.sphereCastRadius, Vector3.down, out var groundHit, 2f, gravity.sphereCastLayerMask);
                    throw new InvalidOperationException($"Player stalled at waypoint {waypoint}: origin={current}, direct={origin.transform.position - current}/{directFlags}, camera={origin.Camera.transform.position}, grounded={controller.isGrounded}/{gravity.isGrounded}, gravityHit={groundHit.point}/{groundHit.normal}/{groundHit.collider?.name}, contact={collisionLogger.lastHit}, capsule={controller.height:F2}/{controller.center}/{controller.stepOffset:F2}, input={reader.ReadValue()}/{move.rightHandMoveInput.ReadValue()}, move={move.locomotionState}/{move.moveSpeed:F2}, motionZ={move.transformation.motion.z:F8}, distance={forwardDistance:F2}/{backwardDistance:F2}, inAir={inAir}, moveGravity={move.gravityProvider?.isGrounded}, heading={heading.forward}/{move.forwardSource?.forward}, transformer={move.mediator.bodyTransformer.isActiveAndEnabled}/{move.mediator.bodyTransformer.useCharacterControllerIfExists}/{appliedFrames}, scale={origin.transform.localScale}, frames={startFrame}->{Time.frameCount}, dt={Time.deltaTime:F6}/{summedDelta:F2}, floor={DescribeFloor(current)}, forward={DescribeForward(current)}.");
                }
                lastDistance = distance;

                if (distance <= 0.38f && Mathf.Abs(current.y - goal.y) < 0.55f)
                {
                    Debug.Log($"PLAYER_STAIR_POINT {waypoint} originY={current.y:F2} cameraY={origin.Camera.transform.position.y:F2} grounded={controller.isGrounded}/{gravity.isGrounded}");
                    if (direction > 0 && waypoint == Stairs.Length - 1)
                    {
                        direction = -1;
                        waypoint--;
                    }
                    else if (direction < 0 && waypoint == 0)
                    {
                        reader.manualValue = Vector2.zero;
                        if (highestOriginY - startOriginY < 3.5f || highestCameraY - startCameraY < 3.5f ||
                            Mathf.Abs(current.y - startOriginY) > 0.55f || !gravity.isGrounded)
                            throw new InvalidOperationException($"Height validation failed: origin {startOriginY:F2}->{highestOriginY:F2}->{current.y:F2}, camera {startCameraY:F2}->{highestCameraY:F2}->{origin.Camera.transform.position.y:F2}, grounded={controller.isGrounded}.");
                        Debug.Log($"PLAYER_GROUND_HEIGHT_VALID: origin {startOriginY:F2}->{highestOriginY:F2}->{current.y:F2}, camera {startCameraY:F2}->{highestCameraY:F2}->{origin.Camera.transform.position.y:F2}, all stair points both directions.");
                        Finish(0);
                        return;
                    }
                    else waypoint += direction;
                    lastProgress = Time.time;
                    lastDistance = float.PositiveInfinity;
                    goal = Stairs[waypoint];
                    horizontal = goal - current;
                    horizontal.y = 0f;
                }

                heading.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                reader.manualValue = Vector2.up;
            }
            catch (Exception exception)
            {
                Debug.LogError("PLAYER_GROUND_HEIGHT_FAILED: " + exception);
                Finish(1);
            }
        }

        private static void Initialize()
        {
            origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            controller = origin.GetComponent<CharacterController>();
            move = origin.GetComponentInChildren<DynamicMoveProvider>(true);
            gravity = origin.GetComponentInChildren<GravityProvider>(true);
            if (controller == null || move == null || gravity == null || !controller.enabled || !move.isActiveAndEnabled || !gravity.isActiveAndEnabled)
                throw new InvalidOperationException("The existing XR locomotion components are unavailable.");
            if (origin.GetComponent<PlayerAdaptiveStepOffset>() == null ||
                !Mathf.Approximately(controller.stepOffset, 0.12f) ||
                !Mathf.Approximately(controller.skinWidth, 0.03f))
                throw new InvalidOperationException("Saved player collision setup is missing or has changed.");
            var simulator = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator>();
            if (simulator == null || origin.GetComponent<GroundedKeyboardBridge>() == null ||
                simulator.translateXInput.inputSourceMode != XRInputValueReader.InputSourceMode.Unused ||
                simulator.translateYInput.inputSourceMode != XRInputValueReader.InputSourceMode.Unused ||
                simulator.translateZInput.inputSourceMode != XRInputValueReader.InputSourceMode.Unused)
                throw new InvalidOperationException("Editor grounded keyboard input was not attached.");
            if (simulator != null) simulator.enabled = false;
            controller.enabled = false;
            origin.transform.position = Stairs[0];
            controller.enabled = true;
            Application.targetFrameRate = 90;
            collisionLogger = origin.gameObject.AddComponent<PlayerGroundCollisionLogger>();
            heading = new GameObject("Temporary Player Stair Heading").transform;
            move.leftHandMovementDirection = DynamicMoveProvider.MovementDirection.HandRelative;
            move.leftControllerTransform = heading;
            move.moveSpeed = 1.5f;
            reader = move.leftHandMoveInput;
            reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            reader.manualValue = Vector2.zero;
            waypoint = 1;
            direction = 1;
            started = Time.time;
            startFrame = Time.frameCount;
            summedDelta = 0f;
            appliedFrames = 0;
            move.mediator.bodyTransformer.afterApplyTransformations += _ => appliedFrames++;
            previousZ = origin.transform.position.z;
            forwardDistance = 0f;
            backwardDistance = 0f;
            lastProgress = Time.time;
            lastDistance = float.PositiveInfinity;
            startOriginY = origin.transform.position.y;
            startCameraY = origin.Camera.transform.position.y;
            highestOriginY = startOriginY;
            highestCameraY = startCameraY;
            Time.timeScale = 1f;
            Debug.Log($"PLAYER_GROUND_HEIGHT_START: origin={origin.transform.position}, camera={origin.Camera.transform.position}, controller step={controller.stepOffset}, slope={controller.slopeLimit}, skin={controller.skinWidth}, radius={controller.radius}.");
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= AttachDriver;
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(code);
        }

        private static string DescribeFloor(Vector3 position)
        {
            var result = string.Empty;
            foreach (float ahead in new[] { 0f, -0.15f, -0.3f, -0.5f, -0.75f })
            {
                var ray = new Ray(new Vector3(position.x, 5f, position.z + ahead), Vector3.down);
                result += Physics.Raycast(ray, out var hit, 5f) ? $" {ahead:F2}:{hit.point.y:F2}/{hit.collider.name}" : $" {ahead:F2}:none";
            }
            return result;
        }

        private static string DescribeForward(Vector3 position)
        {
            var result = string.Empty;
            foreach (float height in new[] { 0.15f, 0.35f, 0.55f, 0.9f, 1.4f })
            {
                var ray = new Ray(position + Vector3.up * height, Vector3.forward);
                result += Physics.Raycast(ray, out var hit, 1f) ? $" {height:F2}:{hit.distance:F2}/{hit.point.y:F2}/{hit.collider.name}" : $" {height:F2}:none";
            }
            return result;
        }

        private static string DescribeClearance()
        {
            var result = "PLAYER_CLEARANCE";
            foreach (float x in new[] { 444.85f, 445.05f, 445.25f, 445.45f, 445.65f, 445.85f })
            {
                result += $"\n{x:F2}:";
                foreach (float z in new[] { 651.6f, 651.8f, 652.0f, 652.2f, 652.4f, 652.6f, 652.8f })
                {
                    var ray = new Ray(new Vector3(x, 2.05f, z), Vector3.up);
                    result += Physics.Raycast(ray, out var hit, 2f) ? $" {z:F1}={hit.point.y:F2}" : $" {z:F1}=open";
                }
            }
            return result;
        }
    }

    internal sealed class PlayerGroundHeightDriver : MonoBehaviour
    {
        private void Update() => PlayerGroundHeightValidation.Tick();
    }

    internal sealed class PlayerGroundCollisionLogger : MonoBehaviour
    {
        internal string lastHit;
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            lastHit = $"{hit.collider.name} at {hit.point}, normal {hit.normal}, motion {hit.moveDirection}";
        }
    }
}
#endif
