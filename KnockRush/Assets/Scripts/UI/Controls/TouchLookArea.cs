using KnockRush.Player.Controls;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KnockRush.UI.Controls
{
    /// <summary>
    /// Invisible full-height area on the right of the screen: dragging a finger here rotates
    /// the camera. Buttons placed above it in the hierarchy take priority. Each finger is
    /// tracked separately, so steering with the joystick and looking work at the same time.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int activePointerId = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId == int.MinValue)
            {
                activePointerId = eventData.pointerId;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            var reader = PlayerInputReader.Local;
            if (reader != null)
            {
                reader.AddTouchLook(eventData.delta);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                activePointerId = int.MinValue;
            }
        }

        private void OnDisable()
        {
            activePointerId = int.MinValue;
        }
    }
}
