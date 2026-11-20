
using System.Collections;
using TMPro;
using UnityEngine;

namespace WakpuVR
{
    public class ItemPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TMP_Text popupText;
        [SerializeField] private float duration = 2f;

        private Coroutine routine;

        private void Awake()
        {
            popupPanel.SetActive(false);
        }

        public void Show(string message)
        {
            if (routine != null)
                StopCoroutine(routine);

            popupText.text = message;
            popupPanel.SetActive(true);

            routine = StartCoroutine(HideLater());
        }

        private IEnumerator HideLater()
        {
            yield return new WaitForSeconds(duration);
            popupPanel.SetActive(false);
            routine = null;
        }
    }
}
