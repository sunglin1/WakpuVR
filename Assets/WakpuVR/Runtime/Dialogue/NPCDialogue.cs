
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class NPCDialogue : MonoBehaviour
{
    [Header("NPC 정보")]
    public string npcName;

    [Header("NPC 대사")]
    [TextArea(2, 4)]
    public string[] lines;

    [Header("NPC 대사 폰트")]
    public TMP_FontAsset dialogueFont;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (DialogueManager.Instance == null)
            return;

        DialogueManager.Instance.StartDialogue(this);
    }
}