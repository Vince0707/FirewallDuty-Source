using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Forgot password (activity_forgot_password.xml / ForgotPasswordActivity.java): sends a real Firebase password-reset email.
public class ForgotPasswordScreen : MenuScreen
{
    private static readonly Regex EmailPattern = new Regex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$");

    private TMP_InputField emailField;
    private Button nextButton;

    protected override void Build(RectTransform root)
    {
        Image bg = UiKit.Pic(root, "Background", UiKit.Shape("signup_bg"));
        UiKit.Stretch(bg.rectTransform);

        RectTransform column = UiKit.VBox(root, "Column", 0f, TextAnchor.MiddleCenter, false, true, 20);
        column.sizeDelta = new Vector2(350f, 0f);
        column.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
        column.gameObject.AddComponent<ShrinkToFit>();

        Image logo = UiKit.Pic(column, "Logo", UiKit.Picture("logo"));
        logo.preserveAspect = true;
        UiKit.Size(logo, 120f, 120f);

        UiKit.Space(column, 8f);
        UiKit.Text(column, "FORGOT PASSWORD", 28f, UiKit.Hex("#1565C0"), FontStyles.Bold);
        UiKit.Text(column, "Enter your registered email", 14f, UiKit.Hex("#444444"));
        UiKit.Space(column, 20f);

        RectTransform wrap = UiKit.New(column, "Email field");
        UiKit.Size(wrap, -1f, 50f);
        emailField = UiKit.Field(wrap, "Email", "Registered Email", false, new Color(1f, 1f, 1f, 0.55f), Color.black, 15f);
        emailField.contentType = TMP_InputField.ContentType.EmailAddress;
        UiKit.Stretch(emailField.GetComponent<RectTransform>());

        UiKit.Space(column, 20f);
        nextButton = UiKit.Btn(column, "NextButton", UiKit.Shape("login_button"), Color.white, "NEXT", 14f, Color.white, true, -1f, 50f);
        nextButton.onClick.AddListener(OnNext);
    }

    public override void OnShow(object arg)
    {
        emailField.text = arg as string ?? "";
        nextButton.interactable = true;
    }

    private void OnNext()
    {
        string email = emailField.text.Trim();

        if (email.Length == 0) { App.Toast("Email is required"); return; }
        if (!EmailPattern.IsMatch(email)) { App.Toast("Please enter a valid email address"); return; }

        nextButton.interactable = false;
        App.SetBusy(true, "Sending...");

        FirebaseRest.SendPasswordReset(email, result =>
        {
            App.SetBusy(false);
            nextButton.interactable = true;

            if (result.ok)
            {
                App.Toast("Password reset email sent. Check your inbox and spam folder.");
                App.Back("Login");
            }
            else
            {
                App.Toast("Unable to send the reset email. Check the email and try again.");
            }
        });
    }
}
