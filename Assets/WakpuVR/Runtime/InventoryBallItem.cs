using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace WakpuVR
{
    // Keeps the original WakpuBall instance (and its use count) while it is hidden in the UI inventory.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WakpuBall), typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class InventoryBallItem : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private Rigidbody body;
        private WakpuBall ball;
        private InventoryUI inventory;
        private bool storing;
        private bool equippedFromInventory;

        public string Id { get; private set; }
        public WakpuBall Ball => ball;
        public bool IsStored { get; private set; }

        private void Awake()
        {
            Id = Guid.NewGuid().ToString("N");
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            ball = GetComponent<WakpuBall>();
            inventory = FindFirstObjectByType<InventoryUI>();
        }

        private void OnEnable()
        {
            grab.selectExited.AddListener(OnReleased);
            ball.Depleted += OnDepleted;
        }

        private void OnDisable()
        {
            grab.selectExited.RemoveListener(OnReleased);
            ball.Depleted -= OnDepleted;
        }

        public bool IsHeldBy(Transform controller)
        {
            if (!grab.isSelected || controller == null) return false;
            foreach (IXRSelectInteractor interactor in grab.interactorsSelecting)
                if (interactor.transform.IsChildOf(controller)) return true;
            return false;
        }

        public bool Store()
        {
            if (IsStored || !grab.isSelected || inventory == null || ball.RemainingUses <= 0) return false;
            storing = true;
            grab.throwOnDetach = false;
            IXRSelectInteractor[] selecting = new IXRSelectInteractor[grab.interactorsSelecting.Count];
            grab.interactorsSelecting.CopyTo(selecting);
            foreach (IXRSelectInteractor interactor in selecting)
                grab.interactionManager.SelectExit(interactor, grab);
            storing = false;
            equippedFromInventory = false;
            IsStored = true;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = false;
            inventory.Add(this);
            gameObject.SetActive(false);
            return true;
        }

        public bool Equip(IXRSelectInteractor interactor, Transform hand)
        {
            if (!IsStored || interactor == null || hand == null || inventory == null) return false;
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(hand.position + hand.forward * 0.07f, hand.rotation);
            // The UI has already checked Grip and hand occupancy. The ball is outside the
            // interactor's range, so use XRI's explicit selection entry for this transfer.
            grab.interactionManager.SelectEnterUnconditionally(interactor, grab);
            if (!grab.isSelected)
            {
                gameObject.SetActive(false);
                return false;
            }
            IsStored = false;
            equippedFromInventory = true;
            inventory.Remove(this);
            return true;
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            if (storing || !equippedFromInventory || ball.RemainingUses <= 0) return;
            equippedFromInventory = false;
            StartCoroutine(ReturnAfterRelease());
        }

        private IEnumerator ReturnAfterRelease()
        {
            yield return null;
            if (inventory == null || grab.isSelected || ball.RemainingUses <= 0) yield break;
            IsStored = true;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = false;
            inventory.Add(this);
            gameObject.SetActive(false);
        }

        private void OnDepleted(WakpuBall value)
        {
            equippedFromInventory = false;
            IsStored = false;
            inventory?.Remove(this);
        }
    }
}
