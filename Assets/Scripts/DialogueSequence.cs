using System;
using UnityEngine;

namespace TapTapGameJam.UI
{
    [CreateAssetMenu(menuName = "TapTap/Dialogue/Sequence", fileName = "Dialogue")]
    public sealed class DialogueSequence : ScriptableObject
    {
        [Serializable]
        public sealed class Line
        {
            public DialogueCharacter character;
            [TextArea(2, 6)] public string text;
            public Sprite portraitOverride;
            [Tooltip("Optional label for this line's advance button; clicking still advances the dialogue.")]
            public string nextButtonLabel;
            [Tooltip("Tower shown in RewardPanel for this line only. Leave empty to hide the reward.")]
            public Sprite rewardSprite;
        }

        public string dialogueId;
        public Line[] lines = new Line[0];

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(dialogueId) || lines == null || lines.Length == 0) return false;
                foreach (var line in lines)
                    if (line == null || string.IsNullOrWhiteSpace(line.text)) return false;
                return true;
            }
        }
    }
}
