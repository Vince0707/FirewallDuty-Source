using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The login screen (activity_main.xml / MainActivity.java): email + password, Remember Me, Forgot Password, social sign-in buttons, Sign Up.
public class LoginScreen : MenuScreen
{
    private static readonly Regex EmailPattern = new Regex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$");

    private TMP_InputField emailField;
    private TMP_InputField passwordField;
    private Toggle rememberToggle;
    private Button loginButton;
    private Image eyeIcon;
    private bool passwordHidden = true;

    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "DarkOverlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform column = UiKit.VBox(root, "Column", 0f, TextAnchor.MiddleCenter, true, true);
        column.gameObject.AddComponent<ShrinkToFit>();

        // Logo
        Image logo = UiKit.Pic(column, "Logo", UiKit.Picture("logo"));
        logo.preserveAspect = true;
        UiKit.Size(logo, 110f, 100f);

        // Title (a blue gradient, like the Android text shader) and description
        TextMeshProUGUI title = UiKit.Text(column, "FIREWALL DUTY", 29f, Color.white, FontStyles.Bold);
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(UiKit.Hex("#0D47A1"), UiKit.Hex("#0D47A1"), UiKit.Hex("#42A5F5"), UiKit.Hex("#42A5F5"));
        UiKit.Space(column, 1f);

        UiKit.Text(column, "3D MOBILE OFFICE CYBERSECURITY SIMULATION", 9f, UiKit.Hex("#DDDDDD"));
        UiKit.Space(column, 6f);

        // Email and password
        emailField = UiKit.Field(column, "Email", "Email", false, Color.white, Color.black, 14f, 310f, 35f);
        emailField.contentType = TMP_InputField.ContentType.EmailAddress;
        UiKit.Space(column, 6f);

        passwordField = UiKit.Field(column, "Password", "Password", true, Color.white, Color.black, 14f, 310f, 35f);
        BuildEyeButton(passwordField);

        // Remember me + Forgot password
        RectTransform row = UiKit.HBox(column, "RememberRow", 0f, TextAnchor.MiddleCenter, false, true);
        UiKit.Size(row, 320f, 34f);
        rememberToggle = UiKit.Check(row, "Remember", "Remember Me", 14f, Color.white);
        UiKit.Flexible(rememberToggle, 1f, 0f);

        Button forgot = LinkButton(row, "Forgot Password?", 14f, Color.white);
        forgot.onClick.AddListener(() => App.Show("ForgotPassword", emailField.text.Trim()));

        // Login
        loginButton = UiKit.Btn(column, "LoginButton", UiKit.Shape("login_button"), Color.white, "LOGIN", 14f, Color.black, true, 120f, 40f);
        loginButton.onClick.AddListener(OnLogin);

        UiKit.Space(column, 5f);
        UiKit.Text(column, "OR", 14f, Color.white, FontStyles.Bold);
        UiKit.Text(column, "Sign up with", 14f, Color.white);
        UiKit.Space(column, 4f);

        // Social buttons
        RectTransform social = UiKit.HBox(column, "Social", 12f, TextAnchor.MiddleCenter);
        Button google = UiKit.Btn(social, "GoogleButton", UiKit.Picture("google_icon"), Color.white, null, 0f, Color.white, false, 40f, 40f);
        google.onClick.AddListener(OnGoogle);
        google.GetComponent<Image>().preserveAspect = true;

        Button facebook = UiKit.Btn(social, "FacebookButton", UiKit.Picture("facebook_icon"), Color.white, null, 0f, Color.white, false, 40f, 40f);
        facebook.onClick.AddListener(OnFacebook);
        facebook.GetComponent<Image>().preserveAspect = true;

        UiKit.Space(column, 6f);

