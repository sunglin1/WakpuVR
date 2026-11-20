
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("대사 UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("입력")]
    [SerializeField] private InputActionReference nextAction;

    [Header("NPC 대화 거리")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float maxDialogueDistance = 3f;

    [Header("시작 나레이션")]
    [TextArea(2, 4)]
    [SerializeField] private string[] startNarration;
    [SerializeField] private TMP_FontAsset narrationFont;

    [Header("엔딩 나레이션")]
    [TextArea(2, 4)]
    [SerializeField] private string[] endingNarration;

    private NPCDialogue currentNPC;
    private string[] currentLines;
    private int lineIndex;

    private bool isDialogueActive;
    private bool isNarration;

    private TMP_FontAsset defaultNameFont;
    private TMP_FontAsset defaultDialogueFont;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        defaultNameFont = nameText.font;
        defaultDialogueFont = dialogueText.font;

        dialoguePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (nextAction == null) return;

        nextAction.action.performed += OnNextPressed;
        nextAction.action.Enable();
    }

    private void OnDisable()
    {
        if (nextAction == null) return;

        nextAction.action.performed -= OnNextPressed;
        nextAction.action.Disable();
    }

    private void Start()
    {
        PlayStartNarration();
    }

    private void Update()
    {
        if (!isDialogueActive || isNarration)
            return;

        if (currentNPC == null || playerCamera == null)
            return;

        float distance = Vector3.Distance(
            playerCamera.position,
            currentNPC.transform.position
        );

        if (distance > maxDialogueDistance)
        {
            EndDialogue();
        }
    }

    // NPC 대화 시작
    public void StartDialogue(NPCDialogue npc)
    {
        if (isDialogueActive) return;
        if (npc == null || npc.lines == null) return;
        if (npc.lines.Length == 0) return;

        currentNPC = npc;
        currentLines = npc.lines;
        lineIndex = 0;

        isNarration = false;
        isDialogueActive = true;

        nameText.gameObject.SetActive(true);
        nameText.text = npc.npcName;

        nameText.font = npc.dialogueFont != null
            ? npc.dialogueFont : defaultNameFont;

        dialogueText.font = npc.dialogueFont != null
            ? npc.dialogueFont : defaultDialogueFont;

        dialoguePanel.SetActive(true);
        ShowLine();
    }

    // 시작 나레이션
    public void PlayStartNarration()
    {
        StartNarration(startNarration);
    }

    // 엔딩 나레이션
    public void PlayEndingNarration()
    {
        StartNarration(endingNarration);
    }

    private void StartNarration(string[] lines)
    {
        if (lines == null || lines.Length == 0)
            return;

        // 진행 중인 대화 종료
        EndDialogue();

        currentNPC = null;
        currentLines = lines;
        lineIndex = 0;

        isNarration = true;
        isDialogueActive = true;

        // 나레이션은 이름 숨기기
        nameText.gameObject.SetActive(false);

        dialogueText.font = narrationFont != null
            ? narrationFont : defaultDialogueFont;

        dialoguePanel.SetActive(true);
        ShowLine();
    }

    private void OnNextPressed(
        InputAction.CallbackContext context)
    {
        NextLine();
    }

    public void NextLine()
    {
        if (!isDialogueActive) return;

        lineIndex++;

        if (lineIndex >= currentLines.Length)
        {
            EndDialogue();
        }
        else
        {
            ShowLine();
        }
    }

    private void ShowLine()
    {
        dialogueText.text = currentLines[lineIndex];
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        isNarration = false;

        currentNPC = null;
        currentLines = null;
        lineIndex = 0;

        dialoguePanel.SetActive(false);
    }
}
