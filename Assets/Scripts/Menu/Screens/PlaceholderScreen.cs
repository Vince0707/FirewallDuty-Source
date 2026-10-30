using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Stands in for a screen that has not been rebuilt in Unity yet, so the rest of the flow can still be tried.
public class PlaceholderScreen : MenuScreen
{
    private readonly string title;

    public PlaceholderScreen(string title)
    {
        this.title = title;
    }

    protected override void Build(RectTransform root)
    {
        Image bg = UiKit.Pic(root, "Background", UiKit.Shape("signup_bg"));
        UiKit.Stretch(bg.rectTransform);

        RectTransform column = UiKit.VBox(root, "Column", 10f, TextAnchor.MiddleCenter, true, true);

        UiKit.Text(column, title.ToUpperInvariant(), 28f, UiKit.Hex("#1565C0"), FontStyles.Bold);
        UiKit.Text(column, "This screen is the next one to be rebuilt in Unity.", 14f, UiKit.Hex("#333333"));
        UiKit.Space(column, 10f);

        Button back = UiKit.Btn(column, "Back", UiKit.Shape("login_button"), Color.white, "BACK", 14f, Color.black, true, 140f, 40f);
        back.onClick.AddListener(() => App.Back());
    }
}
