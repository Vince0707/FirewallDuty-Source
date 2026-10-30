
using UnityEngine;
using UnityEngine.EventSystems;

public class TouchLookArea : MonoBehaviour,
    IPointerDownHandler, IDragHandler
{
    [SerializeField] private PlayerMobileController playerController;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Begin looking as soon as the player touches this area.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (playerController == null)
            return;

        playerController.ApplyLookDelta(eventData.delta);
    }
}
