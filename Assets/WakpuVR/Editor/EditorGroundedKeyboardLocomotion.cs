#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace WakpuVR.Editor
{
    [InitializeOnLoad]
    internal static class EditorGroundedKeyboardLocomotion
    {
        private const string EnabledKey = "WakpuVR.EditorGroundedKeyboardLocomotion";
        private const string ScenePath = "Assets/Scenes/Wakpu_Map.unity";
        private const string MenuPath = "Tools/WakpuVR/Editor Grounded WASD";

        static EditorGroundedKeyboardLocomotion()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RefreshMenuCheck;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(EnabledKey, !EditorPrefs.GetBool(EnabledKey, true));
            RefreshMenuCheck();
            if (EditorApplication.isPlaying) AttachOrRemove();
        }

        private static void RefreshMenuCheck() => Menu.SetChecked(MenuPath, EditorPrefs.GetBool(EnabledKey, true));

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) AttachOrRemove();
        }

        private static void AttachOrRemove()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) return;
            var bridge = origin.GetComponent<GroundedKeyboardBridge>();
            if (SceneManager.GetActiveScene().path != ScenePath || !EditorPrefs.GetBool(EnabledKey, true) || HasRunningXRDisplay())
            {
                if (bridge != null) Object.Destroy(bridge);
                return;
            }

            var move = origin.GetComponentInChildren<DynamicMoveProvider>(true);
            var simulator = Object.FindFirstObjectByType<XRInteractionSimulator>();
            if (move == null || simulator == null || !simulator.isActiveAndEnabled) return;
            if (bridge == null) bridge = origin.gameObject.AddComponent<GroundedKeyboardBridge>();
            bridge.Initialize(move, simulator);
        }

        internal static bool HasRunningXRDisplay()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            return displays.Exists(display => display.running);
        }
    }

    [DefaultExecutionOrder(-100)]
    internal sealed class GroundedKeyboardBridge : MonoBehaviour, IXRInputValueReader<Vector2>
    {
        private XRInputValueReader<Vector2> moveReader;
        private XRInteractionSimulator simulator;
        private IXRInputValueReader<Vector2> previousBypass;
        private XRInputValueReader.InputSourceMode previousX;
        private XRInputValueReader.InputSourceMode previousY;
        private XRInputValueReader.InputSourceMode previousZ;
        private Vector2 keyboardMove;
        private float nextDisplayCheck;

        internal void Initialize(DynamicMoveProvider move, XRInteractionSimulator inputSimulator)
        {
            if (moveReader != null) return;
            moveReader = move.leftHandMoveInput;
            previousBypass = moveReader.bypass;
            moveReader.bypass = this;
            simulator = inputSimulator;
            previousX = simulator.translateXInput.inputSourceMode;
            previousY = simulator.translateYInput.inputSourceMode;
            previousZ = simulator.translateZInput.inputSourceMode;
            simulator.translateXInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            simulator.translateYInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            simulator.translateZInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextDisplayCheck)
            {
                nextDisplayCheck = Time.unscaledTime + 1f;
                if (EditorGroundedKeyboardLocomotion.HasRunningXRDisplay())
                {
                    Destroy(this);
                    return;
                }
            }
            var keyboard = Keyboard.current;
            bool gameFocused = EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.GetType().Name == "GameView";
            if (keyboard == null || !gameFocused)
            {
                keyboardMove = Vector2.zero;
                return;
            }
            keyboardMove = Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)), 1f);
        }

        public Vector2 ReadValue()
        {
            Vector2 original = previousBypass != null ? previousBypass.ReadValue() : moveReader.ReadValue();
            return Vector2.ClampMagnitude(original + keyboardMove, 1f);
        }

        public bool TryReadValue(out Vector2 value)
        {
            bool read = previousBypass != null ? previousBypass.TryReadValue(out value) : moveReader.TryReadValue(out value);
            value = Vector2.ClampMagnitude(value + keyboardMove, 1f);
            return read || keyboardMove != Vector2.zero;
        }

        private void OnDestroy()
        {
            if (moveReader != null && ReferenceEquals(moveReader.bypass, this)) moveReader.bypass = previousBypass;
            if (simulator == null) return;
            simulator.translateXInput.inputSourceMode = previousX;
            simulator.translateYInput.inputSourceMode = previousY;
            simulator.translateZInput.inputSourceMode = previousZ;
        }
    }
}
#endif
