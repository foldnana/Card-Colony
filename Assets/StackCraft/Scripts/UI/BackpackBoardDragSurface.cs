using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackBoardDragSurface : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private BackpackBoardView board;

        internal void Bind(BackpackBoardView boardView)
        {
            board = boardView;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                board?.BeginSurfaceDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                board?.DragSurface(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                board?.EndSurfaceDrag();
        }
    }
}
