using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Gives the TABLET button of the mission screen (top right) the look of the main menu: a navy cut-corner plate with a gold edge and gold Rajdhani letters,
// and the same size as the PAUSE button (120 x 40 on the menu's 800 x 360 grid), in the opposite corner. It only changes how the button looks; it is still
// the same button that opens the tablet. Done when the mission starts (MissionScenarioController calls HudStyle.Apply()), so the scenes themselves are not
// edited. A button that is not there is simply skipped.
public static class HudStyle
{
    public static void Apply()
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.name == "TabletButton")
                StyleTabletButton(button);
            else if (button.GetComponent<InteractionPromptUI>() != null)
                StyleHandButton(button);
        }
    }

    // The hand button: a sharp round plate in the navy and gold of the menu and a clean hand, in place of the small knob picture and the blocky hand that looked
    // blurry when enlarged. The size, the place and the glow are not touched.
    private static void StyleHandButton(Button button)
    {
        Image plate = button.targetGraphic as Image;
        if (plate == null)
            plate = button.GetComponent<Image>();

        if (plate != null)
        {
            plate.sprite = HandButtonArt.Plate();
            plate.type = Image.Type.Simple;
            plate.preserveAspect = true;
            plate.color = Color.white;
        }

        Transform iconTransform = button.transform.Find("HandIcon");
        Image icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;

        if (icon == null)
        {
            foreach (Image candidate in button.GetComponentsInChildren<Image>(true))
            {
                if (candidate != plate)
                {
                    icon = candidate;
                    break;
                }
            }
        }

        if (icon != null)
        {
            icon.sprite = HandButtonArt.Hand();
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
        }
    }

    private static void StyleTabletButton(Button button)
    {
        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();

        if (image == null)
            return;

        // The plate: navy, with the top left and bottom right corners cut, and the gold edge on top of it.
        image.sprite = CartoonArt.MenuChamfer(12f, 0f, 0f, 12f, false);
        image.type = Image.Type.Sliced;
        image.color = MenuStyle.PanelDark;

        if (button.transform.Find("StyleOutline") == null)
        {
            Image edge = UiKit.Pic(button.transform, "StyleOutline", CartoonArt.MenuChamfer(12f, 0f, 0f, 12f, true), MenuStyle.Gold);
            UiKit.Stretch(edge.rectTransform);
            edge.transform.SetAsFirstSibling();   // behind the letters
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            MenuFonts.Use(label, MenuFonts.Label);
            label.fontStyle = FontStyles.Normal;
            label.color = MenuStyle.Gold;
            label.characterSpacing = 8f;
            label.enableAutoSizing = false;
        }

        if (button.GetComponent<TabletButtonSizer>() == null)
            button.gameObject.AddComponent<TabletButtonSizer>().label = label;
    }
}

// Keeps the TABLET button exactly as big as the PAUSE button on the screen, whatever canvas the button lives in (the scene's HUD canvas scales differently
// from the pause canvas): 120 x 40 units of the 800 x 360 grid, 14 from the right edge and 12 from the top.
public class TabletButtonSizer : MonoBehaviour
{
    public TMP_Text label;

    private Button button;
    private CanvasGroup group;
    private RectTransform arrow;
    private TextMeshProUGUI arrowText;

    private void Awake()
    {
        button = GetComponent<Button>();

        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        // The arrow under the button (pointing up at it) and its words: shown when the player has just received the tablet.
        Image triangle = UiKit.Pic(transform, "OpenHint", UiKit.DownTriangleSprite(), MenuStyle.Gold);
        triangle.type = Image.Type.Simple;
        arrow = triangle.rectTransform;
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);

        arrowText = MenuStyle.Text(transform, "OPEN YOUR TABLET", 11f, MenuStyle.Gold, MenuFonts.Label, TextAlignmentOptions.Center, "OpenHintText");
        arrowText.textWrappingMode = TextWrappingModes.NoWrap;
        arrowText.characterSpacing = 6f;

        arrow.gameObject.SetActive(false);
        arrowText.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        RectTransform rect = transform as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (rect == null || canvas == null)
            return;

        canvas = canvas.rootCanvas;
        if (canvas.scaleFactor <= 0f)
            return;

        // The pause canvas scales the 800 x 360 grid to fit the screen (the smaller of the two ratios).
        float pauseScale = Mathf.Min(Screen.width / 800f, Screen.height / 360f);
        float k = pauseScale / canvas.scaleFactor;   // this canvas's units per pause-canvas unit

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-14f * k, -12f * k);
        rect.sizeDelta = new Vector2(120f * k, 40f * k);

        if (label != null)
            label.fontSize = 16f * k;

        // Before the player has the tablet (mission 1) the button is dim and cannot be pressed; after, it is normal.
        bool locked = TabletAccess.Locked;
        if (button != null)
            button.interactable = !locked;

        group.alpha = locked ? 0.35f : 1f;

        // The arrow under the button, bobbing, until the player opens the tablet for the first time.
        bool showArrow = !locked && TabletAccess.ShowArrow && !TabletOverlay.IsOpen;
        if (arrow.gameObject.activeSelf != showArrow)
        {
            arrow.gameObject.SetActive(showArrow);
            arrowText.gameObject.SetActive(showArrow);
        }

        if (showArrow)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 3f * k;

            // The arrow is turned upside down (pointing up at the button), and turning happens round the pivot, so the pivot is its middle: its middle is
            // 17 below the bottom edge of the button, which puts the whole arrow (26 high) just under the button.
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(26f * k, 26f * k);
            arrow.anchoredPosition = new Vector2(0f, -17f * k + bob);

            RectTransform text = arrowText.rectTransform;
            text.anchorMin = text.anchorMax = new Vector2(0.5f, 0f);
            text.pivot = new Vector2(0.5f, 1f);
            text.sizeDelta = new Vector2(190f * k, 16f * k);
            text.anchoredPosition = new Vector2(0f, -32f * k + bob);
            arrowText.fontSize = 11f * k;
        }
    }
}
