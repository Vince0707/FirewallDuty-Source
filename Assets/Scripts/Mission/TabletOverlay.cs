using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The Cyber Tablet opened by the player during a mission (the TABLET button). It is the same tablet as the one in the menu
// (TabletView); BACK closes it and gives the player their movement back. The mission timer keeps running.
public static class TabletOverlay
{
    private static GameObject current;
    private static PlayerMobileController lockedPlayer;
    private static bool previousLock;

    public static bool IsOpen => current != null;

    public static void Open()
    {
        if (current != null || TabletAccess.Locked)
            return;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        current = new GameObject("TabletOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = current.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = current.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // No picture behind the tablet: the office the player is standing in shows through, only a little darker, as if they were holding the tablet up
        // in front of them.
        RectTransform root = (RectTransform)current.transform;
        Image shade = UiKit.Solid(root, "Shade", UiKit.Hex("#59000000"));
        UiKit.Stretch(shade.rectTransform);
        shade.raycastTarget = true;   // touches must not reach the game behind

        RectTransform frame = UiLayout.Frame(root, TabletView.DesignWidth, TabletView.DesignHeight);

        lockedPlayer = Object.FindAnyObjectByType<PlayerMobileController>();
        if (lockedPlayer != null)
        {
            previousLock = lockedPlayer.MovementLocked;   // already locked when seated
            lockedPlayer.MovementLocked = true;
        }

        new TabletView().Build(frame, Close, null);
        TabletAccess.Opened();   // the arrow under the TABLET button has done its job
    }

    public static void Close()
    {
        if (lockedPlayer != null)
            lockedPlayer.MovementLocked = previousLock;

        lockedPlayer = null;

        if (current != null)
            Object.Destroy(current);

        current = null;
    }
}
