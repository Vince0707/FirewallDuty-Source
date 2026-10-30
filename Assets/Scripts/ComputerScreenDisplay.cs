using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws a picture on a computer or TV screen. Put it on a world-space Canvas laid over the screen (the setup tool
// Tools > Firewall Duty > Set Up Computer Screens does that for every computer). The screen is built from code when
// the scene starts, so there are no picture files to manage, and some designs animate (blinking cursor, scrolling code).
//
// Designs:
//   Login    - an employee sign-in screen (cubicle computers)
//   NameList - a directory / visitor log of names (reception)
//   Code     - green terminal code scrolling past (IT experts)
//   Files    - a file explorer window (head manager)
//   BankFiles - the same window with the bank tellers' customer files
//   Meeting  - a security-briefing slide: agenda, chart, live status (meeting room TV)
//
// Only plain ASCII text is used, so it always draws with the default TextMeshPro font.
// [ExecuteAlways]: the picture is also drawn in the Scene and Game view while the game is not running, so screens and posters can
// be seen and placed without pressing Play. The pieces drawn in the editor are never saved into the scene.
[ExecuteAlways]
[RequireComponent(typeof(Canvas))]
public class ComputerScreenDisplay : MonoBehaviour
{
    public enum Design
    {
        Login,
        NameList,
        Code,
        Files,
        Meeting,
        PosterBrand,
        PosterMonitor,
        PosterThink,
        PosterPassword,
        PosterPhishing,
        PosterLock,
        BankFiles
    }

    [SerializeField] private Design design = Design.Login;
    public Design CurrentDesign => design;
    [Tooltip("Size of the screen in canvas pixels. The setup tool sets it to the screen's shape; layouts adapt.")]
    [SerializeField] private Vector2 pixelSize = new Vector2(800f, 500f);
    [Tooltip("The company logo, used by the PosterBrand design.")]
    [SerializeField] private Sprite logo;
    [Tooltip("Animations only run while the player is closer than this (metres), to save work on mobile.")]
    [SerializeField, Min(1f)] private float animateDistance = 9f;

    private static Sprite circleSprite;

    private RectTransform root;
    private Camera viewer;

    // Animated pieces
    private TMP_Text cursorLabel;       // Login: password field with a blinking cursor
    private string cursorBase;
    private TMP_Text clockLabel;
    private TMP_Text codeLabel;         // Code: the scrolling terminal
    private readonly string[] codeLines = new string[15];
    private int codeLineCount;
    private float codeTimer;
    private System.Random random;
    private Graphic liveDot;            // Meeting: blinking LIVE dot
    private float clockTimer;

    private static readonly Color Green = new Color(0.25f, 1f, 0.35f, 1f);

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnDisable()
    {
        // Outside Play Mode, leave no editor-only pieces behind.
        if (!Application.isPlaying)
            ClearChildren();
    }

#if UNITY_EDITOR
    // Changing the design (or size) in the Inspector redraws it straight away.
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled)
                Rebuild();
        };
    }
