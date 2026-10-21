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
        [SerializeField] Sprite restartButtonSprite;
        Sprite continueButtonSprite;
        Sprite skipButtonSprite;
        public GameObject rewardPanel;
        public Image rewardTower;
        public bool IsPlaying => current != null;
        public int LineIndex { get; private set; }
        public DialogueSequence Current => current;
        public event Action<bool> PlayingChanged;
        DialogueSequence current;
        Action<DialogueSequence> completed;
        Action declined;
        TMP_Text skipLabel;
        string defaultSkipLabel;
        string completionLabel;
        bool keepOpenOnDecline;
        int lastAdvanceFrame = -1;

        void OnEnable()
        {
            if (rewardPanel == null && panel != null)
                rewardPanel = panel.transform.Find("RewardPanel")?.gameObject;
            if (rewardTower == null && rewardPanel != null)
                rewardTower = rewardPanel.transform.Find("Tower")?.GetComponent<Image>();
            ShowReward(null);
            if (continueButtonSprite == null && nextButton != null && nextButton.image != null)
                continueButtonSprite = nextButton.image.sprite;
            if (skipButton == null && panel != null)
                skipButton = panel.transform.Find("DialogueBox/SkipButton")?.GetComponent<Button>();
            if (skipButton != null)
            {
                if (skipButtonSprite == null && skipButton.image != null) skipButtonSprite = skipButton.image.sprite;
                skipLabel = skipButton.GetComponentInChildren<TMP_Text>(true);
                if (skipLabel != null) defaultSkipLabel = skipLabel.text;
            }
            if (nextButton != null) nextButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }
        void OnDisable()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(Advance);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            Cancel();
        }

        public bool Play(DialogueSequence sequence, Action<DialogueSequence> onCompleted, Action onDeclined = null,
            string completionButtonLabel = null, bool keepDialogueOnDecline = false)
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
            declined = onDeclined;
            completionLabel = completionButtonLabel;
            keepOpenOnDecline = keepDialogueOnDecline;
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
            if (declined != null)
            {
                // Skipping exposition must not silently accept a retry decision.
                if (LineIndex + 1 < current.lines.Length)
                {
                    LineIndex = current.lines.Length - 1;
                    ShowLine();
                    return;
                }
                var callback = declined;
                if (!keepOpenOnDecline) Cancel();
                callback();
                return;
            }
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
            ShowReward(line.rewardSprite);
            if (portrait != null)
            {
                portrait.sprite = line.portraitOverride != null ? line.portraitOverride :
                    line.character != null ? line.character.portrait : null;
                portrait.enabled = portrait.sprite != null;
            }
            bool lastLine = LineIndex + 1 == current.lines.Length;
            bool choosing = lastLine && declined != null;
            if (nextLabel != null) nextLabel.text = !string.IsNullOrWhiteSpace(line.nextButtonLabel)
                ? line.nextButtonLabel
                : lastLine ? completionLabel ?? (choosing ? "是" : "结束对话") : "继续";
            if (skipLabel != null) skipLabel.text = choosing ? "否" : defaultSkipLabel;
            // The new artwork contains lettering. Keep authored choices readable without overprinting it.
            if (restartButtonSprite != null)
            {
                bool customNext = !string.IsNullOrWhiteSpace(line.nextButtonLabel);
                bool retry = lastLine && completionLabel == "重新开始" && !customNext;
                bool textNext = customNext || (lastLine && !retry && (choosing || !string.IsNullOrEmpty(completionLabel)));
                SetButtonArtwork(nextButton, nextLabel, retry ? restartButtonSprite : continueButtonSprite, textNext);
                SetButtonArtwork(skipButton, skipLabel, skipButtonSprite, choosing);
            }
        }

        static void SetButtonArtwork(Button button, TMP_Text label, Sprite sprite, bool showText)
        {
            if (button == null || button.image == null) return;
            button.image.sprite = sprite;
            // Keep the Image enabled so its original rectangular click target remains available.
            button.image.color = showText ? new Color(1, 1, 1, 0) : Color.white;
            if (label != null) label.enabled = showText;
        }

        public void Cancel()
        {
            bool wasPlaying = IsPlaying;
            current = null;
            completed = null;
            declined = null;
            completionLabel = null;
            keepOpenOnDecline = false;
            LineIndex = 0;
            ShowReward(null);
            if (portrait != null)
            {
                portrait.enabled = false;
                portrait.sprite = null;
            }
            if (skipLabel != null) skipLabel.text = defaultSkipLabel;
            if (EventSystem.current != null && panel != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(panel.transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (panel != null) panel.SetActive(false);
            if (wasPlaying) PlayingChanged?.Invoke(false);
        }

        void ShowReward(Sprite sprite)
        {
            if (rewardTower != null)
            {
                rewardTower.sprite = sprite;
                rewardTower.preserveAspect = true;
                rewardTower.enabled = sprite != null;
            }
            if (rewardPanel != null) rewardPanel.SetActive(sprite != null && rewardTower != null);
        }
    }
}
