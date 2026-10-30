using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Create account (activity_sign_up.xml / SignUpActivity.java): full name, username, email, password (8+ characters), confirm.
public class SignUpScreen : MenuScreen
{
    private static readonly Regex EmailPattern = new Regex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$");

    private TMP_InputField fullName, username, email, password, confirm;
    private TMP_Text fullNameError, usernameError, emailError, passwordError, confirmError;
    private Button createButton;

    protected override void Build(RectTransform root)
    {
        Image bg = UiKit.Pic(root, "Background", UiKit.Shape("signup_bg"));
        UiKit.Stretch(bg.rectTransform);

        // Left panel: 45% wide, starting 5% in (the Android chain spreads the leftover width evenly)
        RectTransform left = UiKit.VBox(root, "LeftPanel", 0f, TextAnchor.MiddleCenter, true, true, 24);
        UiKit.Place(left, new Vector2(0.275f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        left.gameObject.AddComponent<ShrinkToFit>();

        Image logo = UiKit.Pic(left, "Logo", UiKit.Picture("logo"));
        logo.preserveAspect = true;
        UiKit.Size(logo, 170f, 170f);

        UiKit.Text(left, "CREATE ACCOUNT", 30f, UiKit.Hex("#1565C0"), FontStyles.Bold);
        UiKit.Text(left, "Join Firewall Duty", 16f, UiKit.Hex("#333333"));

        // Right panel: 40% wide, starting at 55%
        RectTransform right = UiKit.VBox(root, "RightPanel", 0f, TextAnchor.MiddleCenter, false, true, 24);
        UiKit.Place(right, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 0f));
        right.sizeDelta = new Vector2(320f, 0f);
        right.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;   // fields fill the panel's width
        right.gameObject.AddComponent<ShrinkToFit>();

        fullName = AddField(right, "Full Name", false, out fullNameError);
        username = AddField(right, "Username", false, out usernameError);
        email = AddField(right, "Email", false, out emailError);
        email.contentType = TMP_InputField.ContentType.EmailAddress;
        password = AddField(right, "Password", true, out passwordError);
        confirm = AddField(right, "Confirm Password", true, out confirmError);

        UiKit.Space(right, 10f);
        createButton = UiKit.Btn(right, "CreateButton", UiKit.Shape("login_button"), Color.white, "CREATE ACCOUNT", 14f, Color.white, true, -1f, 50f);
        createButton.onClick.AddListener(OnCreate);
    }

    // A field with a thin line under it, like Android's default EditText, and a red error line that takes no space when empty.
    private TMP_InputField AddField(RectTransform parent, string hint, bool isPassword, out TMP_Text error)
    {
        UiKit.Space(parent, 4f);

        RectTransform wrap = UiKit.New(parent, hint + " field");
        UiKit.Size(wrap, -1f, 44f);

        TMP_InputField field = UiKit.Field(wrap, hint, hint, isPassword, new Color(1f, 1f, 1f, 0.55f), Color.black, 15f);
        UiKit.Stretch(field.GetComponent<RectTransform>());

        Image underline = UiKit.Solid(wrap, "Underline", new Color(0.1f, 0.1f, 0.1f, 0.6f));
        RectTransform line = underline.rectTransform;
        line.anchorMin = new Vector2(0f, 0f);
        line.anchorMax = new Vector2(1f, 0f);
        line.pivot = new Vector2(0.5f, 0f);
        line.offsetMin = new Vector2(0f, 0f);
        line.offsetMax = new Vector2(0f, 1.5f);

        error = UiKit.Text(parent, "", 11f, UiKit.Hex("#D32F2F"), FontStyles.Normal, TextAlignmentOptions.Left, hint + " error");
        return field;
    }

    public override void OnShow(object arg)
    {
        fullName.text = username.text = email.text = password.text = confirm.text = "";
        ClearErrors();
        createButton.interactable = true;
    }

    private void ClearErrors()
    {
        fullNameError.text = usernameError.text = emailError.text = passwordError.text = confirmError.text = "";
    }

    private void OnCreate()
    {
        string name = fullName.text.Trim();
        string user = username.text.Trim();
        string mail = email.text.Trim();
        string pass = password.text;
        string again = confirm.text;

        ClearErrors();
        bool valid = true;

        if (name.Length == 0) { fullNameError.text = "Full Name is required"; valid = false; }
        if (user.Length == 0) { usernameError.text = "Username is required"; valid = false; }

        if (mail.Length == 0) { emailError.text = "Email is required"; valid = false; }
        else if (!EmailPattern.IsMatch(mail)) { emailError.text = "Invalid email address"; valid = false; }

        if (pass.Length == 0) { passwordError.text = "Password is required"; valid = false; }
        else if (pass.Length < 8) { passwordError.text = "Password must be at least 8 characters"; valid = false; }

        if (again.Length == 0) { confirmError.text = "Confirm Password is required"; valid = false; }
        else if (pass != again) { confirmError.text = "Passwords do not match"; valid = false; }

        if (!valid)
            return;

        createButton.interactable = false;
        App.SetBusy(true, "Creating your account...");

        FirebaseRest.SignUp(mail, pass, auth =>
        {
            if (!auth.ok)
            {
                Failed("Registration failed: " + auth.error);
                return;
            }

            // The player's record, same fields as the Android app.
            FirebaseRest.SetPlayerFields(auth.uid, new Dictionary<string, object>
            {
                { "uid", auth.uid },
                { "fullName", name },
                { "username", user },
                { "email", mail },
                { "level", 1 },
                { "exp", 0 }
            }, saved =>
            {
                if (!saved.ok)
                {
                    Failed("Account created, but your profile could not be saved: " + saved.error);
                    return;
                }

                // Back to the login screen, signed out, with the email filled in.
                FirebaseRest.SignOut();
                PlayerSession.SaveRememberedEmail(mail, true);
                App.SetBusy(false);
                App.Toast("Account created successfully!");
                App.Back("Login");
            });
        });
    }

    private void Failed(string message)
    {
        App.SetBusy(false);
        createButton.interactable = true;
        App.Toast(message);
    }
}
