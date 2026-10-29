using UnityEngine;

using UnityEngine.EventSystems;

namespace BackpackDefense
{
    /// <summary>
    /// Starts board placement when an occupied bench slot is dragged.
    /// </summary>
    public sealed class BenchDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private BoardView _view;
        private int _index;

        /// <summary>
        /// Connects this drag handler to the board and its displayed bench slot.
        /// </summary>
        public void Initialize(BoardView view, int index)
        {
            _view = view;
            _index = index;
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _view.BeginBenchDrag(_index);
        }

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _view.EndBenchDrag(eventData.position);
        }
    }
}
