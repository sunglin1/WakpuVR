
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class EndingTrigger : MonoBehaviour
{
    [Header("VR Player")]
    [SerializeField] private Transform playerCamera;

    [Header("Ending")]
    [SerializeField] private bool triggerOnce = true;

    private BoxCollider triggerBox;
    private bool hasTriggered;

    private void Awake()
    {
        triggerBox = GetComponent<BoxCollider>();
        triggerBox.isTrigger = true;
    }

    private void Update()
    {
        if (hasTriggered && triggerOnce)
            return;

        if (playerCamera == null)
            return;

        // 카메라의 월드 좌표를 박스의 로컬 좌표로 변환
        Vector3 localPosition =
            transform.InverseTransformPoint(playerCamera.position);

        // BoxCollider의 실제 범위 안에 있는지 확인
        Vector3 halfSize = triggerBox.size * 0.5f;
        Vector3 offset = localPosition - triggerBox.center;

        bool inside =
            Mathf.Abs(offset.x) <= halfSize.x &&
            Mathf.Abs(offset.y) <= halfSize.y &&
            Mathf.Abs(offset.z) <= halfSize.z;

        if (inside)
        {
            Debug.Log("[Ending] 플레이어가 엔딩 구역에 들어왔습니다!");
            PlayEnding();
        }
    }

    private void PlayEnding()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[Ending] DialogueManager.Instance가 null입니다!");
            return;
        }

        hasTriggered = true;

        Debug.Log("[Ending] 엔딩 대사 실행!");
        DialogueManager.Instance.PlayEndingNarration();
    }
}
