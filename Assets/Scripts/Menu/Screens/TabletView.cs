using System;
using UnityEngine;

// activity_tablet.xml / TabletActivity.java: the Cyber Tablet. Six threats on the left, the investigation (description + three
// pieces of evidence to find) on the right, and an EXAMPLES tab with one example screen per threat. The screens are the Android
// layouts themselves (see AndroidLayout); this class is the behaviour of TabletActivity.
public class TabletView
{
    public const float DesignWidth = 940f;
    public const float DesignHeight = 470f;

    private struct Evidence
    {
        public string title;
        public string description;
        public Evidence(string title, string description) { this.title = title; this.description = description; }
    }

    private struct Threat
    {
        public string title;
        public string description;
        public Evidence[] evidence;
        public string buttonId;
        public string exampleLayout;
    }

    private static readonly Threat[] Threats =
    {
        new Threat
        {
            title = "1. PHISHING EMAIL", buttonId = "btnPhishing", exampleLayout = "activity_examples",
            description = "Fake emails that appear to be from trusted sources to trick users into revealing sensitive information such as usernames, passwords, or financial details.",
            evidence = new[]
            {
                new Evidence("Suspicious Sender Address", "Check if the sender address is unusual."),
                new Evidence("Suspicious Link", "Look for links to fake websites."),
                new Evidence("Urgent or Threatening Language", "Identify pressure or urgency in messages.")
            }
        },
        new Threat
        {
            title = "2. MALWARE FILES", buttonId = "btnMalware", exampleLayout = "activity_malware",
            description = "Malicious files or software designed to damage computer systems, disrupt normal operations, steal information, or gain unauthorized access to a device.",
            evidence = new[]
            {
                new Evidence("Unexpected Attachment", "Analyze the attachment for suspicious file characteristics"),
                new Evidence("Suspicious Sender Address", "Verify the legitimacy of the email sender"),
                new Evidence("Unusual Computer Behavior", "Investigate signs of possible malware infection")
            }
        },
        new Threat
        {
            title = "3. POP-UP WINDOWS", buttonId = "btnPopup", exampleLayout = "activity_popup",
            description = "Unexpected windows that appear on a user's screen to deceive them into clicking harmful links, downloading unwanted software, or providing sensitive information.",
            evidence = new[]
            {
                new Evidence("Fake Security Warnings", "Claims the computer is infected"),
                new Evidence("Suspicious Download Prompt", "Asks the user to download unknown software"),
                new Evidence("Misleading Link or Button", "Tricks the user into clicking an unsafe link")
            }
        },
        new Threat
        {
            title = "4. SPAM MAIL", buttonId = "btnSpam", exampleLayout = "activity_spam",
            description = "Unwanted emails sent in large numbers that may contain advertisements, misleading messages, harmful links, or attempts to collect personal information.",
            evidence = new[]
            {
                new Evidence("Unrealistic Reward Offer", "Examine the offer for signs that it is too good to be true"),
                new Evidence("Personal Information Request", "Review the information being requested from the recipient"),
                new Evidence("Suspicious Sender Address", "Verify whether the sender belongs to a legitimate organization")
            }
        },
        new Threat
        {
            title = "5. SOCIAL ENGINEERING", buttonId = "btnSocial", exampleLayout = "activity_social",
            description = "Deceptive techniques used to manipulate people into revealing confidential information, performing unsafe actions, or granting unauthorized access.",
            evidence = new[]
            {
                new Evidence("Unverified Caller Identity", "Confirm whether the caller is an authorized IT staff member"),
                new Evidence("Credential Request", "Review the information requested by the caller"),
                new Evidence("IT Staff Impersonation", "Identify signs that the caller is pretending to be technical support")
            }
        },
        new Threat
        {
            title = "6. UNAUTHORIZED ACCESS", buttonId = "btnUnauthorized", exampleLayout = "activity_unauthorized",
            description = "Attempts to enter or use a computer system, account, or network without proper permission, potentially allowing sensitive information or resources to be accessed.",
            evidence = new[]
            {
                new Evidence("Login Outside Working Hours", "Check the login timestamp for unusual access times"),
                new Evidence("Unknown IP Address", "Investigate the source location of the login attempt"),
                new Evidence("Unrecognized Device", "Verify whether the device is registered to the employee")
            }
        }
    };

