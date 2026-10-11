using UnityEngine;

namespace TapTapGameJam.PianoDefense
{
    // Its Transform is the saved tower position; the game reads this object on every reset.
    [ExecuteAlways]
    public sealed class PianoTowerAuthoring : MonoBehaviour
    {
        [SerializeField, Range(0, 7)] int pitch;
        [SerializeField] TargetMode mode;
        [SerializeField] TowerRhythm rhythm;
        [SerializeField, Range(0, 3)] int lesson;
        public int Pitch { get { return pitch; } }
        public TargetMode Mode { get { return mode; } }
        public TowerRhythm Rhythm { get { return rhythm; } }
        public int Lesson { get { return lesson; } }

        void OnValidate() { pitch = Mathf.Clamp(pitch, 0, 7); }

        void Update()
        {
            if (Application.isPlaying || !transform.hasChanged) return;
            if (GetComponentInParent<PianoBoardScene>() == null) return;
            int x = Mathf.RoundToInt(transform.localPosition.x + (DefenseSimulation.Width - 1) * 0.5f);
            int y = Mathf.RoundToInt((DefenseSimulation.Height - 1) * 0.5f - transform.localPosition.y);
            transform.localPosition = PianoBoardScene.CellPosition(x, y, -0.18f);
            transform.hasChanged = false;
        }
    }
}
