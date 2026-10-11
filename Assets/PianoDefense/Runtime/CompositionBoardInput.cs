using UnityEngine;
using UnityEngine.EventSystems;

namespace TapTapGameJam.PianoDefense
{
    public sealed class CompositionBoardInput : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public PianoDefenseGame Game;
        public RectTransform Hover;
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (Cell(e, out int x, out int y)) Game.SelectOrBuild(x, y);
        }
        bool Cell(PointerEventData e, out int x, out int y)
        {
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local);
            x = Mathf.FloorToInt((local.x - rect.rect.xMin) / rect.rect.width * DefenseSimulation.Width);
            y = Mathf.FloorToInt((rect.rect.yMax - local.y) / rect.rect.height * DefenseSimulation.Height);
            return x >= 0 && x < DefenseSimulation.Width && y >= 0 && y < DefenseSimulation.Height;
        }
        public void OnPointerMove(PointerEventData e)
        {
            if (Hover == null || !Cell(e, out int x, out int y)) return;
            Hover.gameObject.SetActive(!Game.Finished && !Game.Paused);
            var rect = (RectTransform)transform;
            Hover.anchoredPosition = new Vector2(x * rect.rect.width / 12, -y * rect.rect.height / 7);
        }
        public void OnPointerExit(PointerEventData e) { if (Hover != null) Hover.gameObject.SetActive(false); }
    }
}
