using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_story.xml / StoryActivity.java: four story slides in a dialogue box, then the mission briefing.
public class StoryScreen : MenuScreen
{
    private TMP_Text storyText;
    private Button nextButton;
    private string[] slides;
    private int slide;
    private bool busy;
    private MissionRequest request;

    protected override void Build(RectTransform root)
    {
        Image floor = UiKit.Solid(root, "Floor", Color.black);
        UiKit.Stretch(floor.rectTransform);
        UiKit.Cover(root, "Picture", UiKit.Picture("story_background"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#15000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        Image box = UiKit.Solid(frame, "DialogueBox", UiKit.Hex("#CC17619A"));
        UiKit.Center(box.rectTransform, UiLayout.DesignWidth - 200f, 190f);

        storyText = UiKit.Text(box.transform, "", 16f, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        UiLayout.Spaced((TextMeshProUGUI)storyText, 0.12f);
        UiKit.Stretch(storyText.rectTransform, 30f, 45f, 30f, 25f);

        nextButton = UiKit.Btn(box.transform, "Next", null, Color.clear, "NEXT", 15f, UiKit.Hex("#FFD600"), true, -1f, -1f);
        UiKit.Place((RectTransform)nextButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 10f), new Vector2(75f, 35f));
        nextButton.targetGraphic.raycastTarget = true;
        nextButton.onClick.AddListener(Next);
    }

    public override void OnShow(object arg)
    {
        request = arg as MissionRequest;
        slides = MissionData.StorySlides(request != null ? request.environment : "office");
        slide = 0;
        busy = false;
        nextButton.interactable = true;
        storyText.text = slides[0];
        storyText.alpha = 1f;
    }

    public override void OnBack()
    {
        // The story has no back step in the Android app either.
    }

    private void Next()
    {
        if (busy)
            return;

        busy = true;
        nextButton.interactable = false;
        App.StartCoroutine(slide < slides.Length - 1 ? NextSlide() : IntoBriefing());
    }

    private IEnumerator NextSlide()
    {
        yield return Fade(storyText, 1f, 0f, 0.7f);
        slide++;
        storyText.text = slides[slide];
        yield return Fade(storyText, 0f, 1f, 0.7f);

        busy = false;
        nextButton.interactable = true;
    }

    private IEnumerator IntoBriefing()
    {
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            Group.alpha = 1f - t / 1.2f;
            yield return null;
        }

        App.Show("MissionBriefing", request, false);
    }

    private static IEnumerator Fade(TMP_Text text, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            text.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }

        text.alpha = to;
    }
}
