using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_trivia.xml + activity_trivia_result.xml / TriviaActivity.java: 30 random questions out of the bank, answers shuffled,
// 5 trivia EXP per correct answer (kept on the phone like the Android app), Trivia Master for a perfect score.
public class TriviaScreen : MenuScreen
{
    private const int TotalQuestions = 30;
    private const int ExpPerCorrect = 5;
    private const string TriviaExpKey = "TriviaPrefs_trivia_exp";

    private class Question
    {
        public string text;
        public string[] answers;
        public int correct;
    }

    private static readonly Color Gold = new Color32(0xF2, 0xD5, 0x8A, 255);
    private static readonly Color Green = new Color32(76, 175, 80, 255);
    private static readonly Color Red = new Color32(244, 67, 54, 255);

    private List<Question> bank;
    private List<Question> quiz;
    private int current;
    private int score;
    private bool submitted;
    private int selected = -1;
    private int correctSlot;

    private RectTransform quizPanel;
    private RectTransform resultPanel;
    private TMP_Text counter;
    private TMP_Text expText;
    private TMP_Text questionText;
    private TMP_Text progressValue;
    private TMP_Text feedback;
    private Button submit;
    private TMP_Text submitLabel;
    private Image submitImage;
    private readonly Image[] rowImages = new Image[4];
    private readonly Image[] dots = new Image[4];
    private readonly TMP_Text[] rowTexts = new TMP_Text[4];
    private readonly Button[] rowButtons = new Button[4];
    private TMP_Text finalScore;
    private TMP_Text finalExp;
    private TMP_Text finalMessage;
    private readonly Image[] resultStars = new Image[3];

    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        quizPanel = UiKit.New(frame, "Quiz");
        UiKit.Stretch(quizPanel);
        BuildQuiz(quizPanel);

