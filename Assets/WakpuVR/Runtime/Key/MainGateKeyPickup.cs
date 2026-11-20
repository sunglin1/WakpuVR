
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WakpuVR
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class MainGateKeyPickup : MonoBehaviour
    {
        [SerializeField] private InventoryUI inventory;

        private XRSimpleInteractable interactable;
        private bool collected;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();

            if (inventory == null)
                inventory = FindFirstObjectByType<InventoryUI>();
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
            if (collected || inventory == null) return;

            if (!inventory.AcquireMainGateKey()) return;

            collected = true;
            gameObject.SetActive(false);
        }
    }
}
