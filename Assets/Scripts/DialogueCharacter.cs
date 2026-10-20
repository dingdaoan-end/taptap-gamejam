using UnityEngine;

namespace TapTapGameJam.UI
{
    [CreateAssetMenu(menuName = "TapTap/Dialogue/Character", fileName = "Character")]
    public sealed class DialogueCharacter : ScriptableObject
    {
        public string characterId;
        public string displayName;
        public Sprite portrait;
    }
}
