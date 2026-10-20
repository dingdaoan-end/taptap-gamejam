using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    // Lives on a persistent UI controller, outside the panel it hides.
    public sealed class DialoguePlayer : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text speakerName;
        public TMP_Text content;
        public Image portrait;
        public Button nextButton;
        public Button skipButton;
        public TMP_Text nextLabel;
        public bool IsPlaying => current != null;
        public int LineIndex { get; private set; }
        public DialogueSequence Current => current;
        public event Action<bool> PlayingChanged;
        DialogueSequence current;
        Action<DialogueSequence> completed;
        int lastAdvanceFrame = -1;

        void OnEnable()
        {
            if (skipButton == null && panel != null)
                skipButton = panel.transform.Find("DialogueBox/SkipButton")?.GetComponent<Button>();
            if (nextButton != null) nextButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }
        void OnDisable()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Advance);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            Cancel();
        }

        public bool Play(DialogueSequence sequence, Action<DialogueSequence> onCompleted)
        {
            if (!isActiveAndEnabled || sequence == null || !sequence.IsValid || panel == null ||
                speakerName == null || content == null || nextButton == null)
            {
                Debug.LogError("Dialogue cannot start: assign a valid sequence and all required UI references.", this);
                return false;
            }
            Cancel();
            current = sequence;
            completed = onCompleted;
            LineIndex = 0;
            lastAdvanceFrame = Time.frameCount;
            panel.SetActive(true);
            var group = panel.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
            ShowLine();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(nextButton.gameObject);
            PlayingChanged?.Invoke(true);
            return true;
        }

        public void Advance()
        {
            if (!isActiveAndEnabled || !IsPlaying || !panel.activeInHierarchy || lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;
            if (LineIndex + 1 < current.lines.Length) { LineIndex++; ShowLine(); return; }
            Complete();
        }

        public void Skip()
        {
            if (!isActiveAndEnabled || !IsPlaying || !panel.activeInHierarchy || lastAdvanceFrame == Time.frameCount) return;
            lastAdvanceFrame = Time.frameCount;
            Complete();
        }

        void Complete()
        {
            var finished = current;
            var callback = completed;
            Cancel(); // Clear state before notifying; repeated clicks cannot complete twice.
            callback?.Invoke(finished);
        }

        void ShowLine()
        {
            var line = current.lines[LineIndex];
            speakerName.text = line.character != null ? line.character.displayName : string.Empty;
            content.text = line.text;
            if (portrait != null)
            {
                portrait.sprite = line.portraitOverride != null ? line.portraitOverride :
                    line.character != null ? line.character.portrait : null;
                portrait.enabled = portrait.sprite != null;
            }
            if (nextLabel != null) nextLabel.text = LineIndex + 1 == current.lines.Length ? "结束对话" : "继续";
        }

        public void Cancel()
        {
            bool wasPlaying = IsPlaying;
            current = null;
            completed = null;
            LineIndex = 0;
            if (EventSystem.current != null && panel != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(panel.transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (panel != null) panel.SetActive(false);
            if (wasPlaying) PlayingChanged?.Invoke(false);
        }
    }
}
