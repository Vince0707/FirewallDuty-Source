using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Bigger controls built from uGUI: seek bar (Android SeekBar), drop-down (Spinner), progress bar and the message box (AlertDialog).
public static class UiWidgets
{
    public static Slider SeekBar(Transform parent, string name, int min, int max, int value, Color track, Color fill, Color thumb, float height)
    {
        RectTransform root = UiKit.New(parent, name);
        UiKit.Size(root, -1f, height);

        Slider slider = root.gameObject.AddComponent<Slider>();

        Image background = UiKit.Solid(root, "Track", track);
        UiKit.Place(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 4f));
        background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        background.rectTransform.sizeDelta = new Vector2(-16f, 4f);

        RectTransform fillArea = UiKit.New(root, "FillArea");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.anchoredPosition = Vector2.zero;
        fillArea.sizeDelta = new Vector2(-16f, 4f);

        Image fillImage = UiKit.Solid(fillArea, "Fill", fill);
        fillImage.rectTransform.anchorMin = new Vector2(0f, 0f);
        fillImage.rectTransform.anchorMax = new Vector2(0f, 1f);
        fillImage.rectTransform.offsetMin = Vector2.zero;
        fillImage.rectTransform.offsetMax = Vector2.zero;

        // The slider stretches the handle over the full height of this area, so the area is exactly as tall as the thumb.
        RectTransform handleArea = UiKit.New(root, "HandleArea");
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-16f, 18f);

        Image handle = UiKit.Pic(handleArea, "Handle", UiKit.Shape("profile_avatar_bg"), thumb, true);
        handle.type = Image.Type.Simple;
        handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(18f, 0f);   // 18 wide, as tall as the area (18)

        slider.fillRect = fillImage.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = value;
        return slider;
    }

    // A drop-down like Android's Spinner: the chosen text, an arrow, and an underline; tapping it lists the options.
    public static RectTransform Spinner(Transform parent, string name, string[] options, int selected, Action<int> onChanged, Color accent, out Action<int> setSelected)
    {
        RectTransform root = UiKit.New(parent, name);
        UiKit.Size(root, -1f, 44f);

        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;

        TextMeshProUGUI label = UiKit.Text(root, options[Mathf.Clamp(selected, 0, options.Length - 1)], 14f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiKit.Stretch(label.rectTransform, 4f, 0f, 30f, 0f);

        TextMeshProUGUI arrow = UiKit.Text(root, "v", 14f, accent, FontStyles.Bold, TextAlignmentOptions.Right);
        UiKit.Stretch(arrow.rectTransform, 0f, 0f, 8f, 0f);

        Image line = UiKit.Solid(root, "Underline", accent);
        line.rectTransform.anchorMin = new Vector2(0f, 0f);
        line.rectTransform.anchorMax = new Vector2(1f, 0f);
        line.rectTransform.pivot = new Vector2(0.5f, 0f);
        line.rectTransform.offsetMin = new Vector2(0f, 2f);
        line.rectTransform.offsetMax = new Vector2(0f, 3f);

        // The list hangs below the spinner, above everything else.
        RectTransform list = UiKit.New(root, "List");
        list.anchorMin = new Vector2(0f, 0f);
        list.anchorMax = new Vector2(1f, 0f);
        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = new Vector2(0f, 2f);
        list.sizeDelta = new Vector2(0f, options.Length * 38f);

        Image listBackground = list.gameObject.AddComponent<Image>();
        listBackground.color = UiKit.Hex("#12304A");

        Canvas canvas = list.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30;
        list.gameObject.AddComponent<GraphicRaycaster>();

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            Button item = UiKit.Btn(list, "Option" + i, null, new Color(0f, 0f, 0f, 0f), options[i], 14f, Color.white, false, -1f, -1f);
            RectTransform itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = new Vector2(0f, -i * 38f);
            itemRect.sizeDelta = new Vector2(0f, 38f);
            item.targetGraphic.raycastTarget = true;

            item.onClick.AddListener(() =>
            {
                label.text = options[index];
                list.gameObject.SetActive(false);
                onChanged?.Invoke(index);
            });
        }

        list.gameObject.SetActive(false);
        button.onClick.AddListener(() => list.gameObject.SetActive(!list.gameObject.activeSelf));

        setSelected = index => label.text = options[Mathf.Clamp(index, 0, options.Length - 1)];
        return root;
    }

    // A bar that fills from the left. Use SetProgress to change it.
    public static Image ProgressBar(Transform parent, string name, float height, Color track, Color fill)
    {
        Image background = UiKit.Solid(parent, name, track);
        UiKit.Size(background, -1f, height);
        background.gameObject.AddComponent<RectMask2D>();

        Image bar = UiKit.Solid(background.transform, "Fill", fill);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.offsetMin = Vector2.zero;
        bar.rectTransform.offsetMax = Vector2.zero;
        return bar;
    }

    public static void SetProgress(Image bar, float fraction)
    {
        bar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    // A text box over the screen, like an AlertDialog with a text field. Returns the dimmed root so the caller can hide it.
    public class InputDialog
    {
        public RectTransform Root;
        public TMP_InputField Input;
        public TMP_Text Error;
        public Button Save;
        public Button Cancel;
    }

    public static InputDialog TextDialog(RectTransform screenRoot, string title, string message, string hint)
    {
        InputDialog dialog = new InputDialog();

        Image dim = UiKit.Solid(screenRoot, "DialogDim", new Color(0f, 0f, 0f, 0.6f));
        UiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        dialog.Root = dim.rectTransform;

        Image box = UiKit.Solid(dim.transform, "Dialog", UiKit.Hex("#102940"));
        UiKit.Center(box.rectTransform, 340f, 190f);
        box.raycastTarget = true;

        TextMeshProUGUI titleText = UiKit.Text(box.transform, title, 18f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiKit.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(300f, 24f));

        TextMeshProUGUI messageText = UiKit.Text(box.transform, message, 12f, UiKit.Hex("#DDDDDD"), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        UiKit.Place(messageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(300f, 18f));

        dialog.Input = UiKit.Field(box.transform, "Input", hint, false, Color.white, Color.black, 14f, -1f, -1f);
        UiKit.Place((RectTransform)dialog.Input.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -72f), new Vector2(300f, 36f));

        dialog.Error = UiKit.Text(box.transform, "", 11f, UiKit.Hex("#FF6B6B"), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        UiKit.Place(dialog.Error.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -110f), new Vector2(300f, 16f));

        dialog.Cancel = UiKit.Btn(box.transform, "Cancel", null, Color.clear, "CANCEL", 14f, UiKit.Hex("#64B5F6"), true, -1f, -1f);
        UiKit.Place((RectTransform)dialog.Cancel.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-110f, 12f), new Vector2(90f, 36f));
        dialog.Cancel.targetGraphic.raycastTarget = true;

        dialog.Save = UiKit.Btn(box.transform, "Save", null, Color.clear, "SAVE", 14f, UiKit.Hex("#64B5F6"), true, -1f, -1f);
        UiKit.Place((RectTransform)dialog.Save.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 12f), new Vector2(90f, 36f));
        dialog.Save.targetGraphic.raycastTarget = true;

        dim.gameObject.SetActive(false);
        return dialog;
    }
}