        resultPanel = UiKit.New(frame, "Result");
        UiKit.Stretch(resultPanel);
        BuildResult(resultPanel);
    }

    // ---- The question screen ----------------------------------------------------------------------------------

    private void BuildQuiz(RectTransform parent)
    {
        // Main panel: 105 each side, 25 top and bottom (704 x 361), 12 padding inside.
        Image panel = UiKit.Pic(parent, "Panel", UiKit.Shape("trivia_main_panel"));
        UiKit.Stretch(panel.rectTransform, 105f, 25f, 105f, 25f);
        RectTransform p = panel.rectTransform;

        // Header 680 x 62
        Image header = UiKit.Pic(p, "Header", UiKit.Shape("trivia_header"));
        UiLayout.TopLeft(header.rectTransform, 12f, 12f, 680f, 62f);

        Image shield = UiKit.Pic(header.transform, "Shield", UiKit.Shape("trivia_shield"));
        UiKit.Place(shield.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(38f, 38f));
        Image shieldPicture = UiKit.Pic(shield.transform, "Picture", CartoonArt.ShieldIcon());
        shieldPicture.type = Image.Type.Simple;
        shieldPicture.preserveAspect = true;
        UiKit.Stretch(shieldPicture.rectTransform, 4f, 4f, 4f, 4f);

        TextMeshProUGUI title = UiKit.Text(header.transform, "TRIVIA CHALLENGE", 15f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.Spaced(title, 0.08f);
        UiKit.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 9f), new Vector2(300f, 20f));
        TextMeshProUGUI subtitle = UiKit.Text(header.transform, "Test your cybersecurity knowledge!", 10f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
        UiLayout.Spaced(subtitle, 0.05f);
        UiKit.Place(subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, -9f), new Vector2(300f, 14f));

        Image counterBox = UiKit.Pic(header.transform, "Counter", UiKit.Shape("trivia_question_counter"));
        UiKit.Place(counterBox.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(413f, 0f), new Vector2(135f, 50f));
        counter = UiKit.Text(counterBox.transform, "Question 1 of 30", 14f, Color.white, FontStyles.Bold);
        UiKit.Stretch(counter.rectTransform);

        TextMeshProUGUI rewardTitle = UiKit.Text(header.transform, "EXP Reward", 15f, Color.white, FontStyles.Bold);
        UiKit.Place(rewardTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(556f, 10f), new Vector2(112f, 20f));
        expText = UiKit.Text(header.transform, "Trivia EXP: 0", 13f, UiKit.Hex("#D0BB91"), FontStyles.Bold);
        UiKit.Place(expText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(556f, -10f), new Vector2(112f, 18f));

        // Question: 680 x 48 at y 74
        questionText = UiKit.Text(p, "", 16f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        UiLayout.Spaced((TextMeshProUGUI)questionText, 0.04f);
        UiLayout.TopLeft(questionText.rectTransform, 12f + 18f, 74f, 680f - 36f, 48f);

        // Four answers, 640 wide, 34 high, every 38.
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Image row = UiKit.Pic(p, "Answer" + i, UiKit.Shape("trivia_answer"), Color.white, true);
            UiLayout.TopLeft(row.rectTransform, 32f, 122f + i * 38f, 640f, 34f);
            rowImages[i] = row;

            Button button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = row;
            button.onClick.AddListener(() => Select(index));
            rowButtons[i] = button;

            Image dot = UiKit.Pic(row.transform, "Radio", UiKit.Shape("profile_avatar_bg"), UiKit.Hex("#8FB3CC"));
            dot.type = Image.Type.Simple;
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(14f, 14f));
            dots[i] = dot;

            TextMeshProUGUI text = UiKit.Text(row.transform, "", 11f, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            UiKit.Stretch(text.rectTransform, 30f, 0f, 6f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            rowTexts[i] = text;
        }

        // Progress box 190 x 70 at x 32, y 295
        Image progress = UiKit.Pic(p, "Progress", UiKit.Shape("trivia_progress"));
        UiLayout.TopLeft(progress.rectTransform, 32f, 295f, 190f, 70f);
        TextMeshProUGUI progressTitle = UiKit.Text(progress.transform, "Progress", 19f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(progressTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(57f, 11f), new Vector2(120f, 24f));
        progressValue = UiKit.Text(progress.transform, "0 / 30", 15f, UiKit.Hex("#D0BB91"), FontStyles.Bold, TextAlignmentOptions.Left);
        UiKit.Place(progressValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, -12f), new Vector2(100f, 20f));

        feedback = UiKit.Text(p, "", 15f, Color.white, FontStyles.Bold);
        UiLayout.TopLeft(feedback.rectTransform, 214f, 306f, 290f, 48f);

        submit = UiKit.Btn(p, "Submit", UiKit.Shape("trivia_submit_button"), Color.white, "SUBMIT ANSWER", 13f, UiKit.Hex("#0B1F33"), true, -1f, -1f);
        UiLayout.TopLeft((RectTransform)submit.transform, 512f, 305f, 160f, 50f);
        submitImage = submit.GetComponent<Image>();
        submitLabel = submit.GetComponentInChildren<TMP_Text>();
        submit.onClick.AddListener(OnSubmit);

        UiLayout.BackButton(parent, 10f, 30f, 85f, 40f, 15f, () => App.Back());
    }

    // ---- The result screen ------------------------------------------------------------------------------------

    private void BuildResult(RectTransform parent)
    {
        Image dim = UiKit.Solid(parent, "Dim", UiKit.Hex("#70000000"));
        UiKit.Stretch(dim.rectTransform);

        Image panel = UiKit.Pic(parent, "Panel", UiKit.Shape("trivia_result_panel"));
        UiKit.Center(panel.rectTransform, 430f, 440f);

        RectTransform column = UiKit.VBox(panel.transform, "Column", 0f, TextAnchor.MiddleCenter, false, false, 30);
        UiKit.Stretch(column);

        Color ink = UiKit.Hex("#0B1F33");

        Image trophy = UiKit.Pic(column, "Trophy", CartoonArt.TrophyIcon());
        trophy.type = Image.Type.Simple;
        trophy.preserveAspect = true;
        UiKit.Size(trophy, 58f, 58f);

        RectTransform starRow = UiKit.HBox(column, "Stars", 6f, TextAnchor.MiddleCenter, true, true);
        for (int i = 0; i < 3; i++)
        {
            Image star = UiKit.Pic(starRow, "Star" + i, CartoonArt.StarIcon());
            star.type = Image.Type.Simple;
            star.preserveAspect = true;
            UiKit.Size(star, 26f, 26f);
            resultStars[i] = star;
        }

        UiKit.Space(column, 4f);
        UiKit.Text(column, "TRIVIA COMPLETE!", 28f, ink, FontStyles.Bold);
        UiKit.Space(column, 20f);
        finalScore = UiKit.Text(column, "30 / 30", 42f, ink, FontStyles.Bold);
        UiKit.Space(column, 10f);
        finalExp = UiKit.Text(column, "150 TRIVIA EXP", 18f, UiKit.Hex("#968366"), FontStyles.Bold);
        UiKit.Space(column, 20f);
        finalMessage = UiKit.Text(column, "", 15f, ink);
        UiKit.Size(finalMessage, 350f, -1f);
        UiKit.Space(column, 30f);

        Button menu = UiKit.Btn(column, "BackToMenu", UiKit.Shape("menu_button_big"), Color.white, "BACK TO GAME MENU", 14f, Color.white, true, 230f, 50f);
        menu.onClick.AddListener(() => App.Back());
    }

    // ---- Playing ----------------------------------------------------------------------------------------------

    public override void OnShow(object arg)
    {
        if (bank == null)
            bank = LoadBank();

        // A fresh quiz every time: 30 random questions.
        List<Question> shuffled = new List<Question>(bank);
        Shuffle(shuffled);
        quiz = shuffled.GetRange(0, Mathf.Min(TotalQuestions, shuffled.Count));
        current = 0;
        score = 0;

        resultPanel.gameObject.SetActive(false);
        quizPanel.gameObject.SetActive(true);
        LoadQuestion();
    }

    private void LoadQuestion()
    {
        submitted = false;
        selected = -1;

        Question question = quiz[current];
        counter.text = "Question " + (current + 1) + " of " + quiz.Count;
        questionText.text = question.text;

        // Shuffle the four choices and remember where the right one went.
        List<int> order = new List<int> { 0, 1, 2, 3 };
        Shuffle(order);
        correctSlot = order.IndexOf(question.correct);

        string[] letters = { "A", "B", "C", "D" };
        for (int i = 0; i < 4; i++)
        {
            rowTexts[i].text = letters[i] + "    " + question.answers[order[i]];
            rowTexts[i].color = Color.white;
            rowButtons[i].interactable = true;
            Paint(i, false);
        }

        progressValue.text = score + " / " + quiz.Count;
        expText.text = "Trivia EXP: " + score * ExpPerCorrect;
        feedback.text = "";
        submitLabel.text = "SUBMIT ANSWER";
        submitImage.sprite = UiKit.Shape("trivia_submit_button");
    }

    // The selected row shows the "checked" background and a lit dot.
    private void Paint(int slot, bool isSelected)
    {
        Sprite sprite = isSelected ? UiKit.Shape("trivia_answer_checked") : null;
        rowImages[slot].sprite = sprite != null ? sprite : UiKit.Shape("trivia_answer");
        dots[slot].color = isSelected ? Gold : UiKit.Hex("#8FB3CC");
    }

    private void Select(int slot)
    {
        if (submitted)
            return;

        selected = slot;
        for (int i = 0; i < 4; i++)
        {
            Paint(i, i == slot);
            rowTexts[i].color = i == slot ? Gold : Color.white;
        }
    }

    private void OnSubmit()
    {
        if (!submitted)
            SubmitAnswer();
        else
        {
            current++;

            if (current >= quiz.Count)
                ShowResult();
            else
                LoadQuestion();
        }
    }

    private void SubmitAnswer()
    {
        if (selected == -1)
        {
            App.Toast("Please select an answer.");
            return;
        }

        submitted = true;

        if (selected == correctSlot)
        {
            score++;
            AddTriviaExp(ExpPerCorrect);
            feedback.text = "CORRECT!  +5 EXP";
            feedback.color = Green;
        }
        else
        {
            feedback.text = "INCORRECT!";
            feedback.color = Red;
            rowTexts[selected].color = Red;
        }

        rowTexts[correctSlot].color = Green;

        progressValue.text = score + " / " + quiz.Count;
        expText.text = "Trivia EXP: " + score * ExpPerCorrect;

        foreach (Button row in rowButtons)
            row.interactable = false;

        bool last = current == quiz.Count - 1;
        submitLabel.text = last ? "VIEW RESULTS" : "NEXT QUESTION";
        submitImage.sprite = UiKit.Shape(last ? "trivia_submit_button" : "trivia_next_button");
    }

    private void ShowResult()
    {
        quizPanel.gameObject.SetActive(false);
        resultPanel.gameObject.SetActive(true);

        // Trivia Master only for a perfect score.
        if (score == TotalQuestions)
            UnlockTriviaMaster();

        // Stars: one from 15 right, two from 21, three from 27 (the same bands as the messages below).
        int stars = score >= 27 ? 3 : score >= 21 ? 2 : score >= 15 ? 1 : 0;
        for (int i = 0; i < resultStars.Length; i++)
            resultStars[i].color = i < stars ? Color.white : new Color(0.55f, 0.58f, 0.62f, 0.45f);

        finalScore.text = score + " / " + TotalQuestions;
        finalExp.text = score * ExpPerCorrect + " TRIVIA EXP";

        if (score >= 27) finalMessage.text = "Excellent! Outstanding cybersecurity knowledge!";
        else if (score >= 21) finalMessage.text = "Great job! You have strong cybersecurity knowledge!";
        else if (score >= 15) finalMessage.text = "Good effort! Keep learning and improving your cybersecurity knowledge.";
        else finalMessage.text = "Keep practicing! Review the cybersecurity topics and try again.";
    }

    private void UnlockTriviaMaster()
    {
        if (!PlayerSession.IsSignedIn)
        {
            App.Toast("Perfect score! Please log in to save your achievement.");
            return;
        }

        PlayerSession.MarkTriviaPerfect(ok =>
            App.Toast(ok ? "Trivia Master unlocked!" : "Achievement save failed. Please try again."));
    }

    private static void AddTriviaExp(int amount)
    {
        PlayerPrefs.SetInt(TriviaExpKey, PlayerPrefs.GetInt(TriviaExpKey, 0) + amount);
        PlayerPrefs.Save();
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // The question bank from the Android app (Resources/MenuUI/trivia_questions.json, made by Import Android Assets).
    private static List<Question> LoadBank()
    {
        List<Question> list = new List<Question>();
        TextAsset asset = Resources.Load<TextAsset>("MenuUI/trivia_questions");
        if (asset == null)
        {
            Debug.LogWarning("[Trivia] trivia_questions.json is missing. Run Tools > Firewall Duty > Menu UI > Import Android Assets.");
            return list;
        }

        List<object> items = MiniJson.Get(MiniJson.Parse(asset.text), "questions") as List<object>;
        if (items == null)
            return list;

        foreach (object item in items)
        {
            Dictionary<string, object> entry = item as Dictionary<string, object>;
            List<object> answers = MiniJson.Get(entry, "answers") as List<object>;
            if (entry == null || answers == null || answers.Count != 4)
                continue;

            Question q = new Question
            {
                text = MiniJson.GetString(entry, "question"),
                answers = new string[4],
                correct = System.Convert.ToInt32(MiniJson.GetString(entry, "correct"))
            };

            for (int i = 0; i < 4; i++)
                q.answers[i] = answers[i] as string;

            list.Add(q);
        }

        return list;
    }
}
