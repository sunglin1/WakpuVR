using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace WakpuVR
{
    [DisallowMultipleComponent]
    public sealed class InventoryUI : MonoBehaviour
    {
        [Header("Head gesture")]
        [SerializeField] private Transform head;
        [SerializeField, Range(0f, 89f)] private float openDownAngle = 50f;
        [SerializeField, Range(0f, 89f)] private float closeDownAngle = 35f;
        [Header("UI")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform slotTemplate;
        [SerializeField] private Text pageLabel;
        [SerializeField] private Vector3 panelPosition = new Vector3(0f, -0.12f, 0.75f);
        [SerializeField, Min(0.0001f)] private float uiScale = 0.001f;
        [Header("Left hand")]
        [SerializeField] private Transform leftController;
        [SerializeField] private NearFarInteractor leftInteractor;
        [SerializeField] private InputActionReference leftGrip;
        [SerializeField] private InputActionReference leftStore;
        [SerializeField] private LineRenderer leftRay;
        [SerializeField] private GameObject leftCursor;
        [Header("Right hand")]
        [SerializeField] private Transform rightController;
        [SerializeField] private NearFarInteractor rightInteractor;
        [SerializeField] private InputActionReference rightGrip;
        [SerializeField] private InputActionReference rightStore;
        [SerializeField] private LineRenderer rightRay;
        [SerializeField] private GameObject rightCursor;

        private readonly List<InventoryBallItem> stored = new List<InventoryBallItem>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<XRBaseInputInteractor> gameplayInteractors = new List<XRBaseInputInteractor>();
        private readonly List<bool> previousActivate = new List<bool>();
        private XRInteractionManager manager;
        private XRSelectFilterDelegate selectFilter;
        private int page;
        private const int SlotsPerPage = 20;

        public bool IsOpen { get; private set; }
        public int StoredCount => stored.Count;

        private sealed class Slot
        {
            public RectTransform rect;
            public Image image;
            public InventoryBallItem item;
        }

        public void Configure(Transform camera, RectTransform window, RectTransform list, RectTransform template,
            Transform leftHand, NearFarInteractor leftNearFar, InputActionReference leftSelect, InputActionReference leftSave, LineRenderer leftLine, GameObject leftDot,
            Transform rightHand, NearFarInteractor rightNearFar, InputActionReference rightSelect, InputActionReference rightSave, LineRenderer rightLine, GameObject rightDot)
        {
            head = camera;
            panel = window;
            content = list;
            slotTemplate = template;
            pageLabel = window.Find("Page Label")?.GetComponent<Text>();
            leftController = leftHand;
            leftInteractor = leftNearFar;
            leftGrip = leftSelect;
            leftStore = leftSave;
            leftRay = leftLine;
            leftCursor = leftDot;
            rightController = rightHand;
            rightInteractor = rightNearFar;
            rightGrip = rightSelect;
            rightStore = rightSave;
            rightRay = rightLine;
            rightCursor = rightDot;
            ApplyPanelPlacement();
        }

        private void Awake()
        {
            if (pageLabel == null && panel != null) pageLabel = panel.Find("Page Label")?.GetComponent<Text>();
            manager = FindFirstObjectByType<XRInteractionManager>();
            if (head == null) head = Camera.main?.transform;
            if (leftController != null) gameplayInteractors.AddRange(leftController.GetComponentsInChildren<XRBaseInputInteractor>(true));
            if (rightController != null) gameplayInteractors.AddRange(rightController.GetComponentsInChildren<XRBaseInputInteractor>(true));
            foreach (XRBaseInputInteractor interactor in gameplayInteractors) previousActivate.Add(interactor.allowActivate);
            selectFilter = new XRSelectFilterDelegate((interactor, interactable) =>
                !IsOpen || interactor.IsSelecting(interactable));
            if (manager != null) manager.selectFilters.Add(selectFilter);
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (manager != null && selectFilter != null) manager.selectFilters.Remove(selectFilter);
            RestoreGameplayActivate();
        }

        private void Update()
        {
            if (head == null) return;
            float downAngle = Mathf.Asin(Mathf.Clamp(-head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (!IsOpen && downAngle >= openDownAngle) SetOpen(true);
            else if (IsOpen && downAngle <= closeDownAngle) SetOpen(false);

            if (IsOpen)
            {
                if (leftStore?.action != null && leftStore.action.WasPressedThisFrame()) ChangePage(-1);
                if (rightStore?.action != null && rightStore.action.WasPressedThisFrame()) ChangePage(1);
                foreach (Slot slot in slots)
                    if (slot.image != null) slot.image.color = new Color(0.17f, 0.16f, 0.28f, 0.96f);
                UpdatePointer(leftController, leftInteractor, leftGrip, leftRay, leftCursor);
                UpdatePointer(rightController, rightInteractor, rightGrip, rightRay, rightCursor);
            }
            else
            {
                TryStore(leftController, leftStore);
                TryStore(rightController, rightStore);
            }
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (panel != null) panel.gameObject.SetActive(open);
            if (leftRay != null) leftRay.enabled = open;
            if (rightRay != null) rightRay.enabled = open;
            if (leftCursor != null) leftCursor.SetActive(false);
            if (rightCursor != null) rightCursor.SetActive(false);
            for (int i = 0; i < gameplayInteractors.Count; i++)
                if (gameplayInteractors[i] != null) gameplayInteractors[i].allowActivate = !open && previousActivate[i];
        }

        private void RestoreGameplayActivate()
        {
            for (int i = 0; i < gameplayInteractors.Count; i++)
                if (gameplayInteractors[i] != null) gameplayInteractors[i].allowActivate = previousActivate[i];
        }

        private void UpdatePointer(Transform controller, NearFarInteractor interactor, InputActionReference grip, LineRenderer line, GameObject cursor)
        {
            if (controller == null || line == null || panel == null) return;
            Ray ray = new Ray(controller.position, controller.forward);
            Plane plane = new Plane(panel.forward, panel.position);
            bool intersects = plane.Raycast(ray, out float distance) && distance > 0f && distance <= 2f;
            Vector3 hit = intersects ? ray.GetPoint(distance) : ray.GetPoint(1f);
            bool onPanel = intersects && panel.rect.Contains(panel.InverseTransformPoint(hit));
            line.SetPosition(0, controller.position);
            line.SetPosition(1, hit);
            if (cursor != null)
            {
                cursor.SetActive(onPanel);
                if (onPanel) cursor.transform.position = hit;
            }

            Slot hovered = null;
            if (onPanel)
                foreach (Slot slot in slots)
                    if (slot.rect != null && slot.rect.rect.Contains(slot.rect.InverseTransformPoint(hit)))
                    {
                        hovered = slot;
                        break;
                    }
            if (hovered != null) hovered.image.color = new Color(0.36f, 0.25f, 0.58f, 0.97f);
            if (grip?.action != null && grip.action.WasPressedThisFrame() && hovered != null && interactor != null && !HandHasSelection(controller))
                hovered.item.Equip(interactor, controller);
        }

        private bool HandHasSelection(Transform controller)
        {
            foreach (XRBaseInputInteractor interactor in controller.GetComponentsInChildren<XRBaseInputInteractor>(true))
                if (interactor.hasSelection) return true;
            return false;
        }

        private void TryStore(Transform controller, InputActionReference action)
        {
            if (controller == null || action?.action == null || !action.action.WasPressedThisFrame()) return;
            foreach (InventoryBallItem item in FindObjectsByType<InventoryBallItem>(FindObjectsSortMode.None))
                if (item.IsHeldBy(controller))
                {
                    item.Store();
                    return;
                }
        }

        public void Add(InventoryBallItem item)
        {
            if (item == null || stored.Contains(item)) return;
            stored.Add(item);
            Rebuild();
        }

        public void Remove(InventoryBallItem item)
        {
            if (stored.Remove(item)) Rebuild();
        }

        private void Rebuild()
        {
            foreach (Slot slot in slots)
                if (slot.rect != null) Destroy(slot.rect.gameObject);
            slots.Clear();
            if (slotTemplate == null || content == null) return;
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(stored.Count / (float)SlotsPerPage));
            page = Mathf.Clamp(page, 0, totalPages - 1);
            if (pageLabel != null) pageLabel.text = $"{page + 1} / {totalPages}   LEFT / RIGHT PRIMARY: PAGE";
            int end = Mathf.Min(stored.Count, (page + 1) * SlotsPerPage);
            for (int index = page * SlotsPerPage; index < end; index++)
            {
                InventoryBallItem item = stored[index];
                if (item == null) continue;
                RectTransform rect = Instantiate(slotTemplate, content);
                rect.gameObject.SetActive(true);
                rect.name = $"WakpuBall {item.Id.Substring(0, 6)}";
                Text label = rect.GetComponentInChildren<Text>(true);
                if (label != null) label.text = $"WAKPU BALL\n{item.Ball.RemainingUses} / {item.Ball.InitialUses}";
                slots.Add(new Slot { rect = rect, image = rect.GetComponent<Image>(), item = item });
            }
        }

        private void ChangePage(int delta)
        {
            int maxPage = Mathf.Max(0, Mathf.CeilToInt(stored.Count / (float)SlotsPerPage) - 1);
            int next = Mathf.Clamp(page + delta, 0, maxPage);
            if (next == page) return;
            page = next;
            Rebuild();
        }

        private void ApplyPanelPlacement()
        {
            if (panel == null) return;
            panel.localPosition = panelPosition;
            panel.localRotation = Quaternion.identity;
            panel.localScale = Vector3.one * uiScale;
        }

        private void OnValidate()
        {
            closeDownAngle = Mathf.Min(closeDownAngle, openDownAngle - 1f);
            ApplyPanelPlacement();
        }

        public void RefreshPageLabel()
        {
            if (panel != null) pageLabel = panel.Find("Page Label")?.GetComponent<Text>();
        }
    }
}