#endif

    private void ClearChildren()
    {
        if (root == null)
            root = transform as RectTransform;

        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    // Draws the screen from scratch (when the scene starts, and in the editor).
    private void Rebuild()
    {
        random = new System.Random(GetInstanceID());

        cursorLabel = null;
        clockLabel = null;
        codeLabel = null;
        liveDot = null;
        codeLineCount = 0;

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        root = (RectTransform)transform;
        root.sizeDelta = pixelSize;

        ClearChildren();

        switch (design)
        {
            case Design.Login: BuildLogin(); break;
            case Design.NameList: BuildNameList(); break;
            case Design.Code: BuildCode(); break;
            case Design.Files: BuildFiles(false); break;
            case Design.BankFiles: BuildFiles(true); break;
            case Design.Meeting: BuildMeeting(); break;
            case Design.PosterBrand: BuildPosterBrand(); break;
            case Design.PosterMonitor: BuildPosterMonitor(); break;
            case Design.PosterThink: BuildPosterThink(); break;
            case Design.PosterPassword: BuildPosterPassword(); break;
            case Design.PosterPhishing: BuildPosterPhishing(); break;
            case Design.PosterLock: BuildPosterLock(); break;
        }
    }

    private void Update()
    {
        if (viewer == null)
            viewer = Camera.main;

        // Nothing to animate far away: the text would be too small to read anyway.
        if (viewer != null && (viewer.transform.position - transform.position).sqrMagnitude > animateDistance * animateDistance)
            return;

        float t = Time.time;

        if (cursorLabel != null)
            cursorLabel.text = cursorBase + (((int)(t * 2f) & 1) == 0 ? "|" : " ");

        if (liveDot != null)
            liveDot.enabled = ((int)(t * 1.5f) & 1) == 0;

        clockTimer -= Time.deltaTime;
        if (clockLabel != null && clockTimer <= 0f)
        {
            clockTimer = 5f;
            clockLabel.text = System.DateTime.Now.ToString("HH:mm");
        }

        if (codeLabel != null)
        {
            codeTimer -= Time.deltaTime;
            if (codeTimer <= 0f)
            {
                codeTimer = 0.12f;
                AddCodeLine();
            }
        }
    }

    // =========================================================================================================
    // Login (employee cubicles)
    // =========================================================================================================

    private void BuildLogin()
    {
        Box("Background", new Color(0.05f, 0.1f, 0.2f), Vector2.zero, Vector2.one);
        Box("Glow", new Color(0.1f, 0.25f, 0.5f, 0.35f), new Vector2(0f, 0.55f), Vector2.one);

        clockLabel = Label("Clock", System.DateTime.Now.ToString("HH:mm"), 20, new Color(0.8f, 0.88f, 1f), TextAlignmentOptions.Right,
                           new Vector2(0.7f, 0.9f), new Vector2(0.97f, 0.99f));

        Fixed("Avatar", new Color(0.85f, 0.92f, 1f, 0.95f), new Vector2(0.5f, 0.76f), new Vector2(86f, 86f), Circle());
        Fixed("AvatarBody", new Color(0.3f, 0.45f, 0.7f, 1f), new Vector2(0.5f, 0.755f), new Vector2(44f, 44f), Circle());

        Label("Title", "FIREWALL DUTY", 34, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 0.52f), new Vector2(1f, 0.62f), true);
        Label("Subtitle", "Employee Portal", 18, new Color(0.65f, 0.78f, 0.95f), TextAlignmentOptions.Center, new Vector2(0f, 0.46f), new Vector2(1f, 0.52f));

        Fixed("UserField", new Color(1f, 1f, 1f, 0.14f), new Vector2(0.5f, 0.36f), new Vector2(330f, 40f));
        Label("UserText", "Username:  j.reyes", 19, Color.white, TextAlignmentOptions.Left, new Vector2(0.31f, 0.32f), new Vector2(0.7f, 0.4f));

        Fixed("PassField", new Color(1f, 1f, 1f, 0.14f), new Vector2(0.5f, 0.25f), new Vector2(330f, 40f));
        cursorBase = "Password:  ********";
        cursorLabel = Label("PassText", cursorBase, 19, Color.white, TextAlignmentOptions.Left, new Vector2(0.31f, 0.21f), new Vector2(0.7f, 0.29f));

        Fixed("Button", new Color(0.2f, 0.47f, 0.95f), new Vector2(0.5f, 0.13f), new Vector2(330f, 40f));
        Label("ButtonText", "SIGN IN", 20, Color.white, TextAlignmentOptions.Center, new Vector2(0.31f, 0.09f), new Vector2(0.7f, 0.17f), true);

        Label("Footer", "Session locked - sign in to continue", 15, new Color(0.6f, 0.7f, 0.85f), TextAlignmentOptions.Left,
              new Vector2(0.03f, 0.01f), new Vector2(0.7f, 0.07f));
    }

    // =========================================================================================================
    // Name list (reception)
    // =========================================================================================================

    private void BuildNameList()
    {
        Box("Background", new Color(0.95f, 0.96f, 0.97f), Vector2.zero, Vector2.one);
        Box("Header", new Color(0.05f, 0.4f, 0.45f), new Vector2(0f, 0.86f), Vector2.one);
        Label("Title", "RECEPTION  -  STAFF & VISITOR DIRECTORY", 24, Color.white, TextAlignmentOptions.Left,
              new Vector2(0.03f, 0.87f), new Vector2(0.8f, 0.99f), true);
        clockLabel = Label("Clock", System.DateTime.Now.ToString("HH:mm"), 22, new Color(0.85f, 1f, 1f), TextAlignmentOptions.Right,
                           new Vector2(0.8f, 0.87f), new Vector2(0.97f, 0.99f));

        Box("ColumnHeader", new Color(0.82f, 0.87f, 0.89f), new Vector2(0f, 0.78f), new Vector2(1f, 0.86f));
        Label("HName", "NAME", 17, new Color(0.2f, 0.28f, 0.3f), TextAlignmentOptions.Left, new Vector2(0.04f, 0.78f), new Vector2(0.45f, 0.86f), true);
        Label("HDept", "DEPARTMENT", 17, new Color(0.2f, 0.28f, 0.3f), TextAlignmentOptions.Left, new Vector2(0.47f, 0.78f), new Vector2(0.75f, 0.86f), true);
        Label("HStatus", "STATUS", 17, new Color(0.2f, 0.28f, 0.3f), TextAlignmentOptions.Left, new Vector2(0.8f, 0.78f), new Vector2(0.98f, 0.86f), true);

        string[,] rows =
        {
            { "Alvarez, Marco", "Finance", "In" },
            { "Bautista, Liza", "Human Resources", "In" },
            { "Cruz, Daniel", "IT Support", "In" },
            { "Dela Cruz, Ana", "Marketing", "Away" },
            { "Fernandez, Paolo", "Accounting", "In" },
            { "Garcia, Rina", "Reception", "In" },
            { "Lim, Joshua", "Security", "Meeting" },
            { "Navarro, Ella", "Interns", "In" },
            { "Reyes, Carlo", "Legal", "Out" }
        };

        float top = 0.78f;
        float rowHeight = 0.0775f;

        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y1 = top - i * rowHeight;
            float y0 = y1 - rowHeight;

            if (i % 2 == 1)
                Box("Row" + i, new Color(0.89f, 0.92f, 0.94f), new Vector2(0f, y0), new Vector2(1f, y1));

            Label("Name" + i, rows[i, 0], 20, new Color(0.1f, 0.14f, 0.16f), TextAlignmentOptions.Left, new Vector2(0.04f, y0), new Vector2(0.45f, y1));
            Label("Dept" + i, rows[i, 1], 19, new Color(0.25f, 0.3f, 0.33f), TextAlignmentOptions.Left, new Vector2(0.47f, y0), new Vector2(0.77f, y1));

            string status = rows[i, 2];
            Color dot = status == "In" ? new Color(0.15f, 0.7f, 0.3f) : status == "Out" ? new Color(0.85f, 0.25f, 0.2f) : new Color(0.95f, 0.65f, 0.1f);
            Fixed("Dot" + i, dot, new Vector2(0.815f, (y0 + y1) * 0.5f), new Vector2(14f, 14f), Circle());
            Label("Status" + i, status, 19, new Color(0.25f, 0.3f, 0.33f), TextAlignmentOptions.Left, new Vector2(0.84f, y0), new Vector2(0.99f, y1));
        }
    }

    // =========================================================================================================
    // Code (IT experts)
    // =========================================================================================================

    private void BuildCode()
    {
        Box("Background", new Color(0.0f, 0.03f, 0.01f), Vector2.zero, Vector2.one);
        Box("TopBar", new Color(0.0f, 0.12f, 0.04f), new Vector2(0f, 0.93f), Vector2.one);
        Label("Title", "root@fw-sentinel: ~ (secure shell)", 16, Green, TextAlignmentOptions.Left, new Vector2(0.02f, 0.93f), new Vector2(0.9f, 1f));

        codeLabel = Label("Code", "", 16, Green, TextAlignmentOptions.TopLeft, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.92f));
        codeLabel.richText = true;

        // Start with a screenful so it never looks empty.
        for (int i = 0; i < codeLines.Length; i++)
            AddCodeLine();
    }

    private void AddCodeLine()
    {
        string line = NextCodeLine();

        if (codeLineCount < codeLines.Length)
        {
            codeLines[codeLineCount++] = line;
        }
        else
        {
            for (int i = 1; i < codeLines.Length; i++)
                codeLines[i - 1] = codeLines[i];

            codeLines[codeLines.Length - 1] = line;
        }

        StringBuilder sb = new StringBuilder(codeLineCount * 40);
        for (int i = 0; i < codeLineCount; i++)
        {
            sb.Append(codeLines[i]);
            sb.Append('\n');
        }

        sb.Append("<color=#B8FFBC>root@fw-sentinel:~# </color>");
        codeLabel.text = sb.ToString();
    }

    private string NextCodeLine()
    {
        int kind = random.Next(0, 9);
        int a = random.Next(1, 255);
        int b = random.Next(1, 255);
        int c = random.Next(1, 255);

        switch (kind)
        {
            case 0: return "$ ./scan --target 10.0." + a + "." + b + " --ports 1-1024";
            case 1: return "[ OK ] firewall rule applied: DENY tcp/" + (random.Next(0, 3) == 0 ? 22 : random.Next(1000, 9000));
            case 2: return "0x" + Hex(4) + "  " + Hex(2) + " " + Hex(2) + " " + Hex(2) + " " + Hex(2) + " " + Hex(2) + " " + Hex(2) + " " + Hex(2) + " " + Hex(2);
            case 3: return "ids> alert sev=HIGH sig=PHISH-" + random.Next(100, 999) + " src=185." + a + "." + b + "." + c;
            case 4: return "auth: failed login 'admin' from 91." + a + "." + b + "." + c;
            case 5: return "[ OK ] patch KB" + random.Next(1000000, 9999999) + " installed";
            case 6: return "decrypting packet ........ done";
            case 7: return "sha256 " + Hex(8) + Hex(8) + " verified";
            default: return "quarantine: mail_" + random.Next(1000, 9999) + ".eml -> /vault";
        }
    }

    private string Hex(int digits)
    {
        const string chars = "0123456789ABCDEF";
        char[] buffer = new char[digits];
        for (int i = 0; i < digits; i++)
            buffer[i] = chars[random.Next(0, 16)];

        return new string(buffer);
    }

    // =========================================================================================================
    // Files (head manager)
    // =========================================================================================================

    private void BuildFiles(bool bank)
    {
        Box("Background", new Color(0.96f, 0.96f, 0.97f), Vector2.zero, Vector2.one);
        Box("TitleBar", new Color(0.15f, 0.25f, 0.45f), new Vector2(0f, 0.9f), Vector2.one);
        Label("Title", bank ? "File Explorer  -  Teller Records" : "File Explorer  -  Executive Reports", 20, Color.white, TextAlignmentOptions.Left, new Vector2(0.025f, 0.9f), new Vector2(0.8f, 1f), true);

        Box("AddressBar", new Color(0.88f, 0.9f, 0.93f), new Vector2(0.24f, 0.8f), new Vector2(0.98f, 0.88f));
        Label("Address", bank ? "This PC  >  Documents  >  Customer Files" : "This PC  >  Documents  >  Executive", 17, new Color(0.2f, 0.25f, 0.32f), TextAlignmentOptions.Left, new Vector2(0.26f, 0.8f), new Vector2(0.96f, 0.88f));

        // Sidebar
        Box("Sidebar", new Color(0.88f, 0.9f, 0.94f), new Vector2(0f, 0f), new Vector2(0.22f, 0.9f));
        string[] places = bank
            ? new[] { "Desktop", "Customer Files", "Loan Forms", "Daily Cash", "Transfers", "Archive" }
            : new[] { "Desktop", "Documents", "Downloads", "Mission Reports", "Incident Logs", "Archive" };
        for (int i = 0; i < places.Length; i++)
        {
            float y1 = 0.87f - i * 0.1f;
            if (i == 1)
                Box("Selected", new Color(0.7f, 0.8f, 0.95f), new Vector2(0f, y1 - 0.09f), new Vector2(0.22f, y1));

            Label("Place" + i, places[i], 17, new Color(0.15f, 0.2f, 0.3f), TextAlignmentOptions.Left, new Vector2(0.025f, y1 - 0.09f), new Vector2(0.22f, y1));
        }

        // File grid: 4 columns x 2 rows
        string[] names = bank
            ? new[] { "Customer_Accounts", "Loan_Applications", "Deposit_Slips", "Cash_Count_Today", "Transfer_Requests", "ID_Verification", "Teller_Log_0412", "Archive" }
            : new[] { "Q3_Security_Report", "Incident_Log_0412", "Board_Minutes", "Budget_2026", "Mission_Briefing", "IT_Request_Form", "Contracts", "Archive" };
        string[] kinds = bank
            ? new[] { "XLS", "PDF", "PDF", "XLS", "DOC", "PDF", "DOC", "DIR" }
            : new[] { "PDF", "XLS", "DOC", "XLS", "DOC", "PDF", "DIR", "DIR" };

        for (int i = 0; i < names.Length; i++)
        {
            int col = i % 4;
            int row = i / 4;
            float cx = 0.33f + col * 0.2f;
            float cy = 0.6f - row * 0.38f;

            if (i == 1)
                Fixed("Highlight", new Color(0.7f, 0.82f, 0.98f, 0.8f), new Vector2(cx, cy - 0.02f), new Vector2(150f, 150f));

            Color icon = kinds[i] == "PDF" ? new Color(0.85f, 0.25f, 0.2f)
                : kinds[i] == "XLS" ? new Color(0.15f, 0.62f, 0.32f)
                : kinds[i] == "DOC" ? new Color(0.2f, 0.45f, 0.85f)
                : new Color(0.95f, 0.75f, 0.2f);

            Fixed("Icon" + i, icon, new Vector2(cx, cy + 0.04f), new Vector2(64f, 78f));
            Label("Kind" + i, kinds[i], 17, Color.white, TextAlignmentOptions.Center, new Vector2(cx - 0.05f, cy - 0.03f), new Vector2(cx + 0.05f, cy + 0.11f), true);
            Label("File" + i, names[i], 14, new Color(0.15f, 0.18f, 0.25f), TextAlignmentOptions.Center, new Vector2(cx - 0.095f, cy - 0.14f), new Vector2(cx + 0.095f, cy - 0.04f));
        }
    }

    // =========================================================================================================
    // Meeting (TV in the meeting room): a security briefing slide
    // =========================================================================================================

    private void BuildMeeting()
    {
        Box("Background", new Color(0.06f, 0.09f, 0.16f), Vector2.zero, Vector2.one);
        Box("Header", new Color(0.1f, 0.2f, 0.4f), new Vector2(0f, 0.87f), Vector2.one);
        Label("Title", "WEEKLY SECURITY BRIEFING", 30, Color.white, TextAlignmentOptions.Left, new Vector2(0.03f, 0.87f), new Vector2(0.65f, 0.99f), true);

        liveDot = Fixed("LiveDot", new Color(0.95f, 0.2f, 0.2f), new Vector2(0.745f, 0.93f), new Vector2(16f, 16f), Circle());
        Label("Live", "LIVE  -  Meeting Room", 20, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Left, new Vector2(0.765f, 0.87f), new Vector2(0.99f, 0.99f));

        // Agenda
        Label("AgendaTitle", "AGENDA", 22, new Color(0.55f, 0.75f, 1f), TextAlignmentOptions.Left, new Vector2(0.04f, 0.76f), new Vector2(0.45f, 0.85f), true);

        string[] items =
        {
            "1.  Phishing awareness   [done]",
            "2.  Incident review   [now]",
            "3.  Patch and update status",
            "4.  New firewall rules",
            "5.  Questions"
        };

        for (int i = 0; i < items.Length; i++)
        {
            float y1 = 0.74f - i * 0.1f;
            bool current = i == 1;

            if (current)
                Box("NowBar", new Color(0.2f, 0.4f, 0.75f, 0.6f), new Vector2(0.03f, y1 - 0.09f), new Vector2(0.47f, y1));

            Label("Item" + i, items[i], 20, i == 0 ? new Color(0.55f, 0.85f, 0.6f) : Color.white, TextAlignmentOptions.Left,
                  new Vector2(0.045f, y1 - 0.09f), new Vector2(0.47f, y1));
        }

        // Chart
        Box("ChartPanel", new Color(0.1f, 0.14f, 0.24f), new Vector2(0.52f, 0.2f), new Vector2(0.97f, 0.85f));
        Label("ChartTitle", "Threats blocked this week", 20, new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Left, new Vector2(0.54f, 0.76f), new Vector2(0.96f, 0.84f), true);

        string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        float[] heights = { 0.5f, 0.7f, 0.4f, 0.85f, 0.6f, 0.3f, 0.45f };

        for (int i = 0; i < days.Length; i++)
        {
            float x0 = 0.545f + i * 0.0615f;
            float x1 = x0 + 0.045f;
            float maxHeight = 0.5f;

            Box("Bar" + i, new Color(0.25f, 0.6f, 0.95f), new Vector2(x0, 0.27f), new Vector2(x1, 0.27f + maxHeight * heights[i]));
            Label("Day" + i, days[i], 14, new Color(0.7f, 0.8f, 0.95f), TextAlignmentOptions.Center, new Vector2(x0 - 0.01f, 0.21f), new Vector2(x1 + 0.01f, 0.27f));
        }

        // Counters and ticker
        Label("Blocked", "1,284 blocked", 24, new Color(0.45f, 0.9f, 0.55f), TextAlignmentOptions.Left, new Vector2(0.04f, 0.2f), new Vector2(0.5f, 0.3f), true);
        Label("Open", "3 incidents open", 22, new Color(1f, 0.75f, 0.35f), TextAlignmentOptions.Left, new Vector2(0.04f, 0.12f), new Vector2(0.5f, 0.2f), true);

        Box("TickerBar", new Color(0.1f, 0.2f, 0.4f), new Vector2(0f, 0f), new Vector2(1f, 0.09f));
        Label("Ticker", "Reminder: report suspicious emails to the IT desk straight away.", 17, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.Left,
              new Vector2(0.03f, 0f), new Vector2(0.97f, 0.09f));
    }

    // =========================================================================================================
    // Wall posters (security awareness, 480 x 680)
    // =========================================================================================================

    private void BuildPosterBrand()
    {
        Box("Frame", new Color(0.07f, 0.15f, 0.3f), Vector2.zero, Vector2.one);
        Box("Paper", Color.white, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));

        if (logo != null)
        {
            GameObject go = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root, false);
        Flag(go);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.08f, 0.06f);
            rt.anchorMax = new Vector2(0.92f, 0.94f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.sprite = logo;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        else
        {
            Label("Name", "FIREWALL DUTY", 56, new Color(0.07f, 0.15f, 0.3f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.6f), true);
            Label("Tag", "CYBERSECURITY IS OUR DUTY.", 22, new Color(0.2f, 0.4f, 0.7f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.4f));
        }
    }

    private void BuildPosterMonitor()
    {
        Box("Frame", new Color(0.2f, 0.5f, 0.9f), Vector2.zero, Vector2.one);
        Box("Paper", new Color(0.07f, 0.15f, 0.3f), new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));

        Fixed("Shield", new Color(0.2f, 0.5f, 0.9f), new Vector2(0.5f, 0.86f), new Vector2(96f, 96f), Circle());
        Fixed("ShieldInner", new Color(0.07f, 0.15f, 0.3f), new Vector2(0.5f, 0.86f), new Vector2(66f, 66f), Circle());
        Fixed("Lock", Color.white, new Vector2(0.5f, 0.855f), new Vector2(28f, 24f));

        Label("Monitor", "MONITOR", 76, Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.6f), new Vector2(0.96f, 0.74f), true);
        Box("Line1", new Color(0.2f, 0.5f, 0.9f), new Vector2(0.2f, 0.585f), new Vector2(0.8f, 0.595f));
        Label("Detect", "DETECT", 76, new Color(0.45f, 0.72f, 1f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.43f), new Vector2(0.96f, 0.57f), true);
        Box("Line2", new Color(0.2f, 0.5f, 0.9f), new Vector2(0.2f, 0.415f), new Vector2(0.8f, 0.425f));
        Label("Defend", "DEFEND", 76, new Color(0.35f, 0.9f, 0.5f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.4f), true);

        Label("Brand", "FIREWALL DUTY", 28, new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.16f), true);
    }

    private void BuildPosterThink()
    {
        Box("Frame", new Color(0.12f, 0.12f, 0.14f), Vector2.zero, Vector2.one);
        Box("Paper", new Color(1f, 0.8f, 0.12f), new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));

        Label("Stop", "STOP.", 96, new Color(0.12f, 0.12f, 0.14f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.92f), true);
        Label("Think", "THINK.", 96, new Color(0.12f, 0.12f, 0.14f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.72f), true);
        Label("Click", "CLICK?", 96, new Color(0.12f, 0.12f, 0.14f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.32f), new Vector2(0.96f, 0.52f), true);

        Box("Strip", new Color(0.12f, 0.12f, 0.14f), new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.26f));
        Label("Tip", "Check the sender.  Check the link.", 26, new Color(1f, 0.8f, 0.12f), TextAlignmentOptions.Center, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.26f), true);
    }

    private void BuildPosterPassword()
    {
        Box("Frame", new Color(0.07f, 0.15f, 0.3f), Vector2.zero, Vector2.one);
        Box("Paper", new Color(0.9f, 0.95f, 1f), new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));
        Box("Header", new Color(0.2f, 0.5f, 0.9f), new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.97f));
        Label("Title", "STRONG\nPASSWORDS", 52, Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.78f), new Vector2(0.96f, 0.97f), true);

        Fixed("Field", new Color(1f, 1f, 1f), new Vector2(0.5f, 0.68f), new Vector2(380f, 54f));
        Label("Dots", "**********", 40, new Color(0.07f, 0.15f, 0.3f), TextAlignmentOptions.Center, new Vector2(0.1f, 0.63f), new Vector2(0.9f, 0.73f), true);

        string[] tips =
        {
            "12 or more characters",
            "Mix letters, numbers, symbols",
            "Never reuse a password",
            "Never share it, not even with IT"
        };

        for (int i = 0; i < tips.Length; i++)
        {
            float y1 = 0.56f - i * 0.12f;
            Fixed("Bullet" + i, new Color(0.2f, 0.5f, 0.9f), new Vector2(0.11f, y1 - 0.05f), new Vector2(18f, 18f), Circle());
            Label("Tip" + i, tips[i], 25, new Color(0.07f, 0.15f, 0.3f), TextAlignmentOptions.Left, new Vector2(0.16f, y1 - 0.1f), new Vector2(0.96f, y1));
        }

        Label("Brand", "FIREWALL DUTY", 22, new Color(0.2f, 0.4f, 0.7f), TextAlignmentOptions.Center, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.1f), true);
    }

    private void BuildPosterPhishing()
    {
        Box("Frame", new Color(0.35f, 0.05f, 0.05f), Vector2.zero, Vector2.one);
        Box("Paper", Color.white, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));
        Box("Header", new Color(0.8f, 0.15f, 0.15f), new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.97f));
        Label("Title", "REPORT\nPHISHING", 56, Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.97f), true);

        Fixed("Alert", new Color(0.8f, 0.15f, 0.15f), new Vector2(0.5f, 0.64f), new Vector2(70f, 70f), Circle());
        Label("Bang", "!", 60, Color.white, TextAlignmentOptions.Center, new Vector2(0.4f, 0.58f), new Vector2(0.6f, 0.7f), true);

        Label("Line1", "Got a strange email?", 32, new Color(0.15f, 0.15f, 0.2f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.52f), true);
        Label("Line2", "Do not click.\nDo not reply.", 30, new Color(0.3f, 0.3f, 0.35f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.44f));

        Box("Call", new Color(0.07f, 0.15f, 0.3f), new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.26f));
        Label("CallText", "TELL THE IT DESK\next. 204", 28, Color.white, TextAlignmentOptions.Center, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.26f), true);
    }

    private void BuildPosterLock()
    {
        Box("Frame", new Color(0.05f, 0.2f, 0.22f), Vector2.zero, Vector2.one);
        Box("Paper", new Color(0.07f, 0.35f, 0.38f), new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));

        Label("Title", "LOCK YOUR\nSCREEN", 66, Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.66f), new Vector2(0.96f, 0.92f), true);
        Label("Sub", "Leaving your desk?", 30, new Color(0.8f, 0.95f, 0.95f), TextAlignmentOptions.Center, new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.64f));

        // Key caps: WIN + L
        Fixed("KeyWin", new Color(0.95f, 0.97f, 1f), new Vector2(0.3f, 0.38f), new Vector2(120f, 90f));
        Label("WinText", "WIN", 38, new Color(0.07f, 0.15f, 0.3f), TextAlignmentOptions.Center, new Vector2(0.18f, 0.32f), new Vector2(0.42f, 0.44f), true);
        Label("Plus", "+", 52, Color.white, TextAlignmentOptions.Center, new Vector2(0.42f, 0.32f), new Vector2(0.58f, 0.44f), true);
        Fixed("KeyL", new Color(0.95f, 0.97f, 1f), new Vector2(0.7f, 0.38f), new Vector2(90f, 90f));
        Label("LText", "L", 48, new Color(0.07f, 0.15f, 0.3f), TextAlignmentOptions.Center, new Vector2(0.6f, 0.32f), new Vector2(0.8f, 0.44f), true);

        Label("Note", "Locking takes one second.\nA stolen session takes a lot longer to fix.", 22, new Color(0.8f, 0.95f, 0.95f), TextAlignmentOptions.Center,
              new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.28f));
        Label("Brand", "FIREWALL DUTY", 22, Color.white, TextAlignmentOptions.Center, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.11f), true);
    }

    // =========================================================================================================
    // Building blocks
    // =========================================================================================================

    // Pieces drawn outside Play Mode are temporary: they must not be written into the scene file.
    private static void Flag(GameObject go)
    {
        if (!Application.isPlaying)
            go.hideFlags = HideFlags.DontSave;
    }

    // A rectangle stretched between two anchors (0..1 across the screen).
    private Graphic Box(string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root, false);
        Flag(go);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // A rectangle (or circle) of a fixed size in canvas pixels, centred on a point given as 0..1 across the screen.
    private Graphic Fixed(string objectName, Color color, Vector2 anchor, Vector2 sizePixels, Sprite sprite = null)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root, false);
        Flag(go);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizePixels;
        rt.anchoredPosition = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Label(string objectName, string text, float size, Color color, TextAlignmentOptions alignment,
                           Vector2 anchorMin, Vector2 anchorMax, bool bold = false)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(root, false);
        Flag(go);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false;
        return tmp;
    }

    // A round picture made once in code (no image file needed).
    private static Sprite Circle()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }
}