        // Sign up
        Button signUp = LinkButton(column, "Don't have an account? <color=#42A5F5>Sign Up</color>", 14f, Color.white);
        signUp.onClick.AddListener(() => App.Show("SignUp"));
    }

    // A piece of text that can be tapped.
    private static Button LinkButton(Transform parent, string richText, float size, Color color)
    {
        RectTransform rt = UiKit.New(parent, "Link");
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        TextMeshProUGUI text = UiKit.Text(rt, richText, size, color);
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UiKit.Stretch(text.rectTransform);

        // The button sizes itself to its text.
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = text.GetPreferredValues(richText).x + 8f;
        le.preferredHeight = text.GetPreferredValues(richText).y + 8f;

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        return button;
    }

    // The eye on the right end of the password field (ic_eye / ic_eye_off).
    private void BuildEyeButton(TMP_InputField field)
    {
        Image bg = field.GetComponent<Image>();

        RectTransform eye = UiKit.New(field.transform, "Eye");
        UiKit.Place(eye, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-5f, 0f), new Vector2(26f, 26f));

        eyeIcon = eye.gameObject.AddComponent<Image>();
        eyeIcon.sprite = UiKit.Icon("ic_eye");
        eyeIcon.preserveAspect = true;

        Button button = eye.gameObject.AddComponent<Button>();
        button.targetGraphic = eyeIcon;
        button.onClick.AddListener(() =>
        {
            passwordHidden = !passwordHidden;
            passwordField.contentType = passwordHidden ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            passwordField.ForceLabelUpdate();
            eyeIcon.sprite = UiKit.Icon(passwordHidden ? "ic_eye" : "ic_eye_off");
        });

        // Keep typed text clear of the eye.
        RectTransform viewport = (RectTransform)field.textViewport;
        viewport.offsetMax = new Vector2(-34f, viewport.offsetMax.y);
        bg.raycastTarget = true;
    }

    private static bool autoLoginTried;

    public override void OnShow(object arg)
    {
        passwordField.text = "";
        emailField.text = PlayerSession.RememberedEmail;
        rememberToggle.isOn = PlayerSession.RememberMe || PlayerSession.HasSavedSession;
        loginButton.interactable = true;

        // Signed in before with Remember Me, Google or Facebook: go straight in, no password or code.
        if (!autoLoginTried && PlayerSession.HasSavedSession && !PlayerSession.IsSignedIn)
        {
            autoLoginTried = true;
            loginButton.interactable = false;
            App.SetBusy(true, "Signing in...");

            FirebaseRest.SignInWithRefreshToken(PlayerSession.SavedRefreshToken, result =>
            {
                if (!result.ok)
                {
                    // The saved sign-in is no longer valid (or there is no internet): the normal login screen is used.
                    PlayerSession.ClearSession();
                    App.SetBusy(false);
                    loginButton.interactable = true;
                    return;
                }

                rememberToggle.isOn = true;
                LoadOrCreatePlayer(result);
            });
        }
    }


    public override void OnBack()
    {
        // This is the first screen: Back does nothing (the player leaves with the phone's Home button).
    }

    private void OnLogin()
    {
        string email = emailField.text.Trim();
        string password = passwordField.text;

        if (email.Length == 0) { App.Toast("Email is required"); return; }
        if (!EmailPattern.IsMatch(email)) { App.Toast("Please enter a valid email address"); return; }
        if (password.Length == 0) { App.Toast("Password is required"); return; }

        loginButton.interactable = false;
        App.SetBusy(true, "Signing in...");

        FirebaseRest.SignIn(email, password, result =>
        {
            if (!result.ok)
            {
                Failed(result.error);
                return;
            }

            PlayerSession.SaveRememberedEmail(email, rememberToggle.isOn);
            LoadOrCreatePlayer(result);
        });
    }

    // ---- Facebook ----------------------------------------------------------------------------------------------
    // Facebook's "device login": the game shows a short code, the player enters it at facebook.com/device (on this phone or any
    // other device where Facebook is logged in), approves, and the game receives the access token. Firebase then signs in with it.
    // It needs no Facebook app on the phone, so it also works inside the Unity Editor.

    private FacebookLogin.Session facebookSession;
    private RectTransform facebookDialog;
    private TMP_Text facebookCode;
    private TMP_Text facebookTitle;
    private TMP_Text facebookInfo;

    private void OnFacebook()
    {
        if (!FacebookLogin.IsConfigured)
        {
            App.Toast("Facebook sign-in is not set up (Resources/MenuUI/facebook_config.json is missing).");
            return;
        }

        EnsureFacebookDialog();
        facebookTitle.text = "LOG IN WITH FACEBOOK";
        facebookCode.text = "...";
        facebookInfo.text = "Contacting Facebook...";
        facebookDialog.gameObject.SetActive(true);
        facebookDialog.SetAsLastSibling();

        facebookSession = FacebookLogin.Start(
            (code, address) =>
            {
                facebookCode.text = code;
                facebookInfo.text = "On a phone or computer where you are logged in to Facebook, open\n" + address + "\nand enter this code, then approve.";
                Application.OpenURL(address);
            },
            (ok, value) =>
            {
                facebookDialog.gameObject.SetActive(false);

                if (!ok)
                {
                    if (!string.IsNullOrEmpty(value))
                        App.Toast("Facebook sign-in failed: " + value);

                    return;
                }

                loginButton.interactable = false;
                App.SetBusy(true, "Signing in...");
                FirebaseRest.SignInWithFacebook(value, result =>
                {
                    if (!result.ok)
                    {
                        Failed("Facebook Firebase login failed: " + result.error);
                        return;
                    }

                    rememberToggle.isOn = true;

                    if (!string.IsNullOrEmpty(result.email))
                    {
                        emailField.text = result.email;
                        rememberToggle.isOn = true;
                        PlayerSession.SaveRememberedEmail(result.email, true);
                    }

                    LoadOrCreatePlayer(result);
                });
            });
    }

    private void EnsureFacebookDialog()
    {
        if (facebookDialog != null)
            return;

        Image dim = UiKit.Solid(Root, "FacebookDim", new Color(0f, 0f, 0f, 0.7f));
        UiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        facebookDialog = dim.rectTransform;

        Image box = UiKit.Solid(dim.transform, "FacebookBox", UiKit.Hex("#102940"));
        UiKit.Center(box.rectTransform, 400f, 220f);
        box.raycastTarget = true;

        TextMeshProUGUI title = UiKit.Text(box.transform, "LOG IN WITH FACEBOOK", 16f, UiKit.Hex("#64B5F6"), FontStyles.Bold);
        facebookTitle = title;
        UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(360f, 22f));

        facebookCode = UiKit.Text(box.transform, "...", 34f, Color.white, FontStyles.Bold);
        UiKit.Place(facebookCode.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(360f, 44f));

        facebookInfo = UiKit.Text(box.transform, "", 12f, UiKit.Hex("#DDDDDD"));
        UiKit.Place(facebookInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(370f, 62f));

        Button cancel = UiKit.Btn(box.transform, "Cancel", UiKit.Shape("login_button"), Color.white, "CANCEL", 13f, Color.black, true, -1f, -1f);
        UiKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(120f, 34f));
        cancel.onClick.AddListener(() =>
        {
            if (facebookSession != null)
                facebookSession.Cancel();

            facebookDialog.gameObject.SetActive(false);
        });

        facebookDialog.gameObject.SetActive(false);
    }

    // The Google button: the phone's Google sign-in, then Firebase, then the same steps as an email login.
    private void OnGoogle()
    {
        // On a phone: the phone's own Google sheet. Anywhere else (the Editor): Google's device code, like the Facebook button.
        if (!NativeBridge.OnDevice)
        {
            OnGoogleWithCode();
            return;
        }

        loginButton.interactable = false;
        App.SetBusy(true, "Opening Google...");

        NativeBridge.GoogleSignIn(FirebaseRest.WebClientId, (ok, value) =>
        {
            if (!ok)
            {
                Failed(value);
                return;
            }

            App.SetBusy(true, "Signing in...");
            FirebaseRest.SignInWithGoogle(value, result =>
            {
                if (!result.ok)
                {
                    Failed("Google Firebase login failed: " + result.error);
                    return;
                }

                rememberToggle.isOn = true;

                if (!string.IsNullOrEmpty(result.email))
                {
                    emailField.text = result.email;
                    rememberToggle.isOn = true;
                    PlayerSession.SaveRememberedEmail(result.email, true);
                }

                LoadOrCreatePlayer(result);
            });
        });
    }

    private void OnGoogleWithCode()
    {
        if (!GoogleDeviceLogin.IsConfigured)
        {
            App.Toast("Google sign-in in the Editor needs Resources/MenuUI/google_device_config.json (see the setup steps). On a phone it works without it.");
            return;
        }

        EnsureFacebookDialog();
        facebookTitle.text = "LOG IN WITH GOOGLE";
        facebookCode.text = "...";
        facebookInfo.text = "Contacting Google...";
        facebookDialog.gameObject.SetActive(true);
        facebookDialog.SetAsLastSibling();

        facebookSession = GoogleDeviceLogin.Start(
            (code, address) =>
            {
                facebookCode.text = code;
                facebookInfo.text = "On a phone or computer where you are signed in to Google, open\n" + address + "\nand enter this code, then approve.";
                Application.OpenURL(address);
            },
            (ok, value) =>
            {
                facebookDialog.gameObject.SetActive(false);

                if (!ok)
                {
                    if (!string.IsNullOrEmpty(value))
                        App.Toast("Google sign-in failed: " + value);

                    return;
                }

                loginButton.interactable = false;
                App.SetBusy(true, "Signing in...");
                FirebaseRest.SignInWithGoogle(value, result =>
                {
                    if (!result.ok)
                    {
                        Failed("Google Firebase login failed: " + result.error);
                        return;
                    }

                    rememberToggle.isOn = true;

                    if (!string.IsNullOrEmpty(result.email))
                    {
                        emailField.text = result.email;
                        rememberToggle.isOn = true;
                        PlayerSession.SaveRememberedEmail(result.email, true);
                    }

                    LoadOrCreatePlayer(result);
                });
            });
    }

    // After signing in: load the player's record, or make a first one (same defaults as the Android app).
    private void LoadOrCreatePlayer(FirebaseRest.AuthResult auth)
    {
        FirebaseRest.GetPlayer(auth.uid, (result, player) =>
        {
            if (!result.ok)
            {
                Failed("Could not load player data: " + result.error);
                return;
            }

            if (player != null)
            {
                Finish(player);
                return;
            }

            string email = auth.email ?? "";
            int at = email.IndexOf('@');

            PlayerData created = new PlayerData
            {
                uid = auth.uid,
                email = email,
                fullName = string.IsNullOrWhiteSpace(auth.displayName) ? "Firewall Duty Player" : auth.displayName,
                username = at > 0 ? email.Substring(0, at) : "Player_" + auth.uid.Substring(0, System.Math.Min(6, auth.uid.Length)),
                level = 1,
                exp = 0
            };

            FirebaseRest.SetPlayerFields(auth.uid, new Dictionary<string, object>
            {
                { "uid", created.uid },
                { "fullName", created.fullName },
                { "username", created.username },
                { "email", created.email },
                { "level", created.level },
                { "exp", created.exp }
            }, saved =>
            {
                if (!saved.ok)
                {
                    Failed("Sign-in succeeded, but player data could not be saved: " + saved.error);
                    return;
                }

                Finish(created);
            });
        });
    }

    private void Finish(PlayerData player)
    {
        PlayerSession.SignIn(player);

        // Remember Me (and every Google / Facebook login) keeps the player signed in on this phone.
        if (rememberToggle.isOn)
            PlayerSession.SaveSession(FirebaseRest.RefreshToken);
        else
            PlayerSession.ClearSession();

        App.SetBusy(false);
        loginButton.interactable = true;
        App.ClearHistory();
        App.Show("GameMenu", null, false);
    }

    private void Failed(string message)
    {
        App.SetBusy(false);
        loginButton.interactable = true;
        App.Toast(message);
    }
}