    private static readonly Color Gold = new Color32(0xF2, 0xD5, 0x8A, 255);
    private static readonly Color Ink = new Color32(0x0B, 0x1F, 0x33, 255);

    private AndroidLayout.View root;
    private readonly AndroidLayout.View[] threatButtons = new AndroidLayout.View[6];
    private readonly AndroidLayout.View[] examples = new AndroidLayout.View[6];
    private AndroidLayout.View mainInvestigation, examplesPanel, homeContainer, examplesContainer, txtHome, txtExamples;
    private AndroidLayout.View txtThreatTitle, txtDescription, exampleTitle, exampleDescription, txtEvidenceTitle;
    private readonly AndroidLayout.View[] evidenceTitles = new AndroidLayout.View[3];
    private readonly AndroidLayout.View[] evidenceDescriptions = new AndroidLayout.View[3];
    private readonly AndroidLayout.View[] checks = new AndroidLayout.View[3];

    // Which evidence is ticked for each threat (kept while the tablet is open).
    private readonly bool[][] evidenceChecked = { new bool[3], new bool[3], new bool[3], new bool[3], new bool[3], new bool[3] };
    private int currentThreat;
    private Action<string> toast;

    // host: the area the tablet fills (the Android design is 914 x 411). onBack: the BACK button. toast: for the "Home" message (may be null).
    public void Build(RectTransform host, Action onBack, Action<string> toast)
    {
        this.toast = toast;
        root = AndroidLayout.Inflate(host, "activity_tablet");
        if (root == null)
            return;

        // The background picture is drawn behind the tablet by the screen; the layout's own is not needed.
        root.SetBackground("@color/transparent");

        MakeRoom();

        mainInvestigation = root.Find("mainInvestigation");
        examplesPanel = root.Find("examplesPanel");
        homeContainer = root.Find("homeContainer");
        examplesContainer = root.Find("examplesContainer");
        txtHome = root.Find("txtHome");
        txtExamples = root.Find("txtExamples");
        txtThreatTitle = root.Find("txtThreatTitle");
        txtDescription = root.Find("txtDescription");
        exampleTitle = root.Find("exampleTitle");
        exampleDescription = root.Find("exampleDescription");
        txtEvidenceTitle = root.Find("txtEvidenceTitle");

        string[] numbers = { "One", "Two", "Three" };
        for (int i = 0; i < 3; i++)
        {
            evidenceTitles[i] = root.Find("txtEvidence" + numbers[i] + "Title");
            evidenceDescriptions[i] = root.Find("txtEvidence" + numbers[i] + "Description");
            checks[i] = root.Find("checkEvidence" + numbers[i]);
        }

        // All six example screens, each from its own layout, hidden until chosen.
        AndroidLayout.View container = root.Find("examplesContentContainer");
        for (int i = 0; i < Threats.Length; i++)
        {
            examples[i] = AndroidLayout.InflateInto(container, Threats[i].exampleLayout);
            if (examples[i] != null)
                examples[i].SetVisible(false);
        }

        for (int i = 0; i < Threats.Length; i++)
        {
            int index = i;
            threatButtons[i] = root.Find(Threats[i].buttonId);
            threatButtons[i].OnClick(() => ChooseThreat(index));
        }

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            checks[i].toggle.onValueChanged.AddListener(isOn =>
            {
                evidenceChecked[currentThreat][index] = isOn;
                UpdateEvidenceCount();
            });
        }

        // The gold-and-navy look: a deep navy body for the device, and the menu fonts on the tablet's own texts (the example pictures keep theirs,
        // because they imitate other programs).
        AndroidLayout.View device = root.Find("tabletDevice");
        if (device != null)
            device.SetBackgroundColor(new Color32(0x0A, 0x22, 0x4A, 255));

        System.Collections.Generic.List<Transform> exampleRoots = new System.Collections.Generic.List<Transform>();
        foreach (AndroidLayout.View example in examples)
        {
            if (example != null && example.rect != null)
                exampleRoots.Add(example.rect);
        }

        MenuStyle.ApplyFonts(host, exampleRoots.ToArray());

        // Home selected at the start.
        SelectTab(true);

