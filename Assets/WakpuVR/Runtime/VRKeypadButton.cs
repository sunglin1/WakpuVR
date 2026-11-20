
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using NavKeypad;

[RequireComponent(typeof(XRSimpleInteractable))]
public class VRKeypadButton : MonoBehaviour
{
    private KeypadButton keypadButton;
    private XRSimpleInteractable interactable;

    private void Awake()
    {
        keypadButton = GetComponent<KeypadButton>();
        interactable = GetComponent<XRSimpleInteractable>();

        if (keypadButton == null)
            Debug.LogError(
                $"{name}: KeypadButton 컴포넌트가 없습니다!"
            );
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHover);
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHover);
        interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnHover(HoverEnterEventArgs args)
    {
        Debug.Log($"[Keypad] {name} 가리키기 성공");
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        Debug.Log($"[Keypad] {name} 선택 성공");

        if (keypadButton != null)
        {
            keypadButton.PressButton();
            Debug.Log("[Keypad] PressButton 호출 완료");
        }
    }
}
