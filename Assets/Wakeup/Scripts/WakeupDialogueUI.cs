using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace TheLastCompact.Wakeup
{
    public class WakeupDialogueUI : MonoBehaviour
    {
        [Header("UI 引用")]
        public CanvasGroup uiCanvasGroup;
        public TextMeshProUGUI speakerText;
        public TextMeshProUGUI dialogueText;
        public Transform choiceContainer;
        public GameObject choiceButtonPrefab;

        // 选项被选中时的回调
        public System.Action<int> OnChoiceSelected;

        private void Awake()
        {
            if (uiCanvasGroup == null) uiCanvasGroup = GetComponent<CanvasGroup>();
        }

        public void ShowLine(string speaker, string text)
        {
            if (speakerText != null) speakerText.text = speaker;
            if (dialogueText != null) dialogueText.text = text;
            
            // 清除旧选项
            ClearChoices();
        }

        public void ShowChoices(string[] choices)
        {
            ClearChoices();
            
            if (choiceButtonPrefab == null)
            {
                Debug.LogError("Choice Button Prefab is not assigned in WakeupDialogueUI!");
                return;
            }

            for (int i = 0; i < choices.Length; i++)
            {
                int index = i; // 闭包捕获
                GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
                TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = choices[i];

                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnChoiceSelected?.Invoke(index));
                }
            }
        }

        private void ClearChoices()
        {
            foreach (Transform child in choiceContainer)
            {
                Destroy(child.gameObject);
            }
        }

        public void FadeIn(float duration)
        {
            StartCoroutine(FadeCanvasGroup(0, 1, duration));
        }

        public void FadeOut(float duration)
        {
            StartCoroutine(FadeCanvasGroup(1, 0, duration));
        }

        private IEnumerator FadeCanvasGroup(float start, float end, float duration)
        {
            float elapsed = 0f;
            if (uiCanvasGroup != null)
            {
                uiCanvasGroup.alpha = start;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    uiCanvasGroup.alpha = Mathf.Lerp(start, end, elapsed / duration);
                    yield return null;
                }
                uiCanvasGroup.alpha = end;
            }
        }
    }
}