        homeContainer.OnClick(() =>
        {
            SelectTab(true);
            mainInvestigation.SetVisible(true);
            examplesPanel.SetVisible(false);
            root.Relayout();
            this.toast?.Invoke("Home");
        });

        examplesContainer.OnClick(() =>
        {
            SelectTab(false);
            mainInvestigation.SetVisible(false);
            examplesPanel.SetVisible(true);
            ShowExample(currentThreat);
            root.Relayout();
        });

        root.Find("btnBack").OnClick(() => onBack?.Invoke());
        root.Find("btnBack").SetFontSize(14f);

        root.Relayout();
    }

    // The Android tablet was drawn for a phone that clips its bottom; here it gets a taller screen. The device is made a little
    // bigger (thinner bezel), and BACK sits outside it, above the top-left corner.
    private void MakeRoom()
    {
        Set("tabletDevice", "layout_marginStart", "12dp");
        Set("tabletDevice", "layout_marginEnd", "12dp");
        Set("tabletDevice", "layout_marginTop", "54dp");
        Set("tabletDevice", "layout_marginBottom", "10dp");
        Set("tabletScreen", "layout_marginTop", "28dp");
        Set("tabletScreen", "layout_marginBottom", "36dp");   // room under the screen for the round home button

        Set("btnBack", "layout_width", "84dp");
        Set("btnBack", "layout_height", "38dp");
        Set("btnBack", "layout_marginStart", "14dp");
        Set("btnBack", "layout_marginTop", "8dp");
        Set("btnBack", "textSize", "14sp");
        root.Find("btnBack").SetBackground("briefing_panel_bg");
    }

    private void Set(string id, string attribute, string value)
    {
        AndroidLayout.View view = root.Find(id);
        if (view != null)
            view.attributes[attribute] = value;
    }

    private void SelectTab(bool home)
    {
        homeContainer.SetBackground(home ? "home_example_selected" : "home_example_box");
        examplesContainer.SetBackground(home ? "home_example_box" : "home_example_selected");
        txtHome.SetTextColor(home ? Gold : Ink);
        txtExamples.SetTextColor(home ? Ink : Gold);
    }

    private void ChooseThreat(int index)
    {
        SaveCurrentEvidence();
        currentThreat = index;

        // The chosen button looks selected.
        for (int i = 0; i < threatButtons.Length; i++)
            threatButtons[i].SetBackground(i == index ? "threat_selected" : "threat_button");

        Threat threat = Threats[index];
        txtThreatTitle.SetText(threat.title);
        txtDescription.SetText(threat.description);
        exampleTitle.SetText(threat.title);
        exampleDescription.SetText(threat.description);

        for (int i = 0; i < 3; i++)
        {
            evidenceTitles[i].SetText(threat.evidence[i].title);
            evidenceDescriptions[i].SetText(threat.evidence[i].description);
        }

        LoadEvidenceState();
        ShowExample(index);
        root.Relayout();
    }

    private void SaveCurrentEvidence()
    {
        for (int i = 0; i < 3; i++)
            evidenceChecked[currentThreat][i] = checks[i].toggle.isOn;
    }

    private void LoadEvidenceState()
    {
        for (int i = 0; i < 3; i++)
            checks[i].toggle.SetIsOnWithoutNotify(evidenceChecked[currentThreat][i]);

        UpdateEvidenceCount();
    }

    private void UpdateEvidenceCount()
    {
        int count = 0;
        for (int i = 0; i < 3; i++)
        {
            if (checks[i].toggle.isOn)
                count++;
        }

        txtEvidenceTitle.SetText("EVIDENCE TO FIND (" + count + "/3)");
    }

    // Only one example is visible.
    private void ShowExample(int index)
    {
        for (int i = 0; i < examples.Length; i++)
        {
            if (examples[i] != null)
                examples[i].SetVisible(i == index);
        }
    }
}

// The tablet as a menu screen (Operations Hub > CYBER TABLET).
public class TabletScreen : MenuScreen
{
    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        UnityEngine.UI.Image shade = UiKit.Solid(root, "Shade", UiKit.Hex("#40000000"));
        UiKit.Stretch(shade.rectTransform);

        RectTransform frame = UiLayout.Frame(root, TabletView.DesignWidth, TabletView.DesignHeight);
        new TabletView().Build(frame, () => App.Back("OperationsHub"), message => App.Toast(message));
    }
}
