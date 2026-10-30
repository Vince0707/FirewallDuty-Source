using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_loading.xml / LoadingActivity.java: three seconds of "Loading...", then the story (mission 1) or the briefing.
public class LoadingScreen : MenuScreen
{
    private MissionRequest request;
    private int visit;

    protected override void Build(RectTransform root)
    {
        UiLayout.Backdrop(root, "login_bg", "#D9000000");
        RectTransform frame = UiLayout.Frame(root);

        RectTransform column = UiKit.VBox(frame, "Column", 0f, TextAnchor.MiddleCenter, true, true);
        UiKit.Center(column, 0f, 0f);

        Image logo = UiKit.Pic(column, "Logo", UiKit.Picture("logo"));
        logo.preserveAspect = true;
        UiKit.Size(logo, 150f, 150f);

        UiKit.Text(column, "FIREWALL DUTY", 38f, UiKit.Hex("#42A5F5"), FontStyles.Bold);
        UiKit.Space(column, 30f);
        UiKit.Text(column, "Loading...", 18f, Color.white);
        UiKit.Space(column, 25f);

        Image track = UiKit.Solid(column, "ProgressTrack", new Color(1f, 1f, 1f, 0.2f));
        UiKit.Size(track, 280f, 4f);
        track.gameObject.AddComponent<RectMask2D>();
        Image bar = UiKit.Solid(track.transform, "Bar", UiKit.Hex("#42A5F5"));
        IndeterminateBar moving = track.gameObject.AddComponent<IndeterminateBar>();
        moving.bar = bar.rectTransform;

        UiKit.Space(column, 15f);
        UiKit.Text(column, "Initializing Security System...", 14f, UiKit.Hex("#BBBBBB"));
    }

    public override void OnShow(object arg)
    {
        request = arg as MissionRequest;
        visit++;
        App.StartCoroutine(Wait(visit));
    }

    public override void OnHide()
    {
        visit++;
    }

    public override void OnBack()
    {
        // Like the Android screen, there is nothing to go back to while loading.
    }

    private IEnumerator Wait(int myVisit)
    {
        yield return new WaitForSecondsRealtime(3f);

        if (myVisit != visit || request == null)
            yield break;

        // The loading screen is not kept in the history, so Back from the next screen returns to the mission list.
        App.Show(request.sceneNumber == 1 ? "Story" : "MissionBriefing", request, false);
    }
}
