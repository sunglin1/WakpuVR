
using UnityEngine;

namespace WakpuVR
{
    public class MainGateKeyUse : MonoBehaviour
    {
        [Header("정문")]
        [SerializeField] private DoorController doorController;

        [Header("거리")]
        [SerializeField] private Transform playerHead;
        [SerializeField] private Transform gateCenter;
        [SerializeField] private float useDistance = 3f;

        [Header("팝업")]
        [SerializeField] private ItemPopup popup;

        private bool opened;

        public bool TryUseKey()
        {
            if (opened) return false;

            if (playerHead == null || gateCenter == null ||
                doorController == null)
            {
                Debug.LogWarning("정문 참조가 연결되지 않았습니다.");
                return false;
            }

            Vector3 player = playerHead.position;
            Vector3 gate = gateCenter.position;

            // 플레이어와 정문의 수평 거리
            player.y = 0f;
            gate.y = 0f;

            float distance = Vector3.Distance(player, gate);

            if (distance > useDistance)
            {
                popup?.Show("정문 근처에서 사용할 수 있습니다.");
                return false;
            }

            opened = true;
            doorController.OpenDoor();

            popup?.Show("정문이 열렸습니다!");
            return true;
        }
    }
}
