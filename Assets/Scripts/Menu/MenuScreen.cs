using UnityEngine;

// One screen of the menu (what used to be one Android Activity). Subclasses build their picture in Build and react in OnShow.
public abstract class MenuScreen
{
    protected MenuApp App { get; private set; }
    public string Id { get; private set; }
    public RectTransform Root { get; private set; }
    public CanvasGroup Group { get; private set; }

    public void Init(MenuApp app, RectTransform parent, string id)
    {
        App = app;
        Id = id;

        Root = UiKit.New(parent, id);
        UiKit.Stretch(Root);
        Group = Root.gameObject.AddComponent<CanvasGroup>();

        Build(Root);
        Root.gameObject.SetActive(false);
    }

    // Create the picture. Called once, the first time the screen is shown.
    protected abstract void Build(RectTransform root);

    // Called each time the screen appears. arg is whatever the previous screen passed in.
    public virtual void OnShow(object arg) { }

    public virtual void OnHide() { }

    // The phone's Back button. Default: go back to the previous screen.
    public virtual void OnBack()
    {
        App.Back();
    }
}
