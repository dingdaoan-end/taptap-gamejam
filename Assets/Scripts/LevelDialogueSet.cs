using UnityEngine;

namespace TapTapGameJam.UI
{
    [CreateAssetMenu(menuName = "TapTap/Dialogue/Level Set", fileName = "LevelDialogue")]
    public sealed class LevelDialogueSet : ScriptableObject
    {
        [Range(1, 4)] public int level = 1;
        public DialogueSequence intro;
        public DialogueSequence victory;
        public DialogueSequence defeat;
    }
}
