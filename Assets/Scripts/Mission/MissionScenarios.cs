using System.Collections.Generic;

// What the player has to do in each mission, as data. Every mission is a list of steps (MissionStep) and one investigation at a computer
// (EmailScenario). The six objectives of a mission are the ones shown in the briefing (MissionData.Objectives); the investigation completes
// the first five and the last step (reporting) completes the sixth.
//
// The investigation is built on the Cyber Tablet: every threat has three pieces of evidence on the tablet ("EVIDENCE TO FIND"), and the
// investigation hides exactly those three, with the tablet's own names. Finding the three completes the first three objectives, the fourth
// (identifying the indicators) follows by itself, then the player says what kind of threat it is (fifth) and reports it (sixth).
//
// Step kinds:
//   Meeting  stand in a room for some seconds while someone speaks
//   Talk     talk to a character (the character may hand you something: item)
//   PickUp   pick an object up from a desk
//   Deliver  give the item you carry to a character
//   Analyze  use a computer: the investigation (EmailInvestigationUI)
//   Report   tell a character what you found; this finishes the mission
//   Inspect  walk around the scene and examine places and machines yourself (EmailScenario.stations): flag the ones that match the
//            tablet's evidence, clear the normal ones. Finding all the evidence completes the first objectives, so the computer (Analyze,
//            in Summary style) is only used afterwards, to name the threat and choose the response.
//   Interview question a character (EmailScenario.questions): the questions that probe what the tablet lists as evidence record it, the
//            others cost time. When all the evidence is in, the decision (threat and response) opens by itself.
public enum StepKind
{
    Meeting,
    Talk,
    PickUp,
    Deliver,
    Analyze,
    Report,
    Inspect,
    Interview,
    Tablet      // a character hands the player their Cyber Tablet (mission 1 of each environment); see TabletAccess
}

// How the investigation looks: lines of a document to tap, cards compared with a profile, a table of records, or (after an Inspect step)
// a summary of the evidence that was found in the scene. Live is a stream of records that arrive one by one (the cards)
// and that the player must BLOCK in time, instead of reading a list that stays still. Filter is a set of switches (the cards) of a spam
// filter: the player switches ON the rules that match the evidence. Compare puts the email next to the bank's real notice: the
// player taps the three lines that differ.
public enum AnalysisKind
{
    Lines,
    Board,
    Table,
    Summary,
    Live,
    Filter,
    Compare
}

public class MissionStep
{
    public StepKind kind;
    public bool side;           // a side quest (numbered "SIDE QUEST 1 / 3"), before the main objectives
    public string heading;      // title of a side quest
    public string hint;         // what to do now (shown at the top)
    public string target;       // object name(s) in the scene, separated by commas (characters, the table, the desk)
    public string label;        // the word on the arrow over the target
    public string speaker;      // who speaks
    public string[] lines;      // what is said ("Speaker||text" gives a line its own speaker)
    public float seconds;       // Meeting: how long to stay
    public string prompt;       // the hand button text
    public string item;         // Talk: the item given; PickUp / Deliver: the item carried
    public string computer = "my";   // Analyze: "my" (the player's computer) or "terminal" (a computer of the IT room)
    public bool requireSeat;    // Meeting: the briefing only starts while the player is sitting on a chair (they are told to sit down)
}

public class EmailScenario
{
    public class Line
    {
        public string text;
        public int objective = -1;      // the objective this line completes when tapped (-1: nothing suspicious)
        public string callout;          // the red label shown once it is found (the tablet's name for that evidence)
        public string hint;             // what to say when a harmless line is tapped
        public bool link;               // drawn in link blue
        public bool spaceBefore;
        public bool mine;               // chat style: said by the player's side (drawn on the right)
        public bool header;             // chat style: a line across the top (who is calling)
        public string reference;        // Compare style: the same line in the bank's real notice (null = the same in both)
    }

    // A record shown as a card (Board) or as a row (Table).
    public class Card
    {
        public string title;
        public string[] fields;
        public int objective = -1;
        public string callout;
        public string hint;
    }

    // A place or machine to examine in the scene (Inspect step). Some show the evidence, some are normal.
    public class Station
    {
        // Where it is: "pc@npc" (the computer nearest to that character), "thing@Name@npc" (the object whose name starts with Name,
        // nearest to that character), or "name@Name" (the first object whose name starts with Name).
        public string locate;
        public string prompt = "Inspect";
        public string title;            // the window title on the inspection screen
        public string[] lines;          // what it shows
        public int objective = -1;      // the objective completed when it is flagged as evidence (-1: it is normal)
        public string callout;          // the tablet's name for the evidence
        public string hint;             // said after a wrong decision about it
        public string summary;          // one short line for the incident report once it is found
    }

    // A question to ask a character (Interview step).
    public class Question
    {
        public string text;             // what the player asks
        public string answer;           // what the character says
        public int objective = -1;      // the objective completed when asked (-1: it finds nothing useful)
        public string callout;          // the tablet's name for the evidence
        public string summary;          // one short line for the report once it is found
        public string note;             // said after a useless question
    }

    public List<MissionStep> steps = new List<MissionStep>();
    public List<Station> stations = new List<Station>();
    public List<Question> questions = new List<Question>();
    public string summaryHeading = "EVIDENCE YOU FOUND BY INSPECTING THE BANK";   // the title of the report (Summary analysis)
    public string interviewOpening = "Yes? What can I do for you?";                // what the character says before the first question
    public float liveInterval = 2f;     // Live analysis: seconds between two records of the stream
    public int filterTotal = 214;       // Filter analysis: messages in the flooded inbox
    public int filterPerRule = 70;      // Filter analysis: messages that each correct rule blocks

    // What the screen of the investigation is called and says.
    public string documentTitle = "Inbox  -  Mail";
    public string investigateQuestion = "Investigate the email.";
    public AnalysisKind analysis = AnalysisKind.Lines;
    public bool bubbles;                // show the lines as a conversation (a phone call), not as a document

    public string from, to, subject;
    public List<Line> lines = new List<Line>();

    // Board / Table analysis: the rules or profile the records are compared with, and the records.
    public string rosterTitle = "AUTHORIZED PROFILE";
    public string[] rosterLines;
    public List<Card> cards = new List<Card>();

    // Table analysis: the column titles (the rows are the cards, one field per column).
    public string[] tableHeader;
    public float[] tableWidths;         // optional: the width of each column (the last one is the flag column), 634 in all

    public string verdictQuestion = "What kind of threat is this? Compare it with your TABLET.";
    public string[] verdictOptions;     // the right one is verdictCorrect
    public string[] verdictFeedback;    // why each wrong choice is wrong
    public int verdictCorrect;
    public string verdictRight, verdictWrong;

    public string responseQuestion = "Choose the correct security response";
    public string[] responseOptions;
    public string[] responseFeedback;   // one explanation per option
    public int responseCorrect;

    public string[] hints;              // what to do next while investigating, one per objective
    public string[] idleKeys;           // characters that can be talked to when it is not their turn ...
    public string[] idleLines;          // ... and what each says (same order)
    public string idleDefault = "Hi! I am busy right now.";
    public float wrongTapPenalty = 5f;  // seconds lost for tapping something harmless
    public int clueCount = 3;           // the pieces of evidence (objectives 1..3), the same three as on the tablet
    public int summaryObjective = 3;    // completed by itself once all the evidence is found

    public string IdleFor(string character)
    {
        if (idleKeys != null)
        {
            for (int i = 0; i < idleKeys.Length && i < idleLines.Length; i++)
            {
                if (idleKeys[i] == character)
                    return idleLines[i];
            }
        }

        return idleDefault;
    }
}

public static class MissionScenarios
{
    private const string Boss = "mesh_office_m_boss01";
    private const string Workmate = "mesh_office_m_chara01";
    private const string Receptionist = "mesh_office_f_recepcionist01";
    private const string ItExperts = "mesh_office_f_it01,mesh_office_m_rival01";

    private static readonly string[] ThreatChoices =
    {
        "Phishing Email", "Malware Files", "Pop-up Windows", "Spam Mail", "Social Engineering", "Unauthorized Access", "Nothing Wrong"
    };

    // The scenario for a mission, or null when that mission has not been built yet.
    public static EmailScenario EmailFor(string environment, int sceneNumber)
    {
        if (MissionData.IsBank(environment))
        {
            switch (sceneNumber)
            {
                case 1: return FakeSecurityAlert();
                case 2: return SuspiciousBankAttachment();
                case 3: return UnverifiedCustomer();
                case 4: return ImpersonatedManager();
                case 5: return ConfidentialInquiry();
                case 6: return SuspiciousLogin();
                case 7: return UnauthorizedAccountAccess();
                case 8: return InboxFlood();
                case 9: return UrgentVerification();
                case 10: return SuspiciousTransaction();
                default: return null;   // the other bank missions are not built yet
            }
        }

        switch (sceneNumber)
        {
            case 1: return PayrollPhishing();
            case 2: return AfterHoursLogin();
            case 3: return ConfidentialFiles();
            case 4: return AdvertisementFlood();
            case 5: return PrizeGiveaway();
            case 6: return FakeSupportCall();
            case 7: return ConfidentialRequest();
            case 8: return SuspiciousAttachment();
            case 9: return EmergencyMeeting();
            case 10: return UrgentExecutive();
            default: return null;
        }
    }

    private static string[] Hints(string tabletName)
    {
        string find = "Open your TABLET, choose " + tabletName + ", and find the 3 pieces of evidence it lists.";
        return new[]
        {
            find, find, find, find,
            "Decide what kind of threat this is. Your TABLET describes each one.",
            "Choose the correct security response."
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 1: Payroll Information Update (Phishing Email).
    // Meeting -> report from your desk to the Head Manager -> your workmate -> the email on your computer -> report to IT.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario PayrollPhishing()
    {
        EmailScenario s = new EmailScenario
        {
            from = "payroll_hr-updateportal.com",
            to = "Employee@company.com",
            subject = "Payroll Information update Required",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in this email"
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Attend the meeting",
            hint = "Go to the meeting room and sit down on a chair for the briefing.",
            target = "(Prb)ConferenceTable", label = "MEETING ROOM", speaker = "Vice President", seconds = 30f, requireSeat = true,
            lines = new[]
            {
                "Good morning, everyone. A quick briefing before we start the day.",
                "Several employees received an email that looks like it comes from Payroll.",
                "It asks them to update their payroll information through a link, and says it must be done within 24 hours.",
                "Do not trust an email just because it looks official. Check who really sent it, and where the link goes.",
                "If you are unsure, open your tablet: it explains the warning signs. Stay alert, and report anything suspicious."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Tablet, side = true, heading = "Get your tablet",
            hint = "The Vice President is holding out your Cyber Tablet. Go and take it.",
            target = "mesh_office_f_boss01", label = "YOUR TABLET", speaker = "Vice President", prompt = "Take Tablet",
            lines = new[]
            {
                "One more thing before you go. This is your Cyber Tablet.",
                "It is your investigation tool. It lists every kind of threat, and the three signs to look for in each one.",
                "Keep it with you. Tap the TABLET button any time to open it. Good luck out there."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.PickUp, side = true, heading = "Get your report",
            hint = "Pick up the report paper from your desk.",
            target = "desk_01", label = "YOUR REPORT", item = "Report", prompt = "Take Report"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Deliver the report",
            hint = "Give the report to the Head Manager.",
            target = Boss, label = "HEAD MANAGER", speaker = "Head Manager", item = "Report", prompt = "Give Report",
            lines = new[]
            {
                "Ah, there it is. Thank you for the report.",
                "I heard about the payroll email going around. Do not click anything in it, and do not reply.",
                "Your workmate got one too. Go and see what you can find out, and tell me what we should do."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = false,
            hint = "Go to your workmate. They look worried about an email.",
            target = Workmate, label = "WORKMATE", speaker = "Workmate", prompt = "Talk",
            lines = new[]
            {
                "Hey! Do you have a minute? I just got a strange email from Payroll.",
                "It says I must update my payroll information through a link, or my salary will be delayed. It sounds urgent...",
                "I did not click anything. I forwarded it to your computer. Can you take a look at it for me?",
                "Open your tablet and look at Phishing Email: it lists what to look for. Then find it in the email."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer: your workmate forwarded you the email.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Check Email"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the IT experts and report the threat.",
            target = ItExperts, label = "IT EXPERT", speaker = "IT Expert", prompt = "Report Threat",
            lines = new[]
            {
                "You||I found a suspicious email on my computer. It says it is from Payroll and asks us to update our payroll information through a link.",
                "IT Expert||Good thing you did not click it. Did it ask for personal or financial details?",
                "You||Yes, my payroll details, and it gave a 24-hour deadline.",
                "IT Expert||That is a classic phishing email. We will block the sender and the link, and warn everyone in the company.",
                "IT Expert||Thank you for reporting it so quickly. Always check the sender, the link and what it asks for. Well done."
            }
        });

        s.idleKeys = new[] { Boss, Workmate, "mesh_office_f_it01", "mesh_office_m_rival01" };
        s.idleLines = new[]
        {
            "Stay sharp out there. I am counting on you.",
            "Hi! I need your help with something, but do your tasks first.",
            "Hi! If you find anything suspicious, come and tell me right away.",
            "Hi! If you find anything suspicious, come and tell me right away."
        };

        // The three pieces of evidence of the tablet's "Phishing Email", plus plain lines.
        s.lines.Add(new EmailScenario.Line { text = "From: " + s.from, objective = 0, callout = "Suspicious Sender Address" });
        s.lines.Add(new EmailScenario.Line { text = "To: " + s.to, hint = "Nothing wrong with this line." });
        s.lines.Add(new EmailScenario.Line { text = "Subject: " + s.subject, hint = "A subject alone does not prove anything." });
        s.lines.Add(new EmailScenario.Line { text = "Dear Employee,", spaceBefore = true, hint = "Nothing wrong with a greeting." });
        s.lines.Add(new EmailScenario.Line
        {
            text = "Please update your payroll information by clicking the link below:",
            hint = "This just introduces the link. Look at the link itself."
        });
        s.lines.Add(new EmailScenario.Line { text = "www.hr-updateportal.com/update", link = true, objective = 1, callout = "Suspicious Link" });
        s.lines.Add(new EmailScenario.Line { text = "This must be done within 24 hours, or your salary will be delayed!", objective = 2, callout = "Urgent or Threatening Language" });
        s.lines.Add(new EmailScenario.Line { text = "Thank You,", spaceBefore = true, hint = "Nothing wrong with a thank you." });
        s.lines.Add(new EmailScenario.Line { text = "HR Department", hint = "A signature alone does not prove anything." });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 0;
        s.verdictFeedback = new[]
        {
            "",
            "There is no file or attachment to open. Read the tablet's description of Malware Files.",
            "This is an email, not a window that suddenly appears on the screen.",
            "Spam is unwanted bulk mail full of offers. This one pretends to be Payroll to get your information.",
            "That means tricking a person face to face or by phone. This is an email.",
            "Nobody is trying to log in to a system here. It is an email asking for information.",
            "A real payroll office does not use a strange sender, an odd link and a deadline with a threat."
        };
        s.verdictRight = "Correct. A fake sender, a suspicious link and a threatening deadline: it is a phishing email.";
        s.verdictWrong = "Not quite. Compare the email with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Click the link and update my payroll information",
            "Reply with my bank details to be safe",
            "Report the email to IT Security and delete it",
            "Forward the email to my coworkers"
        };
        s.responseFeedback = new[]
        {
            "That link leads to a fake site that would steal your payroll details.",
            "Never send financial details by email: it goes straight to the attacker.",
            "Correct. Reporting it protects you and everyone else in the company.",
            "Forwarding it spreads the threat. Report it to IT Security instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Phishing Email");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 2: After-hours Login Activity (Unauthorized Access).
    // The receptionist hands you a keycard -> give it to an IT expert -> check the access log on the IT room terminal (login cards
    // compared with the employee's profile) -> report to the Head Manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario AfterHoursLogin()
    {
        EmailScenario s = new EmailScenario
        {
            documentTitle = "IT Terminal  -  Access Log",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in these logins",
            analysis = AnalysisKind.Board
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Visit the reception",
            hint = "Go to the receptionist at the front desk. She has something for you.",
            target = Receptionist, label = "RECEPTIONIST", speaker = "Receptionist", item = "Keycard", prompt = "Talk",
            lines = new[]
            {
                "Oh, good, you are here! Someone left this access keycard at the front desk this morning.",
                "It is not mine, and it does not look like a visitor card. It has an employee name on it.",
                "Could you take it to the IT experts? They can tell whose card it is.",
                "Thank you! I would not know who else to ask."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Hand in the keycard",
            hint = "Give the keycard to an IT expert.",
            target = ItExperts, label = "IT EXPERT", speaker = "IT Expert", item = "Keycard", prompt = "Give Keycard",
            lines = new[]
            {
                "Let me see... this card belongs to Carlos Mendoza, from Payroll.",
                "That is odd. Our system says an account of his was used last night, when nobody should be here.",
                "I have the access log open on the terminal in the IT room. Can you go through the logins and tell me which ones look wrong?",
                "Compare each login with Carlos's profile at the top. Your tablet lists what to look for in Unauthorized Access."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to the terminal in the IT room and check the access log.",
            computer = "terminal", target = "mesh_office_f_it01", label = "IT TERMINAL", prompt = "Check Access Log"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Head Manager and report what you found.",
            target = Boss, label = "HEAD MANAGER", speaker = "Head Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, I checked the access log in the IT room. Carlos Mendoza's account was used at 2:47 AM, from an address and a device we do not know.",
                "Head Manager||Good thing you caught it. Does the keycard from the front desk have anything to do with it?",
                "You||Probably. It was left at reception, and the logins came from a device that is not registered.",
                "Head Manager||Then someone got into that account without permission. I will have IT lock the account and reset the password right away.",
                "Head Manager||Well done. Always check the hours, the address and the device, and report it at once."
            }
        });

        s.idleKeys = new[] { Receptionist, Boss, "mesh_office_f_it01", "mesh_office_m_rival01" };
        s.idleLines = new[]
        {
            "Please take that keycard to the IT experts when you can.",
            "Stay sharp out there. I am counting on you.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        s.rosterTitle = "CARLOS MENDOZA  -  NORMAL PROFILE";
        s.rosterLines = new[]
        {
            "Works: 08:00 AM - 05:00 PM          Device: OFFICE-PC-12",
            "Network: office (192.168.1.x)"
        };

        // Three logins that show the tablet's three pieces of evidence, three normal ones.
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #1", fields = new[] { "Time: 02:47 AM", "IP: 192.168.1.20", "Device: OFFICE-PC-12" },
            objective = 0, callout = "Login Outside Working Hours"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #2", fields = new[] { "Time: 09:05 AM", "IP: 185.72.14.91", "Device: OFFICE-PC-12" },
            objective = 1, callout = "Unknown IP Address"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #3", fields = new[] { "Time: 11:20 AM", "IP: 192.168.1.20", "Device: UNKNOWN-PC-77" },
            objective = 2, callout = "Unrecognized Device"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #4", fields = new[] { "Time: 08:40 AM", "IP: 192.168.1.20", "Device: OFFICE-PC-12" },
            hint = "This login matches the profile: normal hours, network and device."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #5", fields = new[] { "Time: 01:15 PM", "IP: 192.168.1.20", "Device: OFFICE-PC-12" },
            hint = "This login matches the profile: normal hours, network and device."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "LOGIN #6", fields = new[] { "Time: 04:55 PM", "IP: 192.168.1.20", "Device: OFFICE-PC-12" },
            hint = "This login matches the profile: normal hours, network and device."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 5;
        s.verdictFeedback = new[]
        {
            "There is no email or fake link here. Someone logged in to a real account.",
            "No file was infected or opened. The record shows logins, not a program.",
            "Nothing popped up on a screen. Look at who logged in, when and from where.",
            "There is no bulk mail here. This is an access log.",
            "Nobody was tricked into anything in this record. Someone simply got into the account.",
            "",
            "A real employee does not log in at 2:47 AM, or from an unknown address or device."
        };
        s.verdictRight = "Correct. A login outside working hours, from an unknown address and an unregistered device: someone is using the account without permission.";
        s.verdictWrong = "Not quite. Compare the logins with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Ignore it, it was probably just a late shift",
            "Log in to the account myself to see what happened",
            "Report it to the Head Manager so the account can be locked",
            "Tell the whole office to change their passwords by email"
        };
        s.responseFeedback = new[]
        {
            "Nobody on a normal shift logs in at 2:47 AM from an unknown device. Ignoring it lets the intruder stay in.",
            "Do not use a compromised account. You could destroy evidence or warn the intruder.",
            "Correct. The account can be locked, the password reset and the intruder traced.",
            "A mass email causes panic and can be copied by attackers. Report it to the Head Manager first."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Unauthorized Access");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 3: Confidential File Access (Unauthorized Access).
    // Take the printout from the printer -> give it to the Accountant in the meeting room -> read the file access records at your
    // computer (a table of who opened the file, to compare with the file's rules) -> report to the Vice President.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario ConfidentialFiles()
    {
        const string Accountant = "mesh_office_m_accountant01";
        const string VicePresident = "mesh_office_f_boss01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "File Server  -  Salary_Report.xlsx  -  Access Records",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in these records",
            analysis = AnalysisKind.Table
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.PickUp, side = true, heading = "Collect the printout",
            hint = "Pick up the printout from the printer.",
            target = "mesh_printer_01", label = "PRINTER", item = "Printout", prompt = "Take Printout"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take it to the accountant",
            hint = "Bring the printout to the accountant in the meeting room.",
            target = Accountant, label = "ACCOUNTANT", speaker = "Accountant", item = "Printout", prompt = "Give Printout",
            lines = new[]
            {
                "Perfect, this is the printout I was waiting for. It lists who opened the Salary Report this month.",
                "That file is confidential. Only accountants and the Head Manager may open it, from the office, during office hours.",
                "Something on this list does not look right. I sent the full file log to your computer.",
                "Please check it. Open your tablet and look at Unauthorized Access: it lists what to look for."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer and check the file access records.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Check File Log"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Vice President and report what you found.",
            target = VicePresident, label = "VICE PRESIDENT", speaker = "Vice President", prompt = "Report Threat",
            lines = new[]
            {
                "You||Ma'am, I checked the access records of the Salary Report. Someone opened it at night, from an unknown address, and from a device that is not registered.",
                "Vice President||Who is allowed to open that file?",
                "You||Only the accountants and the Head Manager, from the office, during office hours.",
                "Vice President||Then that is unauthorized access to confidential data. I will have the permissions reviewed today and the accounts checked.",
                "Vice President||Thank you for catching it. A file is only for the people who need it, and anything else must be reported."
            }
        });

        s.idleKeys = new[] { Accountant, VicePresident, "mesh_office_f_it01", "mesh_office_m_rival01" };
        s.idleLines = new[]
        {
            "Thank you again. Please check that file log when you can.",
            "Stay alert. Report anything unusual.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        s.rosterTitle = "RULES OF THE FILE  -  Salary_Report.xlsx (CONFIDENTIAL)";
        s.rosterLines = new[]
        {
            "Allowed: Accountants and the Head Manager, 08:00 AM - 05:00 PM",
            "From the office network (192.168.1.x) and a registered device (OFFICE-PC)"
        };
        s.tableHeader = new[] { "USER", "ROLE", "TIME", "IP ADDRESS", "DEVICE" };

        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "dan.cruz", "Accountant", "11:58 PM", "192.168.1.31", "OFFICE-PC-04" },
            objective = 0, callout = "Login Outside Working Hours"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "maria.santos", "Accountant", "10:15 AM", "203.0.113.50", "OFFICE-PC-07" },
            objective = 1, callout = "Unknown IP Address"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "head.manager", "Manager", "09:00 AM", "192.168.1.10", "UNKNOWN-LAPTOP-9" },
            objective = 2, callout = "Unrecognized Device"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "maria.santos", "Accountant", "09:30 AM", "192.168.1.22", "OFFICE-PC-07" },
            hint = "This record follows the rules: allowed person, office hours, office network, registered device."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "dan.cruz", "Accountant", "01:30 PM", "192.168.1.31", "OFFICE-PC-04" },
            hint = "This record follows the rules: allowed person, office hours, office network, registered device."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "head.manager", "Manager", "03:45 PM", "192.168.1.10", "OFFICE-PC-01" },
            hint = "This record follows the rules: allowed person, office hours, office network, registered device."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 5;
        s.verdictFeedback = new[]
        {
            "There is no email or fake link here. People opened a file on the server.",
            "No program or infected file is involved. The records show who opened a file.",
            "Nothing popped up on a screen. Look at when, from where and on what the file was opened.",
            "There is no bulk mail here. This is a file access record.",
            "Nobody was tricked into anything in this record. The file was opened in ways it should not be.",
            "",
            "Some of these records break the rules of the file: wrong hour, unknown address, unregistered device."
        };
        s.verdictRight = "Correct. The file was opened at night, from an unknown address and from an unregistered device: that is unauthorized access.";
        s.verdictWrong = "Not quite. Compare the records with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Ignore it, the file was only opened, not changed",
            "Give everyone access to the file so it is fair",
            "Report it to the Vice President so permissions can be reviewed",
            "Confront the employee yourself and demand an explanation"
        };
        s.responseFeedback = new[]
        {
            "Even only opening a confidential file is a breach. Ignoring it lets it happen again.",
            "Confidential files are protected on purpose. Giving everyone access makes the problem much bigger.",
            "Correct. Management can review permissions and check the accounts and devices involved.",
            "Accusing someone yourself can spoil the investigation. Report it to the right person instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Unauthorized Access");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 4: Inbox Advertisement Flood (Spam Email).
    // The intern at the printer tells what happened (15 seconds) -> the secretary gives you a coffee -> bring it to the IT expert -> sort
    // the flooded inbox at your computer (a mail list) -> report to the other IT expert.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario AdvertisementFlood()
    {
        const string Intern = "mesh_office_m_intern01";
        const string Secretary = "mesh_office_m_secretary01";
        const string ItFemale = "mesh_office_f_it01";
        const string ItMale = "mesh_office_m_rival01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Inbox  -  214 new messages since midnight",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in this inbox",
            analysis = AnalysisKind.Table
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the intern",
            hint = "Go to the printer and listen to the intern.",
            target = "mesh_printer_01", label = "PRINTER", speaker = "Intern", seconds = 15f,
            lines = new[]
            {
                "Oh hi! Sorry, I am just waiting for the printer.",
                "Since last night my inbox has been flooded with ads. Hundreds of emails, in just a few hours!",
                "Free phones, prizes, special offers... I did not sign up for any of them. I cannot find my real work emails anymore."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Visit the kitchen",
            hint = "Go to the secretary in the kitchen.",
            target = Secretary, label = "SECRETARY", speaker = "Secretary", item = "Coffee", prompt = "Talk",
            lines = new[]
            {
                "Morning! Have you seen the mess in the inboxes? Mine is full of ads too.",
                "The IT expert has been fighting it all night and has not had a single coffee.",
                "Could you take her this one? And maybe you can help her sort out the emails."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Bring the coffee",
            hint = "Give the coffee to the IT expert.",
            target = ItFemale, label = "IT EXPERT", speaker = "IT Expert", item = "Coffee", prompt = "Give Coffee",
            lines = new[]
            {
                "Oh, you are a lifesaver! Thank you for the coffee.",
                "The mail server got hit with thousands of ads overnight. I need someone to look through a sample inbox.",
                "I forwarded one to your computer. Tell me which messages are the problem. Your tablet lists what to look for in Spam Mail."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer and look through the inbox.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Check Inbox"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the IT expert at the desk and report what you found.",
            target = ItMale, label = "IT EXPERT", speaker = "IT Expert", prompt = "Report Threat",
            lines = new[]
            {
                "You||I checked the inbox on my computer. Most of the new messages are ads from strangers: free prizes, requests for card details and strange senders.",
                "IT Expert||Did you open any of them, or reply?",
                "You||No. I only looked at them.",
                "IT Expert||Good. That is spam: unwanted bulk mail. We will block those senders and turn on the filter, and I will remind everyone not to reply.",
                "IT Expert||Great job. Never reply to spam, and never share personal data. Report it instead."
            }
        });

        s.idleKeys = new[] { Intern, Secretary, ItFemale, ItMale };
        s.idleLines = new[]
        {
            "Still waiting for this printer... and my inbox is a mess!",
            "Please take that coffee to the IT expert before it gets cold.",
            "Hi! Come and talk to me when you have looked through the inbox.",
            "Hi! Come and talk to me when you have looked through the inbox."
        };

        s.rosterTitle = "YOUR TABLET LISTS WHAT TO LOOK FOR IN SPAM MAIL";
        s.rosterLines = new[]
        {
            "Normal mail comes from company addresses (@company.com).",
            "Tap the messages that show the evidence."
        };
        s.tableHeader = new[] { "FROM", "SUBJECT", "TIME" };
        s.tableWidths = new[] { 200f, 300f, 60f, 74f };

        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "prizes@mega-rewards.biz", "YOU WON a FREE phone! Claim your prize now!!!", "03:12 AM" },
            objective = 0, callout = "Unrealistic Reward Offer"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "gifts@promo-hub.net", "Send your card number and password for a gift", "03:40 AM" },
            objective = 1, callout = "Personal Information Request"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "x7k2q9@free-offers4u.xyz", "Important notice about your mailbox", "04:05 AM" },
            objective = 2, callout = "Suspicious Sender Address"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "maria.santos@company.com", "Meeting moved to 3 PM today", "08:15 AM" },
            hint = "A normal message from a coworker: company address, ordinary subject."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "it.support@company.com", "Scheduled system maintenance tonight", "08:30 AM" },
            hint = "A normal message from the company: company address, no offer, no request for data."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "hr@company.com", "Holiday schedule for December", "09:00 AM" },
            hint = "A normal message from the company: company address, ordinary subject."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 3;
        s.verdictFeedback = new[]
        {
            "Phishing pretends to be one trusted sender to steal your information. This is a flood of unwanted ads from strangers.",
            "No file or attachment is involved. These are messages in the inbox.",
            "Nothing popped up on a screen. These are emails.",
            "",
            "Nobody is tricking a person face to face or on the phone. These are mass emails.",
            "Nobody is logging in to anything here. These are unwanted emails.",
            "A normal inbox does not get hundreds of prize offers from strangers overnight."
        };
        s.verdictRight = "Correct. A flood of unwanted ads from strangers with unrealistic prizes and requests for your card details: that is spam.";
        s.verdictWrong = "Not quite. Compare the inbox with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Open every email to see which ones are real",
            "Reply to the senders and ask them to stop",
            "Mark them as spam without opening them, and tell IT",
            "Forward the ads to all my coworkers as a warning"
        };
        s.responseFeedback = new[]
        {
            "Opening spam can show the sender that your address works, and some links are dangerous. Do not open them.",
            "A reply tells the sender your address is real, so you get even more spam.",
            "Correct. Marking them as spam teaches the filter, and IT can block the senders.",
            "Forwarding spreads it to everyone. Tell IT instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Spam Mail");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 5: Prize Giveaway Scam (Spam Email).
    // The intern is thrilled about a "prize" and hands you her entry form -> show it to the IT expert at his desk -> read the giveaway
    // email on the IT room terminal -> report to the secretary, who warns the whole company.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario PrizeGiveaway()
    {
        const string Intern = "mesh_office_f_intern01";
        const string Secretary = "mesh_office_m_secretary01";
        const string ItFemale = "mesh_office_f_it01";
        const string ItMale = "mesh_office_m_rival01";

        EmailScenario s = new EmailScenario
        {
            from = "giveaway_team@luckydraw-win.top",
            to = "all-staff@company.com",
            subject = "CONGRATULATIONS!!! You are our grand prize winner",
            documentTitle = "IT Terminal  -  Mail Sample",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in this email"
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Meet the intern",
            hint = "Find the intern. She has big news.",
            target = Intern, label = "INTERN", speaker = "Intern", item = "Entry Form", prompt = "Talk",
            lines = new[]
            {
                "Guess what?! I won a giveaway! A brand new car and ten thousand dollars!",
                "I already filled in the entry form with my name, my birthday and my bank account number. I only have to send it.",
                "Wait... my friend said it might be a scam. Could you take the form to IT and ask if it is safe?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Show the form to IT",
            hint = "Take the entry form to the IT expert at his desk.",
            target = ItMale, label = "IT EXPERT", speaker = "IT Expert", item = "Entry Form", prompt = "Give Form",
            lines = new[]
            {
                "Let me see this form... it asks for a full name, a birthday and a bank account number.",
                "No real giveaway needs your bank account just to win. Good thing it has not been sent!",
                "The original email is on the terminal over there. Please look through it. Your tablet lists what to look for in Spam Mail."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to the terminal in the IT room and read the giveaway email.",
            computer = "terminal", target = ItMale, label = "IT TERMINAL", prompt = "Read Email"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the secretary and report what you found.",
            target = Secretary, label = "SECRETARY", speaker = "Secretary", prompt = "Report Threat",
            lines = new[]
            {
                "You||I read the giveaway email. It offers a car and cash, asks for a full name, a birthday and a bank account number, and comes from a strange address.",
                "Secretary||Oh no, that is the same one the intern wanted to answer! Did she send anything?",
                "You||No, she only filled in the form. I took it to IT, so nothing was sent.",
                "Secretary||Phew. That is spam, and a dangerous one. I will send a warning to the whole company right now.",
                "Secretary||Thank you for stopping it. A prize you never entered for is never real, and nobody should give out personal details for it."
            }
        });

        s.idleKeys = new[] { Intern, ItMale, Secretary, ItFemale };
        s.idleLines = new[]
        {
            "Please take that form to IT, I want to be sure it is safe!",
            "Come and see me once you have the entry form.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // The three pieces of evidence of the tablet's "Spam Mail", plus plain lines.
        s.lines.Add(new EmailScenario.Line { text = "From: " + s.from, objective = 0, callout = "Suspicious Sender Address" });
        s.lines.Add(new EmailScenario.Line { text = "To: " + s.to, hint = "Nothing wrong with this line." });
        s.lines.Add(new EmailScenario.Line { text = "Subject: " + s.subject, hint = "A loud subject alone does not prove anything." });
        s.lines.Add(new EmailScenario.Line { text = "Dear Lucky Winner,", spaceBefore = true, hint = "Nothing wrong with a greeting." });
        s.lines.Add(new EmailScenario.Line
        {
            text = "You have won a brand new car and $10,000 in cash!", objective = 1, callout = "Unrealistic Reward Offer"
        });
        s.lines.Add(new EmailScenario.Line
        {
            text = "To claim it, reply with your full name, birthday and bank account number.", objective = 2, callout = "Personal Information Request"
        });
        s.lines.Add(new EmailScenario.Line { text = "Your prize will be delivered in 3 to 5 days.", hint = "That just says when it would arrive. Nothing to flag here." });
        s.lines.Add(new EmailScenario.Line { text = "Best regards,", spaceBefore = true, hint = "Nothing wrong with a closing." });
        s.lines.Add(new EmailScenario.Line { text = "The Giveaway Team", hint = "A team name alone does not prove anything." });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 3;
        s.verdictFeedback = new[]
        {
            "Phishing pretends to be a company you trust. This is a mass \"you won\" offer from a stranger: compare it with Spam Mail on your tablet.",
            "There is no file or attachment to open.",
            "This is an email, not a window that suddenly appears on the screen.",
            "",
            "Nobody is tricking a person face to face or by phone. It is a mass email.",
            "Nobody is logging in to anything here. It is an unwanted email.",
            "A real giveaway does not ask for your bank account number to give you a car."
        };
        s.verdictRight = "Correct. An unrealistic prize, a request for your personal details and a strange sender: that is spam.";
        s.verdictWrong = "Not quite. Compare the email with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Send my details to claim the prize before it is gone",
            "Tell my coworkers to enter too, since it is free",
            "Do not reply, delete it, and report it so everyone is warned",
            "Reply with fake details to teach them a lesson"
        };
        s.responseFeedback = new[]
        {
            "That would hand your name, birthday and bank account to a scammer.",
            "Telling others to enter spreads the scam. Warn them instead.",
            "Correct. Not replying keeps you safe, and reporting it lets everyone be warned.",
            "Any reply tells the sender your address is real, and you could be tricked further. Do not reply."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Spam Mail");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 6: Fake IT Support Call (Social Engineering).
    // Listen to the receptionist's phone tips (15 seconds) -> a coworker gives you the number of a strange call -> take it to IT ->
    // question the coworker who took the call (Social Engineering is always done by questioning, like the bank missions) -> decide -> report to
    // the accountant, who warns the finance team.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario FakeSupportCall()
    {
        const string Suspenders = "mesh_office_m_suspenders01";
        const string Accountant = "mesh_office_m_accountant01";
        const string ItFemale = "mesh_office_f_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question your coworker about the call: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT YOUR COWORKER TOLD YOU ABOUT THE CALL",
            interviewOpening = "Oh, it is you. I am still shaking from that phone call. Ask me anything."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the receptionist",
            hint = "Go to the receptionist at the front desk and listen to her phone tips.",
            target = Receptionist, label = "RECEPTIONIST", speaker = "Receptionist", seconds = 15f,
            lines = new[]
            {
                "Quick tip while you are here: I answer many calls, and some callers are not who they say they are.",
                "Some pretend to be IT support or a manager, and ask for passwords or other private information.",
                "Real staff never ask for your password over the phone. If in doubt, hang up and call back on the official number."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Help your coworker",
            hint = "Find the coworker sitting in the kitchen area. He had a strange phone call.",
            target = Suspenders, label = "COWORKER", speaker = "Coworker", item = "Sticky Note", prompt = "Talk",
            lines = new[]
            {
                "Hey, do you have a minute? I just had a really weird phone call.",
                "A man said he was from IT support and needed my password to finish a security update.",
                "I did not give it, but I wrote down the number he called from. Could you take this note to IT?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the note to IT",
            hint = "Give the sticky note to the IT expert.",
            target = ItFemale, label = "IT EXPERT", speaker = "IT Expert", item = "Sticky Note", prompt = "Give Note",
            lines = new[]
            {
                "Thanks for bringing this. Hmm... this number is not one of ours.",
                "Real IT staff would never ask someone for a password over the phone.",
                "Go back to your coworker and question him about the call, to find out exactly what the caller said. Your tablet lists what to look for in Social Engineering.",
                "A question that finds nothing costs you time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question your coworker in the kitchen area about the call. Ask what your tablet lists for Social Engineering.",
            target = Suspenders, label = "QUESTION HIM", speaker = "Coworker", prompt = "Question Him"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the accountant in the meeting room and report what you found.",
            target = Accountant, label = "ACCOUNTANT", speaker = "Accountant", prompt = "Report Threat",
            lines = new[]
            {
                "You||I questioned my coworker about the call. A man claiming to be IT support called from an unknown number and asked for a username and password.",
                "Accountant||Did anyone give it to him?",
                "You||No. My coworker refused, and the number is now with IT.",
                "Accountant||Good. That is social engineering: they pretend to be someone you trust to get your password. I will warn the whole finance team right away.",
                "Accountant||Remember: real IT staff never ask for your password. Hang up and report it."
            }
        });

        s.idleKeys = new[] { Receptionist, Suspenders, ItFemale, Accountant };
        s.idleLines = new[]
        {
            "Please be careful with callers. I have to answer the phone!",
            "Please take that note to IT, I am still shaken up.",
            "Hi! Come and see me with the note from the coworker.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // Six questions. The first three find the tablet's evidence (in the tablet's order), the others find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Could you tell who was really calling?",
            answer = "He said his name was Mike, but the number was unknown, and I have never heard that name in this office. I could not check it.",
            objective = 0, callout = "Unverified Caller Identity", summary = "Unknown number, and a name nobody here has heard: he could not be verified"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What did he ask you for?",
            answer = "My username and my password, to \"verify my account\" for a security update.",
            objective = 1, callout = "Credential Request", summary = "He asked for the username and the password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did he say who he worked for?",
            answer = "IT Support. He said the IT department needed it right now, but he did not know our IT expert by name.",
            objective = 2, callout = "IT Staff Impersonation", summary = "He said he was IT Support but did not know the real IT staff"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What time did he call?",
            answer = "A little after two in the afternoon.",
            note = "The time does not show who he was or what he wanted."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "How long did the call last?",
            answer = "Only a minute or two. I hung up quickly.",
            note = "The length of the call does not show who he was or what he asked."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Was he polite?",
            answer = "Quite polite, and in a hurry. He said it would only take a minute.",
            note = "Being polite or in a hurry is common for a scammer too, and is not on your tablet's list."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. This happened on a phone call.",
            "No file or program is involved. A person called.",
            "Nothing popped up on a screen. Somebody phoned.",
            "Spam is unwanted bulk mail. This was one person on the phone.",
            "",
            "Nobody logged in to anything. The caller tried to trick a person into giving access.",
            "A real IT person never asks for your password on the phone, and never calls from an unknown number."
        };
        s.verdictRight = "Correct. An unknown number, a caller pretending to be IT support and a request for a password: that is social engineering.";
        s.verdictWrong = "Not quite. Compare the call with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Give my username and password so he can finish the update",
            "Ask him to send a reset link to my personal email",
            "Hang up, share nothing, and report the call",
            "Call the number back and give it if he answers"
        };
        s.responseFeedback = new[]
        {
            "That gives a stranger full access to your account. Never share your password.",
            "He could use that link to take over your account. Do not continue the call.",
            "Correct. Hanging up and reporting it protects you and warns everyone else.",
            "Calling back the same number only reaches the same caller. Use the official number, and report it."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 7: Confidential Information Request (Social Engineering).
    // The secretary tells you about odd chat messages (10 seconds) -> the workmate gives you the staff directory -> bring it to the IT
    // expert -> question the workmate about the message he got (Social Engineering is always done by questioning, like the bank missions) ->
    // decide -> report to the Vice President.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario ConfidentialRequest()
    {
        const string VicePresident = "mesh_office_f_boss01";
        const string ItMale = "mesh_office_m_rival01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question your workmate about the message: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT YOUR WORKMATE TOLD YOU ABOUT THE MESSAGE",
            interviewOpening = "Hey. That chat message was so strange. Ask me anything, I only wish I had not almost answered it."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the secretary",
            hint = "Go to the secretary and listen to what she noticed.",
            target = "mesh_office_m_secretary01", label = "SECRETARY", speaker = "Secretary", seconds = 10f,
            lines = new[]
            {
                "Oh, good, you are here. Several people got odd chat messages this morning.",
                "Somebody wrote to them as an IT person and asked for their passwords and for private company files.",
                "Nobody in IT sent those messages. I am worried someone might actually answer."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Meet your workmate",
            hint = "Find your workmate. He received one of those messages.",
            target = Workmate, label = "WORKMATE", speaker = "Workmate", item = "Staff Directory", prompt = "Talk",
            lines = new[]
            {
                "Hey! I got a chat message from a stranger asking for the client list. It said it was urgent.",
                "I checked the staff directory and that name is not in it. I almost sent it, honestly.",
                "Here is the directory. Could you take it to IT? They can check the messages against it."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the directory to IT",
            hint = "Give the staff directory to the IT expert at his desk.",
            target = ItMale, label = "IT EXPERT", speaker = "IT Expert", item = "Staff Directory", prompt = "Give Directory",
            lines = new[]
            {
                "Good, that helps. If a sender is not in the directory, they are not one of us.",
                "The message was sent to your workmate, so he is the witness. Go back and question him about what the sender said.",
                "Your tablet lists what to look for in Social Engineering. A question that finds nothing costs you time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question your workmate about the message he received. Ask what your tablet lists for Social Engineering.",
            target = Workmate, label = "QUESTION HIM", speaker = "Workmate", prompt = "Question Him"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Vice President and report what you found.",
            target = VicePresident, label = "VICE PRESIDENT", speaker = "Vice President", prompt = "Report Threat",
            lines = new[]
            {
                "You||I questioned my workmate about the chat message. A stranger pretending to be IT support asked for passwords and for the client list.",
                "Vice President||Did anyone reply with anything?",
                "You||No. My workmate checked the directory first, and the senders are not in it.",
                "Vice President||Good. That is social engineering. I will tell every department to verify who is asking before sharing anything.",
                "Vice President||Well done. Nobody should share confidential information with someone they have not verified."
            }
        });

        s.idleKeys = new[] { "mesh_office_m_secretary01", Workmate, ItMale, VicePresident };
        s.idleLines = new[]
        {
            "I hope nobody answers those messages!",
            "Please take the staff directory to IT.",
            "Come and see me once you have the directory.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // Six questions. The first three find the tablet's evidence (in the tablet's order), the others find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Who sent the message? Is the sender in the staff directory?",
            answer = "Just \"Support\". I checked the staff directory and nobody by that name is in it. Nobody here knows who it is.",
            objective = 0, callout = "Unverified Caller Identity", summary = "A sender called \"Support\" that is not in the staff directory"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What did the message ask you for?",
            answer = "The client list, and then my password \"to confirm that I am a real employee\".",
            objective = 1, callout = "Credential Request", summary = "The message asked for the client list and for the password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did the sender say he works in IT?",
            answer = "Yes: \"Mike, IT Manager\". But our IT manager is somebody else, and IT never writes to people like that.",
            objective = 2, callout = "IT Staff Impersonation", summary = "He signed as \"Mike, IT Manager\", but the real IT manager is someone else"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What time did the message arrive?",
            answer = "A little before ten, I think.",
            note = "The time does not show who he was or what he wanted."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Was the message long?",
            answer = "Just three lines. Short and pushy.",
            note = "The length of the message does not show who sent it or what it asked."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did you reply to it?",
            answer = "No, I checked the directory first. That is why I got the directory for you.",
            note = "A good instinct, but it does not show who the sender was, what he asked or whose name he used."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email with a link. These are chat messages from strangers.",
            "No file or program is involved. These are messages.",
            "Nothing popped up on a screen as a window. These are chat messages.",
            "Spam is bulk mail to everyone. These messages target people with a story.",
            "",
            "Nobody logged in to an account. The senders tried to trick people into handing information over.",
            "Strangers pretending to be IT and asking for passwords and files is not normal."
        };
        s.verdictRight = "Correct. Unknown senders pretending to be IT and asking for passwords and files: that is social engineering.";
        s.verdictWrong = "Not quite. Compare the messages with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Reply to them and send what they ask for, to be helpful",
            "Check the staff directory, send nothing, and report them",
            "Ignore the messages and tell nobody",
            "Send the client list, but leave out my password"
        };
        s.responseFeedback = new[]
        {
            "That hands private information to strangers. Never share it with someone you have not verified.",
            "Correct. Verifying the sender and reporting it protects everyone.",
            "If nobody is told, others may still answer. Always report it.",
            "The client list is confidential too. Do not send anything to an unverified sender."
        };
        s.responseCorrect = 1;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 8: Suspicious Attachment (Malware Files).
    // The accountant complains about his slow computer (15 seconds) -> the male intern gives you a USB drive -> bring it to the IT
    // expert -> read the antivirus scan report on the IT room terminal (a table) -> report to the Head Manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario SuspiciousAttachment()
    {
        const string ItFemale = "mesh_office_f_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "IT Terminal  -  Antivirus Scan Report",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in this report",
            analysis = AnalysisKind.Table
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the accountant",
            hint = "Go to the accountant and listen to his problem.",
            target = "mesh_office_m_accountant01", label = "ACCOUNTANT", speaker = "Accountant", seconds = 15f,
            lines = new[]
            {
                "Ah, finally someone. My computer has been acting strangely since this morning.",
                "It is very slow, the fan is loud, and programs open by themselves. I do not know why.",
                "All I did was open a file from an email about an invoice. I did not expect any invoice."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Meet the intern",
            hint = "Find the intern. He has a USB drive that he found.",
            target = "mesh_office_m_intern01", label = "INTERN", speaker = "Intern", item = "USB Drive", prompt = "Talk",
            lines = new[]
            {
                "Hi! The accountant gave me this USB drive. He saved the invoice file from his email on it.",
                "Now he says his computer is slow, and I am a little scared to plug it in anywhere.",
                "Could you take it to the IT expert? She will know what to do."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the USB drive to IT",
            hint = "Give the USB drive to the IT expert.",
            target = ItFemale, label = "IT EXPERT", speaker = "IT Expert", item = "USB Drive", prompt = "Give USB",
            lines = new[]
            {
                "Good thing you did not plug it in. I will keep it away from the network.",
                "I scanned the accountant's computer. The report is on the terminal in the IT room.",
                "Please read it and tell me what looks wrong. Your tablet lists what to look for in Malware Files."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to the terminal in the IT room and read the scan report.",
            computer = "terminal", target = ItFemale, label = "IT TERMINAL", prompt = "Read Report"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Head Manager and report what you found.",
            target = Boss, label = "HEAD MANAGER", speaker = "Head Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, I read the scan report. The accountant opened an unexpected file from an unknown sender, and his computer is now running very strangely.",
                "Head Manager||So it was infected. Did it reach any other computers?",
                "You||Not yet. IT has the USB drive and the computer is being kept off the network.",
                "Head Manager||Good. That is malware. I will have the computer cleaned and tell everyone not to open files they did not expect.",
                "Head Manager||Well done. Always check the sender and the file before you open anything."
            }
        });

        s.idleKeys = new[] { "mesh_office_m_accountant01", "mesh_office_m_intern01", ItFemale, Boss };
        s.idleLines = new[]
        {
            "I hope IT can fix my computer soon...",
            "Please take the USB drive to the IT expert.",
            "Come and see me once you have the USB drive.",
            "Stay sharp out there. I am counting on you."
        };

        s.rosterTitle = "YOUR TABLET LISTS WHAT TO LOOK FOR IN MALWARE FILES";
        s.rosterLines = new[]
        {
            "A safe file is expected, from a known sender, and the computer behaves normally.",
            "Tap the rows that show the evidence."
        };
        s.tableHeader = new[] { "ITEM", "DETAILS", "STATUS" };
        s.tableWidths = new[] { 120f, 330f, 110f, 74f };

        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Attachment", "Invoice_2291.pdf.exe  (an .exe file pretending to be a PDF)", "Opened" },
            objective = 0, callout = "Unexpected Attachment"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Sender", "billing@invo1ce-center.top  (not a known supplier)", "Unknown" },
            objective = 1, callout = "Suspicious Sender Address"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Computer", "CPU 98% and unknown program running, fan at full speed", "Abnormal" },
            objective = 2, callout = "Unusual Computer Behavior"
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Windows Update", "Installed this week from the official update service", "Normal" },
            hint = "A normal update from the official service. Nothing wrong with it."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Office Suite", "Opens and saves documents in the usual time", "Normal" },
            hint = "The office programs behave normally."
        });
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Last Backup", "Completed last night without errors", "Normal" },
            hint = "A normal backup. Nothing wrong with it."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 1;
        s.verdictFeedback = new[]
        {
            "Phishing tries to get information by a fake link or form. Here a file was opened and the computer is now affected.",
            "",
            "Nothing popped up as a window. Look at the file and the computer's behavior.",
            "Spam is unwanted bulk mail. The problem here is a harmful file.",
            "Nobody was tricked on the phone or face to face. A file did this.",
            "Nobody logged in to anything. A file infected a computer.",
            "A fake PDF that is really an .exe and a computer running wild are not normal."
        };
        s.verdictRight = "Correct. An unexpected file, an unknown sender and a computer that behaves strangely: that is malware.";
        s.verdictWrong = "Not quite. Compare the report with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Keep working on the computer, it is probably just slow",
            "Plug the USB drive into my own computer to look at the file",
            "Disconnect it from the network and report it to IT",
            "Forward the invoice to my coworkers to check if it opens for them"
        };
        s.responseFeedback = new[]
        {
            "Malware keeps working while you do. Waiting lets it spread and steal more.",
            "That would infect your computer too. Never plug in or open a suspicious file.",
            "Correct. Disconnecting stops it spreading, and IT can clean the computer.",
            "Forwarding spreads the malware to everyone. Report it to IT instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Malware Files");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 9: Emergency Meeting Invitation (Phishing Email).
    // The workmate mentions the strange invitation (10 seconds) -> the receptionist gives you the printed invite -> bring it to the IT
    // expert -> read the invitation email at your computer -> report to the Vice President.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario EmergencyMeeting()
    {
        EmailScenario s = new EmailScenario
        {
            from = "ceo-office@company-meetings.co",
            to = "all-staff@company.com",
            subject = "EMERGENCY meeting today - confirm NOW",
            documentTitle = "Inbox  -  Meeting Invitation",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in this email"
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to your workmate",
            hint = "Go to your workmate and listen to what he says.",
            target = Workmate, label = "WORKMATE", speaker = "Workmate", seconds = 10f,
            lines = new[]
            {
                "Did you see the invitation to an emergency meeting? It says everyone must confirm right away.",
                "It is from the CEO's office, but I never heard of an emergency meeting today.",
                "I did not click anything. It felt strange to me."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Visit the reception",
            hint = "Go to the receptionist. She printed the invitation.",
            target = Receptionist, label = "RECEPTIONIST", speaker = "Receptionist", item = "Printed Invite", prompt = "Talk",
            lines = new[]
            {
                "Oh, you are here! Several people asked me about this emergency meeting.",
                "I printed the invitation, but there is no room booked and nobody at the office knows about it.",
                "Could you take it to the IT experts and ask if it is real?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Show the invite to IT",
            hint = "Give the printed invite to an IT expert.",
            target = ItExperts, label = "IT EXPERT", speaker = "IT Expert", item = "Printed Invite", prompt = "Give Invite",
            lines = new[]
            {
                "Let me see... the sender address does not look like our company address.",
                "I copied the original email to your computer. Please read it carefully.",
                "Your tablet lists what to look for in Phishing Email."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer and read the invitation email.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Read Email"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Vice President and report what you found.",
            target = "mesh_office_f_boss01", label = "VICE PRESIDENT", speaker = "Vice President", prompt = "Report Threat",
            lines = new[]
            {
                "You||I read the meeting invitation. It comes from a strange address, pushes everyone to confirm at once and has a suspicious link.",
                "Vice President||I never called an emergency meeting. Did anyone click the link?",
                "You||No. My workmate and the receptionist both stopped and asked first.",
                "Vice President||Good. That is phishing. I will tell everyone the meeting is not real and not to click anything.",
                "Vice President||Well done. A message that pressures you to act fast deserves a second look."
            }
        });

        s.idleKeys = new[] { Workmate, Receptionist, ItExperts, "mesh_office_f_boss01" };
        s.idleLines = new[]
        {
            "Weird, right? I hope nobody clicks that.",
            "Please take the invite to IT when you can.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        s.lines.Add(new EmailScenario.Line { text = "From: " + s.from, objective = 0, callout = "Suspicious Sender Address" });
        s.lines.Add(new EmailScenario.Line { text = "To: " + s.to, hint = "Nothing wrong with this line." });
        s.lines.Add(new EmailScenario.Line { text = "Subject: " + s.subject, hint = "A loud subject alone does not prove anything. Look at the message itself." });
        s.lines.Add(new EmailScenario.Line { text = "Dear Team,", spaceBefore = true, hint = "Nothing wrong with a greeting." });
        s.lines.Add(new EmailScenario.Line { text = "An emergency meeting has been called for 4:00 PM today.", hint = "That just states the meeting. Nothing to flag here." });
        s.lines.Add(new EmailScenario.Line
        {
            text = "Confirm now or your access will be suspended in 30 minutes!", objective = 1, callout = "Urgent or Threatening Language"
        });
        s.lines.Add(new EmailScenario.Line
        {
            text = "Confirm here: http://company-meetings.co/confirm-login", link = true, objective = 2, callout = "Suspicious Link"
        });
        s.lines.Add(new EmailScenario.Line { text = "Regards,", spaceBefore = true, hint = "Nothing wrong with a closing." });
        s.lines.Add(new EmailScenario.Line { text = "Office of the CEO", hint = "A name alone does not prove anything." });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 0;
        s.verdictFeedback = new[]
        {
            "",
            "There is no file or attachment to open.",
            "This is an email, not a window that suddenly appears on the screen.",
            "Spam is mass advertising. This one pretends to be a real invitation from your company.",
            "Nobody is tricking a person face to face or by phone. It is an email.",
            "Nobody is logging in to anything here. It is a fake email asking to log in.",
            "A real emergency meeting does not come from a strange address with a threat and a link."
        };
        s.verdictRight = "Correct. A strange sender address, pressure to act fast and a suspicious link: that is phishing.";
        s.verdictWrong = "Not quite. Compare the email with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Click the link quickly so my access is not suspended",
            "Reply to ask the sender if the meeting is real",
            "Do not click anything, and report the email to IT",
            "Forward the invitation to my team so they confirm too"
        };
        s.responseFeedback = new[]
        {
            "The link leads to a fake login page that steals your password. Never click it.",
            "A reply confirms your address to the sender. Report it instead.",
            "Correct. Not clicking and reporting protects you and lets IT warn everyone.",
            "Forwarding spreads the phishing email. Report it to IT instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Phishing Email");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Office, mission 10: The Urgent Executive Request (Social Engineering) - the finale.
    // The Vice President explains the "CEO" calls (20 seconds) -> the secretary gives you her call notes -> bring them to the IT
    // expert -> question the secretary about the calls she took (Social Engineering is always done by questioning, like the bank missions) ->
    // decide -> report to the Head Manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario UrgentExecutive()
    {
        const string VicePresident = "mesh_office_f_boss01";
        const string Secretary = "mesh_office_m_secretary01";
        const string ItFemale = "mesh_office_f_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question the secretary about the calls: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT THE SECRETARY TOLD YOU ABOUT THE CALLS",
            interviewOpening = "Oh, thank goodness. Those calls were awful. Ask me anything you need."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the Vice President",
            hint = "Go to the Vice President and listen to her.",
            target = VicePresident, label = "VICE PRESIDENT", speaker = "Vice President", seconds = 20f,
            lines = new[]
            {
                "I am glad you are here. This morning, people in the office got calls from someone saying the CEO needed urgent help.",
                "The caller said the CEO was traveling, could not reach his own accounts, and that staff must give their login details at once.",
                "The real CEO is in a meeting and never asked for any of this. Somebody is using his name to pressure us.",
                "This is the biggest attempt so far. Please help us find out how it works."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Visit the secretary",
            hint = "Go to the secretary. She wrote down the calls she took.",
            target = Secretary, label = "SECRETARY", speaker = "Secretary", item = "Call Notes", prompt = "Talk",
            lines = new[]
            {
                "I took three of those calls myself. I wrote down the time, what the caller said and what he asked for.",
                "One said he was the CEO, one said he was from IT sent by the CEO, and one just said it was urgent.",
                "Could you take my notes to the IT expert? She can compare them with the phone log."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Bring the notes to IT",
            hint = "Give the call notes to the IT expert.",
            target = ItFemale, label = "IT EXPERT", speaker = "IT Expert", item = "Call Notes", prompt = "Give Notes",
            lines = new[]
            {
                "Perfect, these match the entries in our phone system.",
                "The best witness is the secretary, who took three of the calls herself. Go back and question her about what the callers said.",
                "Your tablet lists what to look for in Social Engineering. A question that finds nothing costs you time, so choose carefully. This is the last one."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question the secretary about the calls she took. Ask what your tablet lists for Social Engineering.",
            target = Secretary, label = "QUESTION HER", speaker = "Secretary", prompt = "Question Her"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the Head Manager and report what you found.",
            target = Boss, label = "HEAD MANAGER", speaker = "Head Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, I questioned the secretary about the calls. The callers could not be verified, pretended to be the CEO and IT, and asked for logins.",
                "Head Manager||Did anyone give their login details?",
                "You||No. Everyone doubted it, and the secretary wrote everything down.",
                "Head Manager||Excellent. That is social engineering: they use a powerful name to rush people. I will announce to everyone that nobody asks for passwords over the phone, not even the CEO.",
                "Head Manager||You have helped keep this company safe. Well done on every mission."
            }
        });

        s.idleKeys = new[] { VicePresident, Secretary, ItFemale, Boss };
        s.idleLines = new[]
        {
            "Please help us find out how this works.",
            "Please take my call notes to IT.",
            "Come and see me once you have the call notes.",
            "Stay sharp out there. I am counting on you."
        };

        // Six questions. The first three find the tablet's evidence (in the tablet's order), the others find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Could you tell who \"the CEO\" really was?",
            answer = "No. It was an unknown number, and the voice did not sound like the CEO. I had no way of checking.",
            objective = 0, callout = "Unverified Caller Identity", summary = "\"The CEO\" called from an unknown number and could not be verified"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What did the callers ask you for?",
            answer = "One wanted my login and my password \"to help the CEO\". They all wanted something private, and at once.",
            objective = 1, callout = "Credential Request", summary = "One caller asked for her login and her password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did anyone say they were from IT?",
            answer = "Yes: \"IT Support, sent by the CEO\". But when I asked, his name was not on our IT staff list.",
            objective = 2, callout = "IT Staff Impersonation", summary = "A caller said \"IT Support, sent by the CEO\", but he is not on the IT staff list"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "How many calls did you take?",
            answer = "Three, one after another, within an hour.",
            note = "The number of calls does not show who the callers were or what they asked."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What time was the first call?",
            answer = "A little before nine.",
            note = "The time does not show who the caller was or what he wanted."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did the callers sound angry?",
            answer = "Pushy, mostly. Always in a hurry.",
            note = "A pushy tone is common, but it is not on your tablet's list."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email with a link. These were phone calls.",
            "No file or program is involved. These were calls.",
            "Nothing popped up on a screen. People phoned.",
            "Spam is unwanted bulk mail. These calls targeted people with a story.",
            "",
            "Nobody logged in to anything yet. The callers tried to trick people into handing over their access.",
            "Callers using the CEO's name, claiming to be IT and asking for logins are not normal."
        };
        s.verdictRight = "Correct. Unknown callers using the CEO's name and pretending to be IT to get logins: that is social engineering.";
        s.verdictWrong = "Not quite. Compare the calls with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Give the login details, because the CEO asked for them",
            "Call back on the number the caller gave to check",
            "Share nothing, verify with the real CEO's office, and report it",
            "Give only my username and keep the password"
        };
        s.responseFeedback = new[]
        {
            "Even a real CEO never asks for your password. Using his name is the trick.",
            "The number belongs to the caller, so you reach the same attacker. Use an official contact.",
            "Correct. Verifying through official channels and reporting stops the trick for everyone.",
            "A username is part of a login. Give nothing to an unverified caller."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 1: Fake Security Alert (Fake Pop-up Windows).
    // The teller at the third counter complains about flashing warnings (12 seconds) -> a customer at the middle counter gives you the note he
    // wrote down from a scary window on the bank's kiosk -> take it to the IT officer -> go through the pop-ups on YOUR computer (the middle
    // counter) -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario FakeSecurityAlert()
    {
        const string TellerThree = "mesh_office_f_secretary01";
        const string Customer = "mesh_office_m_black01";
        const string ItOfficer = "mesh_office_f_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Pop-up Windows",
            investigateQuestion = "Find the 3 pieces of evidence from your TABLET in these pop-ups",
            analysis = AnalysisKind.Board
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Tablet, side = true, heading = "Get your tablet",
            hint = "Go to the bank manager. He has your Cyber Tablet for you.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Take Tablet",
            lines = new[]
            {
                "Welcome to the bank. You must be our new cybersecurity officer. I have been expecting you.",
                "This is your Cyber Tablet. It is your investigation tool: it lists every kind of threat, and the three signs to look for in each one.",
                "Keep it with you. Tap the TABLET button any time to open it. We are having a strange morning, so it will be useful today."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the third counter and listen to her.",
            target = TellerThree, label = "TELLER", speaker = "Teller", seconds = 12f,
            lines = new[]
            {
                "Oh, good, you are here. All morning my screen has been flashing red windows.",
                "They say the computer is infected and that I must call a number or download a fix right away.",
                "I have not clicked anything. Customers are watching, and I do not know what is safe."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the customer at the middle counter. He saw a strange window on the bank kiosk.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Warning Note", prompt = "Talk",
            lines = new[]
            {
                "Excuse me, I was at the bank kiosk and a big red window appeared. It said the bank computers had a virus.",
                "It told me to call a number and to click a green button to clean it. I did not, but I copied the message on this note.",
                "Could you take it to the IT officer? I would not want anybody else to fall for it."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the note to IT",
            hint = "Give the note to the IT officer in the back office.",
            target = ItOfficer, label = "IT OFFICER", speaker = "IT Officer", item = "Warning Note", prompt = "Give Note",
            lines = new[]
            {
                "Thank you. That wording is not from our system. A real bank alert never tells you to call a number.",
                "I copied the pop-ups from the counters to your computer, the one at the middle counter. Please look through them.",
                "Your tablet lists what to look for in Pop-up Windows."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and check the pop-ups.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Check Pop-ups"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||I checked the pop-ups. Some said our computers are infected, asked people to download a fix, and had a button that leads somewhere else.",
                "Bank Manager||Did anyone click them or call that number?",
                "You||No. The teller and the customer both stopped, and the note is with IT.",
                "Bank Manager||Good. Those are fake pop-up windows. I will tell every counter to close them without clicking, and IT will block the source.",
                "Bank Manager||Well done. A bank never warns you through a window that tells you to call or download something."
            }
        });

        s.idleKeys = new[] { TellerThree, Customer, ItOfficer, Boss };
        s.idleLines = new[]
        {
            "Please help me with these windows!",
            "Please take the note to IT, I hope it is useful.",
            "Come and see me once you have the note.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        s.rosterTitle = "YOUR TABLET LISTS WHAT TO LOOK FOR IN POP-UP WINDOWS";
        s.rosterLines = new[]
        {
            "A real alert comes from the bank's own system and never asks you to call, download or click an unknown button.",
            "Tap the pop-ups that show the evidence."
        };

        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #1", fields = new[] { "\"WARNING! This computer", "is infected with 7 viruses!\"", "Call 1-800-555-0199 now" },
            objective = 0, callout = "Fake Security Warnings"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #2", fields = new[] { "\"Your system is out of date.\"", "[ Download SecureFix_Bank.exe ]", "Install now to stay protected" },
            objective = 1, callout = "Suspicious Download Prompt"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #3", fields = new[] { "\"Account locked! Verify now\"", "[ CLEAN NOW ]  (the link goes to", "bank-verify.top, not our bank)" },
            objective = 2, callout = "Misleading Link or Button"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #4", fields = new[] { "Printer: paper tray is low", "Please add paper to Tray 2", "[ OK ]" },
            hint = "A normal message from the office printer. It claims nothing and asks for nothing."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #5", fields = new[] { "Calendar reminder", "Branch meeting at 3:00 PM", "[ Dismiss ]" },
            hint = "A normal reminder from the calendar. It claims nothing and asks for nothing."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "POP-UP #6", fields = new[] { "Update (from the bank's IT)", "Restart tonight at 11:00 PM", "[ Remind me later ]" },
            hint = "A normal update notice from the bank's own IT. It asks for no download and no call."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 2;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. These are windows that pop up on the screen.",
            "No file was opened or infected yet. The danger here is a window that tries to make you download one.",
            "",
            "Spam is bulk mail. These are windows on a screen, not messages in an inbox.",
            "Nobody is tricking a person face to face or on the phone. The windows push you to call, but the trick is on the screen.",
            "Nobody is logging in to an account here. These are windows on a screen.",
            "Windows that say the computer is infected and tell you to call or download something are not normal."
        };
        s.verdictRight = "Correct. A fake virus warning, a download prompt and a misleading button: those are fake pop-up windows.";
        s.verdictWrong = "Not quite. Compare the windows with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Call the number in the window to clear the viruses",
            "Click the green CLEAN NOW button to get rid of them",
            "Close the window without clicking inside it, and tell IT",
            "Download the fix to be on the safe side"
        };
        s.responseFeedback = new[]
        {
            "The number reaches the scammers, who will ask for access or money. Never call it.",
            "The button is a trap: it leads to a fake website or installs something harmful.",
            "Correct. Closing it without clicking keeps you safe, and IT can block it for everyone.",
            "That file is probably malware. Never download from a pop-up."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Pop-up Windows");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 2: Suspicious Bank Attachment (Malware Files).
    // A different way to investigate: nobody puts the evidence on your screen. You walk around the bank and INSPECT its computers and machines
    // yourself (hand button), and decide for each one: flag it as evidence, or clear it as normal. Five places, three are infected (the tablet's
    // three pieces of evidence), two are normal. Only afterwards do you go to your computer, to read the incident report, name the threat and
    // choose the response.
    // Teller at counter 1 tells what happened (10 seconds) -> the supplier waiting in the armchair gives you a note -> take it to the IT
    // officer, who sends you to inspect -> inspect -> your computer (report) -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario SuspiciousBankAttachment()
    {
        const string TellerOne = "mesh_office_f_recepcionist01";
        const string TellerThree = "mesh_office_f_secretary01";
        const string Supplier = "mesh_office_m_slacker01";
        const string ItOfficer = "mesh_office_m_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Incident Report",
            investigateQuestion = "Inspect the bank's machines: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the first counter and listen to her.",
            target = TellerOne, label = "TELLER", speaker = "Teller", seconds = 10f,
            lines = new[]
            {
                "Good morning. Something odd happened today: an invoice came in by email, and a few minutes after somebody opened it, computers started acting up.",
                "The manager's computer is crawling, and nobody can say where the invoice came from.",
                "I am afraid to touch anything else until IT has looked."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the supplier",
            hint = "Find the man waiting in the armchair near the first counter. He is a supplier of the bank.",
            target = Supplier, label = "SUPPLIER", speaker = "Supplier", item = "Supplier Note", prompt = "Talk",
            lines = new[]
            {
                "Hello. I deliver office supplies to this bank. The teller says an invoice came in under my company's name today.",
                "I never sent one, and my address does not look like that. Here, I wrote down what I know.",
                "Please give this note to the IT officer. It might help."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the note to IT",
            hint = "Give the note to the IT officer, who is waiting on the sofa in the manager's room.",
            target = ItOfficer, label = "IT OFFICER", speaker = "IT Officer", item = "Supplier Note", prompt = "Give Note",
            lines = new[]
            {
                "Thank you. So the invoice was not from your supplier. Somebody got a harmful file onto the bank's computers.",
                "I cannot check every machine at once. Walk around and inspect the computers and machines yourself, with the hand button.",
                "Your tablet lists the 3 pieces of evidence for Malware Files. Flag only what is really wrong: a wrong guess costs you time."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Inspect, side = false,
            hint = "Inspect the bank's computers and machines (hand button). Flag the 3 pieces of evidence your tablet lists for Malware Files.",
            label = "INSPECT", prompt = "Inspect"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and read the incident report.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Read Report"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||I inspected the computers. An invoice from a strange address was opened as a program, and the manager's computer is running an unknown program that eats the CPU.",
                "Bank Manager||So it is infected. Did it reach other machines?",
                "You||Not that I could find. The counter machines and the ATM are clean, and the infected computer will be taken off the network.",
                "Bank Manager||Good. That is malware. IT will clean it and I will remind everyone never to open a file they did not expect.",
                "Bank Manager||Well done. Always check the sender and the file before you open anything."
            }
        });

        s.idleKeys = new[] { TellerOne, Supplier, ItOfficer, Boss, TellerThree };
        s.idleLines = new[]
        {
            "I do not want to touch anything else before IT looks.",
            "Please give my note to the IT officer.",
            "Come and see me once you have the note.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // The five places. Three show the tablet's evidence (the objective numbers follow the tablet: attachment, sender, behavior).
        s.stations.Add(new EmailScenario.Station
        {
            locate = "pc@" + TellerOne, title = "Counter 1 computer  -  Downloads folder",
            lines = new[]
            {
                "Invoice_4471.pdf.exe        opened 09:12 AM",
                "Type: Application. The name says PDF, but it is a program.",
                "Nobody at counter 1 was expecting an invoice."
            },
            objective = 0, callout = "Unexpected Attachment", summary = "File: Invoice_4471.pdf.exe (a program pretending to be a PDF)",
            hint = "A real invoice would be a document, not a program. Look at the file name again."
        });
        s.stations.Add(new EmailScenario.Station
        {
            locate = "pc@" + TellerThree, title = "Counter 3 computer  -  Inbox",
            lines = new[]
            {
                "Inbox (3 new messages)",
                "From: billing@invo1ce-bank.top",
                "Subject: Invoice 4471 - open today",
                "The bank's own mail always ends in @ourbank.com."
            },
            objective = 1, callout = "Suspicious Sender Address", summary = "Sender: billing@invo1ce-bank.top (not @ourbank.com)",
            hint = "Read the sender address again: does it end the way the bank's own mail does?"
        });
        s.stations.Add(new EmailScenario.Station
        {
            locate = "pc@" + Boss, title = "Manager's computer  -  Task Manager",
            lines = new[]
            {
                "CPU 98%        Memory 91%",
                "Unknown program: svc_update32.exe   (87% of the CPU)",
                "The fan is at full speed and the screen freezes for seconds."
            },
            objective = 2, callout = "Unusual Computer Behavior", summary = "Manager's PC: CPU 98%, unknown program svc_update32.exe",
            hint = "Look at how the computer is behaving: is that normal?"
        });
        s.stations.Add(new EmailScenario.Station
        {
            locate = "thing@Cash_Machine_01@" + TellerOne, title = "Cash counter  -  Counter 1",
            lines = new[]
            {
                "Counted today: 1,250 bills",
                "Status: OK, no errors",
                "Software: factory version, no updates waiting."
            },
            hint = "This machine works normally: no strange file, no strange sender, no strange behavior."
        });
        s.stations.Add(new EmailScenario.Station
        {
            locate = "name@ATM_03", title = "ATM 03  -  Transaction log",
            lines = new[]
            {
                "Withdrawals today: 38 (normal)",
                "Status: online, no errors",
                "Last service: 3 days ago."
            },
            hint = "This machine works normally: no strange file, no strange sender, no strange behavior."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 1;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email that steals what you type. Here a harmful file was opened and a computer is now affected.",
            "",
            "Nothing popped up as a window. Look at the file and at how the computer behaves.",
            "Spam is bulk mail. This is one harmful file that infected a computer.",
            "Nobody was tricked on the phone or face to face. A file did this.",
            "Nobody logged in to an account. A file infected a computer.",
            "A program pretending to be a PDF and a computer running an unknown program are not normal."
        };
        s.verdictRight = "Correct. An unexpected file, an unknown sender and a computer that behaves strangely: that is malware.";
        s.verdictWrong = "Not quite. Compare your evidence with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Keep using the infected computer to finish the day",
            "Open the invoice file on another computer to look closer",
            "Disconnect the computer from the network and report it to IT",
            "Delete the email and tell nobody"
        };
        s.responseFeedback = new[]
        {
            "Malware keeps working while you do. Waiting lets it spread and steal more.",
            "That would infect a second computer. Never open a suspicious file.",
            "Correct. Disconnecting stops it spreading, and IT can clean the computer.",
            "The infected computer is still running. Always report it."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Malware Files");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 3: Unverified Customer Request (Social Engineering).
    // A third way to investigate: you QUESTION the suspect yourself. Nothing is on a screen and nothing is a place to inspect: you choose what to
    // ask him from six questions. Three of them probe exactly what the tablet lists for Social Engineering (identity, the credentials he asks
    // for, his claim to be IT staff); the other three find nothing and cost time. When all three answers are in, the decision (what kind of
    // threat, which response) opens by itself.
    // Teller at counter 3 tells what she saw (12 seconds) -> the customer in the armchair gives you the visitor log -> take it to the IT officer
    // -> question the man at the middle counter -> decide -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario UnverifiedCustomer()
    {
        const string TellerThree = "mesh_office_f_secretary01";
        const string Waiting = "mesh_office_f_black01";
        const string ItWest = "mesh_office_f_it01";
        const string Suspect = "mesh_office_m_black01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question him: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT HE SAID WHEN YOU QUESTIONED HIM",
            interviewOpening = "Hello. I am from the bank's IT department. I need a minute of your teller's time, it is urgent."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the third counter and listen to her.",
            target = TellerThree, label = "TELLER", speaker = "Teller", seconds = 12f,
            lines = new[]
            {
                "Oh, thank goodness. See the man at the middle counter? He says he is from our IT department.",
                "I have never seen him before, and he keeps asking for something from her computer.",
                "I did not want to start a scene. Could you find out who he really is?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the woman waiting in the armchair near the third counter.",
            target = Waiting, label = "CUSTOMER", speaker = "Customer", item = "Visitor Log", prompt = "Talk",
            lines = new[]
            {
                "Hi. I am just waiting for my turn, but that man at the middle counter made me uneasy. He told me he was \"bank IT\" and asked to borrow my phone.",
                "I looked at the visitor log by the door. His name is not on the staff list, and he did not sign in as a visitor either.",
                "Here is the log. Could you give it to the IT officer? She will know whether he is real."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the log to IT",
            hint = "Give the visitor log to the IT officer in the back office.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", item = "Visitor Log", prompt = "Give Log",
            lines = new[]
            {
                "Thank you. We have no IT staff matching what you describe, so he is not who he says he is.",
                "Go and question him yourself, but ask the right questions. Your tablet lists the 3 things to look for in Social Engineering.",
                "Every question that finds nothing wastes time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question the man at the middle counter. Ask what your tablet lists for Social Engineering.",
            target = Suspect, label = "QUESTION HIM", speaker = "Suspect", prompt = "Question Him"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||I questioned the man at the middle counter. He claimed to be from IT, could not show any ID, could not name his IT manager and asked the teller for her login and password.",
                "Bank Manager||Did she give it to him?",
                "You||No. My report and the visitor log are with IT, and he has not been given anything.",
                "Bank Manager||Good. That is social engineering: he pretended to be staff to get her credentials. I will have him escorted out and warn every counter.",
                "Bank Manager||Well done. Nobody gets a login without being verified first."
            }
        });

        s.idleKeys = new[] { TellerThree, Waiting, ItWest, Boss, Suspect };
        s.idleLines = new[]
        {
            "Please find out who he really is.",
            "Please give the log to the IT officer.",
            "Come and see me once you have the log.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Hello. Can we do this quickly?"
        };

        // Six questions. The first three find the tablet's evidence (in the tablet's order), the others find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "May I see your staff ID, please?",
            answer = "Er... I left it in the car. Look, I am in a hurry. Just trust me, everyone knows me at head office.",
            objective = 0, callout = "Unverified Caller Identity", summary = "He could not show any ID: \"I left it in the car\""
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What do you need from the teller?",
            answer = "Only her login and password, so I can fix the system. It takes two minutes, I promise.",
            objective = 1, callout = "Credential Request", summary = "He asked the teller for her login and password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Who is your manager in the IT department?",
            answer = "My manager? Uh... somebody at head office. I do not remember the name right now.",
            objective = 2, callout = "IT Staff Impersonation", summary = "He says he is IT staff but cannot name his IT manager"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "How are you today?",
            answer = "Fine, fine. Can we hurry, please?",
            note = "That is polite, but it tells you nothing about who he is."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like a glass of water?",
            answer = "No, thank you. Water is not why I am here.",
            note = "A kind offer, but it finds no evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Which counter has the shortest line?",
            answer = "I do not care. I am not here to do any banking.",
            note = "That tells you nothing about his identity, his request or his claim."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. Nothing was sent: a man talked to the staff.",
            "No file or program is involved. A person asked for something.",
            "Nothing popped up on a screen. A man spoke to the tellers.",
            "Spam is bulk mail. This is one person at the counter.",
            "",
            "Nobody has logged in to anything. He tried to talk his way into getting a login.",
            "A real IT person shows ID, knows his manager and never asks for a password."
        };
        s.verdictRight = "Correct. No ID, a request for a login and a false claim to be IT staff: that is social engineering.";
        s.verdictWrong = "Not quite. Compare what he said with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Give him the teller's login so he can fix the system",
            "Let him use a computer for two minutes, under supervision",
            "Refuse him, check with IT and the manager, and report him",
            "Ask him to come back with a letter and then give him access"
        };
        s.responseFeedback = new[]
        {
            "A login given to a stranger gives him the bank's accounts. Never share credentials.",
            "Even two minutes at a computer can be enough to steal data or install something. Nobody unverified uses a bank computer.",
            "Correct. Verifying with IT and reporting him protects the bank and warns every counter.",
            "A letter is easy to fake. Verify through the bank's own IT, not through the person asking."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 4: Impersonated Bank Manager (Social Engineering) - questioned like mission 3 (Social Engineering is always done by
    // questioning), but this time you question the WITNESS: the teller who took a phone call from someone using the manager's name.
    // Teller at counter 1 explains (12 seconds) -> a customer gives you the slip with the number the caller used -> take it to the IT officer
    // -> question the teller at counter 3 about the call -> decide -> report to the real bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario ImpersonatedManager()
    {
        const string TellerOne = "mesh_office_f_recepcionist01";
        const string TellerThree = "mesh_office_f_secretary01";
        const string Customer = "mesh_office_m_hipster01";
        const string ItOfficer = "mesh_office_m_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question the teller about the call: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT THE TELLER TOLD YOU ABOUT THE CALL",
            interviewOpening = "Oh, thank you for coming. That phone call was so strange. I do not know if I did the right thing."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the first counter and listen to her.",
            target = TellerOne, label = "TELLER", speaker = "Teller", seconds = 12f,
            lines = new[]
            {
                "Something odd happened this morning. A man phoned the third counter and said he was our bank manager.",
                "But the real manager has been in a meeting all morning. He has not phoned anyone.",
                "My colleague is shaken. Could you talk to her, once you know more?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the man using the cash machine on the left. He found something near the counter phone.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Phone Slip", prompt = "Talk",
            lines = new[]
            {
                "Excuse me. I was waiting by the counters and found this slip near the phone. It has a number written on it.",
                "I heard the teller say it was the number of a man who called pretending to be the manager. I thought it might be important.",
                "Could you take it to the IT officer? She or he can check where the call came from."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the slip to IT",
            hint = "Give the slip to the IT officer, who is waiting on the sofa in the manager's room.",
            target = ItOfficer, label = "IT OFFICER", speaker = "IT Officer", item = "Phone Slip", prompt = "Give Slip",
            lines = new[]
            {
                "Thank you. This number is not the manager's, and it is not in our directory either.",
                "The best witness is the teller at the third counter, who took the call. Question her.",
                "Your tablet lists the 3 things to look for in Social Engineering. A question that finds nothing costs time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question the teller at the third counter about the call. Ask what your tablet lists for Social Engineering.",
            target = TellerThree, label = "QUESTION HER", speaker = "Teller", prompt = "Question Her"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the real bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, someone phoned the third counter pretending to be you. The teller could not verify him, he asked for her login and the vault code, and he said technical support was helping him.",
                "Bank Manager||I never made that call. Did she give him anything?",
                "You||No. She stalled, and the number is with IT.",
                "Bank Manager||Good. That is social engineering: they used my name and the name of technical support to get access. I will warn every counter that I never ask for logins by phone.",
                "Bank Manager||Well done. Always verify who is calling before you share anything."
            }
        });

        s.idleKeys = new[] { TellerOne, Customer, ItOfficer, Boss, TellerThree };
        s.idleLines = new[]
        {
            "Please talk to my colleague at the third counter once you know more.",
            "Please take the slip to the IT officer.",
            "Come and see me once you have the slip.",
            "Hi! Do your tasks first, then come and talk to me.",
            "I am still shaken about that call. Please come back later."
        };

        // Six questions. The first three find the tablet's evidence (in the tablet's order), the others find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Could you check who the caller really was?",
            answer = "He said he was the bank manager, but the number was hidden and his voice sounded different. I had no way to check.",
            objective = 0, callout = "Unverified Caller Identity", summary = "She could not verify him: hidden number, a voice that was not the manager's"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What did he ask you to do?",
            answer = "He wanted my login and the vault access code, to approve an urgent transfer for him.",
            objective = 1, callout = "Credential Request", summary = "He asked for her login and the vault access code"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did he mention technical support?",
            answer = "Yes. He said technical support from head office was working with him, and that I should follow their instructions. He never gave a name.",
            objective = 2, callout = "IT Staff Impersonation", summary = "He claimed technical support from head office was with him, without a name"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What time did he call?",
            answer = "Around 9:40, a little before the lunch rush.",
            note = "The time is not what shows who he was or what he wanted."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Was the line noisy?",
            answer = "A bit. There was some rustling, like paper.",
            note = "The noise does not show who he was or what he wanted."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Did you offer to call him back?",
            answer = "I told him I would, but he said there was no time and hung up.",
            note = "A good instinct, but it does not show who he was, what he asked or whose name he used."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. This was a phone call.",
            "No file or program is involved. A caller asked for something.",
            "Nothing popped up on a screen. Someone phoned the counter.",
            "Spam is bulk mail. This was one caller using the manager's name.",
            "",
            "Nobody logged in to anything yet. The caller tried to talk his way into a login.",
            "A real manager is known to the bank, never hides his number and never asks for a vault code by phone."
        };
        s.verdictRight = "Correct. A caller who could not be verified, asked for credentials and used the names of the manager and technical support: that is social engineering.";
        s.verdictWrong = "Not quite. Compare what the teller said with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Give him the login if he sounds sure of himself",
            "Do a small test transfer first, then decide",
            "Refuse, check with the real manager and IT, and report the call",
            "Call back the number he used and ask him again"
        };
        s.responseFeedback = new[]
        {
            "Sounding sure proves nothing. Never share a login or a vault code with a caller you have not verified.",
            "Even a small transfer helps the scam. Nothing is done for an unverified caller.",
            "Correct. Verifying with the real manager and IT, and reporting the call, protects the bank and warns every counter.",
            "The number belongs to the caller, so you reach the same person. Verify through the bank's own contacts."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 5: Confidential Account Inquiry (Social Engineering) - questioned (Social Engineering is always done by questioning). This
    // time the suspect is a woman at counter 3 who wants another person's account details, and there are EIGHT questions to choose from (three
    // useful, five that find nothing), so a careless player loses more time.
    // The bank manager explains the rules for confidential accounts (15 seconds) -> a customer gives you the slip with the account number the
    // woman asked about -> take it to the teller at counter 3 -> question the woman -> decide -> report to the IT officer in the back office.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario ConfidentialInquiry()
    {
        const string TellerThree = "mesh_office_f_secretary01";
        const string Customer = "mesh_office_m_slacker01";
        const string ItWest = "mesh_office_f_it01";
        const string Suspect = "mesh_office_f_slacker01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question the woman at counter 3: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT SHE SAID WHEN YOU QUESTIONED HER",
            interviewOpening = "Hello. I need the details of an account, it is for a relative. It is quite urgent, so please be quick."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the bank manager",
            hint = "Go to the bank manager and listen to the rules for confidential accounts.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", seconds = 15f,
            lines = new[]
            {
                "Listen carefully. Account details are confidential: balance, PIN, online password. They go to the account holder, and only after we have verified who is asking.",
                "People who want to steal money do not break in. They walk up to a counter, say they are a friend, a relative or technical support, and just ask.",
                "If someone asks about an account that is not theirs, do not answer. Verify first, and tell security."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the man waiting in the armchair near the first counter.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Account Slip", prompt = "Talk",
            lines = new[]
            {
                "Excuse me. The woman at the third counter asked me earlier if I knew somebody named Maria Santos, and whether I could tell her Maria's account number.",
                "I do not know any Maria Santos. I saw her write this account number on a slip before she walked to the counter.",
                "Could you take the slip to the teller at the third counter? She should know what her customer is asking about."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the slip to the teller",
            hint = "Give the slip to the teller at the third counter.",
            target = TellerThree, label = "TELLER", speaker = "Teller", item = "Account Slip", prompt = "Give Slip",
            lines = new[]
            {
                "Oh. That is the account number she is asking me about. The account holder is Maria Santos, and the woman at my counter is not Maria.",
                "She says she is a relative, then a friend, and I cannot tell who she is. Please question her. I have to keep her at my counter.",
                "Your tablet lists the 3 things to look for in Social Engineering. A question that finds nothing costs time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question the woman at the third counter. Ask what your tablet lists for Social Engineering.",
            target = Suspect, label = "QUESTION HER", speaker = "Customer", prompt = "Question Her"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the IT officer in the back office and report what you found.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", prompt = "Report Threat",
            lines = new[]
            {
                "You||I questioned the woman at the third counter. She could not prove who she is, she asked for the balance, the PIN and the online password of Maria Santos, and she said she was from online banking support without a badge.",
                "IT Officer||Did she get any of it?",
                "You||No. The teller kept her talking, and the slip is with the teller.",
                "IT Officer||Good. That is social engineering: she tried to talk her way into someone else's account. I will lock Maria's online access and have security speak to her.",
                "IT Officer||Well done. Confidential details only go to a verified account holder."
            }
        });

        s.idleKeys = new[] { Boss, Customer, TellerThree, ItWest, Suspect };
        s.idleLines = new[]
        {
            "Listen to the rules first, then we will talk.",
            "Please take the slip to the teller at the third counter.",
            "Please question her. I am keeping her at my counter.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Can we please be quick? It is urgent."
        };

        // Eight questions. The first three find the tablet's evidence (in the tablet's order), the other five find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Can you prove who you are?",
            answer = "I do not have any papers on me. I am a close friend of hers, she told me everything. Just look it up.",
            objective = 0, callout = "Unverified Caller Identity", summary = "She could not prove who she is: no papers, only \"a close friend\""
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What exactly do you need?",
            answer = "The balance, her PIN and her online banking password, so I can move her savings before tonight.",
            objective = 1, callout = "Credential Request", summary = "She asked for the balance, the PIN and the online banking password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Are you a bank employee?",
            answer = "Yes, technical support for online banking. I have no badge today: the system is down, so I came in person.",
            objective = 2, callout = "IT Staff Impersonation", summary = "She claimed to be online banking technical support, with no badge"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Is it your first time at this branch?",
            answer = "Yes. Can we get on with it?",
            note = "That does not show who she is, what she wants or whose side she claims to be on."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like to take a number?",
            answer = "No, I am already here at the counter.",
            note = "A polite question, but it finds no evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like to sit down?",
            answer = "No, thank you. I will stand. Please hurry.",
            note = "That is kind, but it tells you nothing about the three things on your tablet."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Is it raining outside?",
            answer = "I did not notice. Why do you ask?",
            note = "Small talk finds no evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like a pen for the form?",
            answer = "There is no form. I only need the details.",
            note = "A pen is not evidence."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. A woman asked at the counter.",
            "No file or program is involved. A person asked for something.",
            "Nothing popped up on a screen. A woman spoke to the teller.",
            "Spam is bulk mail. This is one person at one counter.",
            "",
            "Nobody has logged in to anything. She tried to talk her way into someone else's account.",
            "Someone who cannot prove who she is, asks for a PIN and claims to be support without a badge is not normal."
        };
        s.verdictRight = "Correct. No proof of identity, a request for a PIN and a password, and a claim to be support staff: that is social engineering.";
        s.verdictWrong = "Not quite. Compare what she said with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Give her the balance and the PIN, since she knows the name",
            "Give her the balance only, but keep the PIN",
            "Refuse, verify with the account holder and IT, and report her",
            "Ask her to write the request and leave it with the teller"
        };
        s.responseFeedback = new[]
        {
            "Knowing a name proves nothing. A PIN given to a stranger gives her the savings.",
            "Even the balance is confidential, and it helps her plan the theft. Nothing goes to an unverified person.",
            "Correct. Verifying with the real account holder and IT, and reporting her, protects the account and warns the bank.",
            "A written request is just as easy to fake. Verify through the account holder, not through the person asking."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 6: Suspicious Login Attempt (Unauthorized Access).
    // A fourth way to investigate, and the only one in real time: a LIVE login monitor on your computer. The logins of one teller's account arrive
    // one by one (a new one every two seconds, ten different ones going round); you compare each with the teller's normal profile and press BLOCK on
    // the ones that match what the tablet lists for Unauthorized Access. A wrong block costs time, and the monitor keeps going until all three
    // are blocked.
    // The IT officer tells about the login alerts (12 seconds) -> the woman at the cash machine gives you a lost staff badge -> take it to the bank
    // manager -> watch the login monitor at your computer -> decide -> report to the teller whose account it is.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario SuspiciousLogin()
    {
        const string ItWest = "mesh_office_f_it01";
        const string Customer = "mesh_office_f_hipster01";
        const string TellerOne = "mesh_office_f_recepcionist01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Live Login Monitor",
            investigateQuestion = "Watch the login stream and BLOCK the 3 logins your TABLET lists as evidence",
            analysis = AnalysisKind.Live,
            liveInterval = 2f,
            rosterTitle = "ANA REYES (COUNTER 1)  -  NORMAL PROFILE",
            rosterLines = new[]
            {
                "Works: 08:00 AM - 05:00 PM          Device: TELLER-PC-01",
                "Network: bank (10.20.x.x)           A new login arrives every 2 seconds. BLOCK the suspicious ones."
            }
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the IT officer",
            hint = "Go to the IT officer in the back office and listen to her.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", seconds = 12f,
            lines = new[]
            {
                "The login monitor has been sounding alerts all day for one teller's account, Ana Reyes at the first counter.",
                "Ana only works from eight to five, from her own computer on the bank network. But the monitor shows logins that do not fit her.",
                "I cannot tell which are hers. I will leave the monitor on your computer in a moment."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the woman using the cash machine on the right.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Staff Badge", prompt = "Talk",
            lines = new[]
            {
                "Excuse me. I found this staff badge on the floor by the cash machine. It says Ana Reyes.",
                "I am afraid somebody could use it to get in. Would you take it to the bank manager?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the badge to the manager",
            hint = "Give the badge to the bank manager in his office.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", item = "Staff Badge", prompt = "Give Badge",
            lines = new[]
            {
                "That is Ana's badge. She has not even noticed it was lost. So her account may be in use by somebody else.",
                "Go to your computer and watch the live login monitor. Compare every login with her profile at the top.",
                "Your tablet lists the 3 things to look for in Unauthorized Access. BLOCK only what matches: a wrong block costs you time."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and watch the live login monitor.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Open Login Monitor"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to Ana at the first counter and report what you found.",
            target = TellerOne, label = "ANA", speaker = "Ana", prompt = "Report Threat",
            lines = new[]
            {
                "You||Ana, I watched the logins of your account. Some came at 2:47 in the morning, one from an address outside the bank, and one from a laptop that is not yours.",
                "Ana||That was not me! I only use my own computer here, during my shift. Did they get in?",
                "You||I blocked them, and your lost badge is with the manager.",
                "Ana||Thank you. That is unauthorized access: somebody used my account without permission. I will change my password and IT will lock the old one.",
                "Ana||Well done. I will never leave my badge lying around again."
            }
        });

        s.idleKeys = new[] { ItWest, Customer, Boss, TellerOne };
        s.idleLines = new[]
        {
            "The monitor will be on your computer in a moment.",
            "Please take the badge to the bank manager.",
            "Come and see me once you have the badge.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // The stream: ten records going round. Three are the tablet's evidence (the objective numbers follow the tablet: time, address, device).
        // The pattern is spread out: seven normal logins between them.
        string[,] normal =
        {
            { "Time: 08:05 AM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 09:30 AM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 11:45 AM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 12:50 PM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 02:10 PM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 03:20 PM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            { "Time: 04:40 PM", "IP: 10.20.4.12", "Device: TELLER-PC-01" }
        };

        const string normalHint = "This login matches Ana's profile: working hours, the bank network and her own computer.";

        EmailScenario.Card Normal(int i)
        {
            return new EmailScenario.Card { fields = new[] { normal[i, 0], normal[i, 1], normal[i, 2] }, hint = normalHint };
        }

        s.cards.Add(Normal(0));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 02:47 AM", "IP: 10.20.4.12", "Device: TELLER-PC-01" },
            objective = 0, callout = "Login Outside Working Hours"
        });
        s.cards.Add(Normal(1));
        s.cards.Add(Normal(2));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 10:15 AM", "IP: 185.72.14.91", "Device: TELLER-PC-01" },
            objective = 1, callout = "Unknown IP Address"
        });
        s.cards.Add(Normal(3));
        s.cards.Add(Normal(4));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 01:30 PM", "IP: 10.20.4.12", "Device: LAPTOP-9F3A" },
            objective = 2, callout = "Unrecognized Device"
        });
        s.cards.Add(Normal(5));
        s.cards.Add(Normal(6));

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 5;
        s.verdictFeedback = new[]
        {
            "There is no email or fake link here. Somebody logged in to a real account.",
            "No file was infected or opened. The record shows logins, not a program.",
            "Nothing popped up on a screen. Look at who logged in, when and from where.",
            "There is no bulk mail here. This is a login record.",
            "Nobody was tricked into anything in this record. Someone simply got into the account.",
            "",
            "A real teller does not log in at 2:47 AM, from an unknown address or from a laptop that is not hers."
        };
        s.verdictRight = "Correct. A login outside working hours, from an unknown address and on an unregistered device: someone is using the account without permission.";
        s.verdictWrong = "Not quite. Compare the logins with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Ignore it, it is probably Ana working late",
            "Log in as Ana yourself to see what the intruder did",
            "Lock the account, reset her password and report it to the manager",
            "Tell everyone at the counters to change their password by email"
        };
        s.responseFeedback = new[]
        {
            "Ana only works from 8 to 5, and the other logins are from outside the bank. Ignoring them lets the intruder stay in.",
            "Never use a compromised account. You could destroy evidence or warn the intruder.",
            "Correct. Locking the account and resetting the password stops the intruder, and the manager can follow it up.",
            "A mass email causes panic and attackers can copy it. Report it to the manager first."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Unauthorized Access");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 7: Unauthorized Account Access (Unauthorized Access) - the same way as mission 6 (Unauthorized Access is always done with the
    // live login monitor), with a new story: this time it is a CUSTOMER's online banking account (Maria Santos, the account from mission 5),
    // twelve records going round instead of ten, and her own profile instead of a teller's.
    // The bank manager explains the alert (12 seconds) -> the man at the left cash machine gives you the alert printout -> take it to the IT officer
    // on the sofa -> watch the login monitor at your computer -> decide -> report to the teller at counter 3.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario UnauthorizedAccountAccess()
    {
        const string Customer = "mesh_office_m_hipster01";
        const string ItOfficer = "mesh_office_m_it01";
        const string TellerThree = "mesh_office_f_secretary01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Live Login Monitor",
            investigateQuestion = "Watch the login stream and BLOCK the 3 logins your TABLET lists as evidence",
            analysis = AnalysisKind.Live,
            liveInterval = 2f,
            rosterTitle = "MARIA SANTOS (CUSTOMER)  -  ONLINE BANKING PROFILE",
            rosterLines = new[]
            {
                "Usual hours: 07:00 AM - 10:00 PM          Device: PHONE-MARIA-S21",
                "Network: her home (112.205.x.x)           A new login arrives every 2 seconds. BLOCK the suspicious ones."
            }
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the bank manager",
            hint = "Go to the bank manager and listen to him.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", seconds = 12f,
            lines = new[]
            {
                "A customer, Maria Santos, got an alert from her online banking this morning. She says she did not log in at the time it shows.",
                "Customers trust us to keep their accounts safe. If somebody is in her account, we must find out how, and stop them.",
                "The login monitor for her account will be on your computer. Compare each login with her usual habits."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the man using the cash machine on the left.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Alert Printout", prompt = "Talk",
            lines = new[]
            {
                "Hello. This printout was in the tray of the cash machine. It is a security alert for an account of a Maria Santos.",
                "It says someone logged in to her online banking in the middle of the night. It is not mine, so I thought it belongs to the bank.",
                "Would you take it to the IT officer?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the printout to IT",
            hint = "Give the printout to the IT officer, who is waiting on the sofa in the manager's room.",
            target = ItOfficer, label = "IT OFFICER", speaker = "IT Officer", item = "Alert Printout", prompt = "Give Printout",
            lines = new[]
            {
                "Thank you. This is the alert for her account: the logins are being recorded in the monitor.",
                "Go to your computer and watch the live login monitor. Compare every login with Maria's profile at the top.",
                "Your tablet lists the 3 things to look for in Unauthorized Access. BLOCK only what matches: a wrong block costs you time."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and watch the live login monitor.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Open Login Monitor"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the teller at the third counter, who looks after Maria's account, and report what you found.",
            target = TellerThree, label = "TELLER", speaker = "Teller", prompt = "Report Threat",
            lines = new[]
            {
                "You||I watched the logins of Maria Santos's online banking. Some were in the middle of the night, one came from an address that is not her home, and one from a tablet that is not hers.",
                "Teller||That is not Maria. Did they take any money?",
                "You||I blocked them, and the printout is with IT, so nothing was moved.",
                "Teller||Good. That is unauthorized access: someone used her account without permission. I will call Maria on her official number, and IT will lock her online access.",
                "Teller||Well done. We never let a stranger stay in a customer's account."
            }
        });

        s.idleKeys = new[] { Boss, Customer, ItOfficer, TellerThree };
        s.idleLines = new[]
        {
            "The monitor will be on your computer in a moment.",
            "Please take the printout to the IT officer.",
            "Come and see me once you have the printout.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // The stream: twelve records going round, three of them evidence (the objective numbers follow the tablet: time, address, device).
        string[,] normal =
        {
            { "Time: 07:30 AM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 09:10 AM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 12:15 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 01:40 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 03:25 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 06:45 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 08:20 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 09:35 PM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            { "Time: 08:50 AM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" }
        };

        const string normalHint = "This login matches Maria's profile: her usual hours, her home network and her own phone.";

        EmailScenario.Card Normal(int i)
        {
            return new EmailScenario.Card { fields = new[] { normal[i, 0], normal[i, 1], normal[i, 2] }, hint = normalHint };
        }

        s.cards.Add(Normal(0));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 03:12 AM", "IP: 112.205.14.8", "Device: PHONE-MARIA-S21" },
            objective = 0, callout = "Login Outside Working Hours"
        });
        s.cards.Add(Normal(1));
        s.cards.Add(Normal(2));
        s.cards.Add(Normal(3));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 10:45 AM", "IP: 45.133.201.7", "Device: PHONE-MARIA-S21" },
            objective = 1, callout = "Unknown IP Address"
        });
        s.cards.Add(Normal(4));
        s.cards.Add(Normal(5));
        s.cards.Add(Normal(6));
        s.cards.Add(new EmailScenario.Card
        {
            fields = new[] { "Time: 05:05 PM", "IP: 112.205.14.8", "Device: UNKNOWN-TABLET-77" },
            objective = 2, callout = "Unrecognized Device"
        });
        s.cards.Add(Normal(7));
        s.cards.Add(Normal(8));

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 5;
        s.verdictFeedback = new[]
        {
            "There is no email or fake link here. Somebody logged in to a real account.",
            "No file was infected or opened. The record shows logins, not a program.",
            "Nothing popped up on a screen. Look at who logged in, when and from where.",
            "There is no bulk mail here. This is a login record.",
            "Nobody was tricked into anything in this record. Someone simply got into the account.",
            "",
            "A real customer does not log in at 3:12 AM, from an unknown address or from a tablet that is not hers."
        };
        s.verdictRight = "Correct. A login at an unusual hour, from an unknown address and on an unregistered device: someone is using the account without permission.";
        s.verdictWrong = "Not quite. Compare the logins with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Ignore it, customers log in at strange hours sometimes",
            "Log in to her account yourself to see what the intruder did",
            "Lock her online access and call her on her official number",
            "Email Maria the new password so she can log in again"
        };
        s.responseFeedback = new[]
        {
            "The unusual hour, address and device together are no coincidence. Ignoring them lets the intruder keep the account.",
            "Never use a customer's account. You could destroy evidence or warn the intruder.",
            "Correct. Locking the online access stops the intruder, and calling her on the number the bank already has confirms it with the real Maria.",
            "Sending a password by email can be read by the intruder. Verify with Maria on her official number first."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Unauthorized Access");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 8: Inbox Advertisement Flood (Spam Email) - a new threat in the bank, so a new way to investigate: you SET UP THE SPAM FILTER.
    // Your inbox holds 214 messages since midnight. Six rules can be switched on; three of them match what the tablet lists for Spam Mail (a
    // too-good-to-be-true offer, a request for personal information, a strange sender address) and empty the inbox as you switch them on. The
    // other three would block real mail, so they cost time and switch themselves off again.
    // The IT officer in the back office explains the flood (10 seconds) -> the woman in the armchair gives you a printed ad -> take it to the IT
    // officer on the sofa -> set up the filter at your computer -> decide -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario InboxFlood()
    {
        const string ItWest = "mesh_office_f_it01";
        const string Customer = "mesh_office_f_black01";
        const string ItOfficer = "mesh_office_m_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Spam Filter Settings",
            investigateQuestion = "Switch ON the 3 filter rules that match the evidence your TABLET lists",
            analysis = AnalysisKind.Filter,
            filterTotal = 214,
            filterPerRule = 70,
            rosterTitle = "YOUR INBOX  -  FLOODED OVERNIGHT.  SWITCH ON ONLY THE RIGHT RULES"
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the IT officer",
            hint = "Go to the IT officer in the back office and listen to her.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", seconds = 10f,
            lines = new[]
            {
                "Since midnight the whole bank's inbox has been flooded: more than two hundred ads in a few hours.",
                "The tellers cannot find the real emails from customers any more. The spam filter is nearly empty, it only works with the rules we give it.",
                "I will open the filter on your computer. Be careful: a bad rule blocks real mail too."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the woman waiting in the armchair near the third counter.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Printed Ad", prompt = "Talk",
            lines = new[]
            {
                "Hello. Even my phone got these emails about the bank. They are everywhere.",
                "I printed one, it says I won a loan and that I must send my card number to get it. Of course I did not.",
                "Maybe it helps. Would you give it to the IT officer on the sofa?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the ad to IT",
            hint = "Give the printed ad to the IT officer, who is waiting on the sofa in the manager's room.",
            target = ItOfficer, label = "IT OFFICER", speaker = "IT Officer", item = "Printed Ad", prompt = "Give Ad",
            lines = new[]
            {
                "A prize, a request for card details and a sender address of random letters. A perfect sample of the flood.",
                "Go to your computer and open the spam filter. Switch on the rules that catch these things.",
                "Your tablet lists the 3 things to look for in Spam Mail. A rule that blocks real mail costs you time."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and set up the spam filter.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Open Spam Filter"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, the inbox flood is spam. The messages promise prizes, ask for card details and come from strange addresses, and I switched on the filter rules that catch them.",
                "Bank Manager||Did anybody open the messages or reply?",
                "You||No. I left the real customer mail alone, and the filter is blocking the rest.",
                "Bank Manager||Good. That is spam: unwanted bulk mail. IT will keep the filter on and I will tell everyone never to reply or give personal information.",
                "Bank Manager||Well done. Never reply to spam, and report it."
            }
        });

        s.idleKeys = new[] { ItWest, Customer, ItOfficer, Boss };
        s.idleLines = new[]
        {
            "The filter will be on your computer in a moment.",
            "Please give the ad to the IT officer on the sofa.",
            "Come and see me once you have the ad.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // The six rules (in a mixed order). The three that work follow the tablet's order: offer, personal information, sender address.
        s.cards.Add(new EmailScenario.Card
        {
            title = "Block every email that has a picture in it",
            fields = new[] { "Catches: ads with pictures... and the bank's own statements and newsletters" },
            hint = "Real mail has pictures too: statements and newsletters. This rule would block real customer mail."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "Block messages that promise a prize, a loan or free money",
            fields = new[] { "Catches: \"YOU WON $5,000!\", \"free loan\", \"claim your reward now\"" },
            objective = 0, callout = "Unrealistic Reward Offer"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "Block every email from outside the bank",
            fields = new[] { "Catches: all senders that are not @ourbank.com... including every customer" },
            hint = "Customers write to the bank from outside. This rule would block their real requests."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "Block messages that ask for card numbers, PINs or passwords",
            fields = new[] { "Catches: \"send your card number\", \"confirm your PIN\", \"verify your password\"" },
            objective = 1, callout = "Personal Information Request"
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "Delete every email that arrives at night",
            fields = new[] { "Catches: all mail between 10 PM and 6 AM... including real customers abroad" },
            hint = "Real messages arrive at night too, from customers in other time zones. This rule would delete real mail."
        });
        s.cards.Add(new EmailScenario.Card
        {
            title = "Block strange sender addresses (random letters, odd endings)",
            fields = new[] { "Catches: x7k2q9@free-offers4u.xyz, prizes@mega-rewards.biz, gifts@promo-hub.net" },
            objective = 2, callout = "Suspicious Sender Address"
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 3;
        s.verdictFeedback = new[]
        {
            "Phishing pretends to be one trusted sender to steal your information. This is a flood of unwanted ads from strangers.",
            "No file or attachment is involved. These are messages in the inbox.",
            "Nothing popped up on a screen. These are emails.",
            "",
            "Nobody is tricking a person face to face or on the phone. These are mass emails.",
            "Nobody is logging in to anything here. These are unwanted emails.",
            "A normal inbox does not get more than two hundred prize offers from strangers in one night."
        };
        s.verdictRight = "Correct. A flood of prize offers, requests for personal information and strange senders: that is spam.";
        s.verdictWrong = "Not quite. Compare the rules you switched on with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Open every email and click unsubscribe",
            "Reply to the senders and ask them to stop",
            "Keep the filter on, mark the messages as spam and tell IT",
            "Forward the ads to the tellers as a warning"
        };
        s.responseFeedback = new[]
        {
            "Opening spam and clicking its links confirms your address and can be dangerous. Do not open it.",
            "A reply tells the sender your address is real, so you get even more spam.",
            "Correct. The filter and the spam marks teach the system, and IT can block the senders.",
            "Forwarding spreads it to everyone. Tell IT instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Spam Mail");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 9: Urgent Account Verification (Phishing Email) - a new way for phishing: SPOT THE DIFFERENCES. The bank's REAL notice is shown
    // next to the email the customers received. Eight lines, five identical, three different: the sender address, the link and the urgent wording,
    // which are exactly the three pieces of evidence the tablet lists for Phishing Email. You tap the lines of the received email that differ.
    // The teller at counter 1 says customers are worried (12 seconds) -> the man in the armchair gives you a printed copy -> take it to the IT
    // officer in the back office, who put the real notice next to it -> compare at your computer -> decide -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario UrgentVerification()
    {
        const string TellerOne = "mesh_office_f_recepcionist01";
        const string Customer = "mesh_office_m_slacker01";
        const string ItWest = "mesh_office_f_it01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "THE EMAIL YOU RECEIVED",
            rosterTitle = "THE BANK'S REAL NOTICE",
            investigateQuestion = "Tap the 3 differences your TABLET lists between the real notice and the email"
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the first counter and listen to her.",
            target = TellerOne, label = "TELLER", speaker = "Teller", seconds = 12f,
            lines = new[]
            {
                "Customers keep coming to my counter with their phones. They got an email that says their account must be verified at once.",
                "Some of them are scared and almost clicked. The bank did send a notice this week, but this one looks different, and I cannot say how.",
                "Maybe somebody at IT can compare them."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the man waiting in the armchair near the first counter.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Printed Email", prompt = "Talk",
            lines = new[]
            {
                "Hello. I got an email saying my account would be closed if I did not verify it today. I was about to click.",
                "Then I saw the other customers with the same one, so I printed it. It looks like the bank's, but something feels wrong.",
                "Could you give this to the IT officer in the back office?"
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the email to IT",
            hint = "Give the printed email to the IT officer in the back office.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", item = "Printed Email", prompt = "Give Email",
            lines = new[]
            {
                "Thank you. I have the bank's real notice here, so I put it next to this email on your computer.",
                "They say almost the same thing, but not in every line. Compare them and tap what is different.",
                "Your tablet lists the 3 things to look for in Phishing Email. A line that is the same in both costs you time."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Analyze, side = false,
            hint = "Go to your computer at the middle counter and compare the two emails.",
            computer = "my", label = "YOUR COMPUTER", prompt = "Compare Emails"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, I compared the customers' email with our real notice. The sender address is different, the link goes to another website and the wording is urgent and threatening.",
                "Bank Manager||Did any customer click it?",
                "You||No. They stopped and brought it to the counter, and IT has a copy.",
                "Bank Manager||Good. That is phishing: a fake that looks like us. I will warn every customer and ask IT to block that website.",
                "Bank Manager||Well done. Always compare the sender, the link and the tone before you click."
            }
        });

        s.idleKeys = new[] { TellerOne, Customer, ItWest, Boss };
        s.idleLines = new[]
        {
            "Please take the email to IT, my customers are worried.",
            "Please give the printout to the IT officer in the back office.",
            "Come and see me once you have the printed email.",
            "Hi! Do your tasks first, then come and talk to me."
        };

        // Eight lines. The received email is s.lines; "reference" is the real notice's version (missing = the same in both).
        s.lines.Add(new EmailScenario.Line
        {
            text = "From: service@ourbank-verify.top", reference = "From: service@ourbank.com",
            objective = 0, callout = "Suspicious Sender Address"
        });
        s.lines.Add(new EmailScenario.Line { text = "To: customer@mail.com", hint = "The same in both emails: nothing different here." });
        s.lines.Add(new EmailScenario.Line { text = "Subject: Your account verification", hint = "The same in both emails: nothing different here." });
        s.lines.Add(new EmailScenario.Line { text = "Dear customer,", spaceBefore = true, hint = "The same in both emails: nothing different here." });
        s.lines.Add(new EmailScenario.Line { text = "We are updating our records.", hint = "The same in both emails: nothing different here." });
        s.lines.Add(new EmailScenario.Line
        {
            text = "Verify in 24 HOURS or lose access!", reference = "You can verify at a branch, no rush.",
            objective = 2, callout = "Urgent or Threatening Language"
        });
        s.lines.Add(new EmailScenario.Line
        {
            text = "Verify: ourbank-verify.top/login", reference = "Details: www.ourbank.com/accounts", link = true,
            objective = 1, callout = "Suspicious Link"
        });
        s.lines.Add(new EmailScenario.Line { text = "Regards, Customer Service", spaceBefore = true, hint = "The same in both emails: nothing different here." });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 0;
        s.verdictFeedback = new[]
        {
            "",
            "There is no file or attachment to open.",
            "This is an email, not a window that suddenly appears on the screen.",
            "Spam is mass advertising. This one pretends to be a real notice from your own bank.",
            "Nobody is tricking a person face to face or by phone. It is an email.",
            "Nobody is logging in to anything here. It is a fake email asking you to log in.",
            "A real bank notice does not come from a strange address with a threat and a strange link."
        };
        s.verdictRight = "Correct. A strange sender address, a suspicious link and urgent, threatening language: that is phishing.";
        s.verdictWrong = "Not quite. Compare what was different with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Click the link quickly so the account is not closed",
            "Reply with my account details to be safe",
            "Do not click, and report the email to IT",
            "Forward the email to every customer as a warning"
        };
        s.responseFeedback = new[]
        {
            "The link leads to a fake login page that steals your password. Never click it.",
            "That gives your account details to the scammers. Never reply to it.",
            "Correct. Not clicking and reporting protects you, and IT can warn the customers and block the website.",
            "Forwarding spreads the phishing email. Report it to IT instead."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Phishing Email");
        return s;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // BANK, mission 10: The Suspicious Transaction Request (Social Engineering) - the finale, questioned like the other Social Engineering missions.
    // A woman at counter 3 wants a large transfer approved at once. Eight questions, three useful and five that find nothing.
    // The teller at counter 1 sets the scene (15 seconds) -> the man at the right cash machine gives you the transfer form she left -> take it to the
    // IT officer in the back office -> question the woman -> decide -> report to the bank manager.
    // ---------------------------------------------------------------------------------------------------------------------------
    private static EmailScenario SuspiciousTransaction()
    {
        const string TellerOne = "mesh_office_f_recepcionist01";
        const string Customer = "mesh_office_m_hipster01";
        const string ItWest = "mesh_office_f_it01";
        const string Suspect = "mesh_office_f_slacker01";

        EmailScenario s = new EmailScenario
        {
            documentTitle = "Your Computer  -  Interview Notes",
            investigateQuestion = "Question the woman at counter 3: find the 3 pieces of evidence from your TABLET",
            analysis = AnalysisKind.Summary,
            summaryHeading = "WHAT SHE SAID WHEN YOU QUESTIONED HER",
            interviewOpening = "I need a transfer approved right now. It is for a very big order, and there is no time to lose."
        };

        s.steps.Add(new MissionStep
        {
            kind = StepKind.Meeting, side = true, heading = "Listen to the teller",
            hint = "Go to the teller at the first counter and listen to her.",
            target = TellerOne, label = "TELLER", speaker = "Teller", seconds = 15f,
            lines = new[]
            {
                "This is the last day of your training, so I will be honest with you. Today is the kind of day banks fear.",
                "A woman has been at the third counter for twenty minutes. She wants a transfer of forty-eight thousand dollars to another bank, and she wants it approved immediately.",
                "She keeps saying it is urgent and that someone sent her. My colleague is holding her at the counter. Please find out the truth."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Talk, side = true, heading = "Talk to the customer",
            hint = "Find the man using the cash machine on the right.",
            target = Customer, label = "CUSTOMER", speaker = "Customer", item = "Transfer Form", prompt = "Talk",
            lines = new[]
            {
                "Hello. The woman at the third counter dropped this form near the cash machine, and she was in such a hurry that she did not notice.",
                "It is a transfer form for forty-eight thousand dollars. The account holder's signature box is empty.",
                "Please take it to the IT officer in the back office. It looks like a scam to me."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Deliver, side = true, heading = "Take the form to IT",
            hint = "Give the transfer form to the IT officer in the back office.",
            target = ItWest, label = "IT OFFICER", speaker = "IT Officer", item = "Transfer Form", prompt = "Give Form",
            lines = new[]
            {
                "Thank you. Nobody from our IT or security department has asked for a transfer today, and an empty signature box means nobody authorized it.",
                "Go and question her. It is the last mission, so use everything you learned. Your tablet lists the 3 things to look for in Social Engineering.",
                "Every question that finds nothing costs you time, so choose carefully."
            }
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Interview, side = false,
            hint = "Question the woman at the third counter. Ask what your tablet lists for Social Engineering.",
            target = Suspect, label = "QUESTION HER", speaker = "Customer", prompt = "Question Her"
        });
        s.steps.Add(new MissionStep
        {
            kind = StepKind.Report, side = false,
            hint = "Go to the bank manager and report what you found.",
            target = Boss, label = "BANK MANAGER", speaker = "Bank Manager", prompt = "Report Threat",
            lines = new[]
            {
                "You||Sir, the woman at the third counter wanted a forty-eight thousand dollar transfer. She had no ID or authorization, she asked the teller for the approval code and the vault password, and she said the IT security department sent her.",
                "Bank Manager||Did the transfer go through?",
                "You||No. The teller kept her at the counter, and the form is with IT.",
                "Bank Manager||Excellent. That is social engineering at its boldest: she tried to talk the bank into doing the thieving for her. I will call the police and warn every branch.",
                "Bank Manager||You have helped keep this bank safe from the first mission to the last. Well done, and thank you."
            }
        });

        s.idleKeys = new[] { TellerOne, Customer, ItWest, Boss, Suspect };
        s.idleLines = new[]
        {
            "Please find out the truth about the woman at the third counter.",
            "Please take the form to the IT officer.",
            "Come and see me once you have the form.",
            "Hi! Do your tasks first, then come and talk to me.",
            "Can we please be quick? This is urgent."
        };

        // Eight questions. The first three find the tablet's evidence (in the tablet's order), the other five find nothing.
        s.questions.Add(new EmailScenario.Question
        {
            text = "Can you show me your ID and the account holder's authorization?",
            answer = "I left the papers at home. The owner told me by phone that I could do it. That is enough, is it not?",
            objective = 0, callout = "Unverified Caller Identity", summary = "She had no ID and no written authorization from the account holder"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "What do you need me to do?",
            answer = "Approve the transfer right now. I need your approval code and the vault authorization password to release it.",
            objective = 1, callout = "Credential Request", summary = "She asked for the teller's approval code and the vault authorization password"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Who sent you?",
            answer = "The bank's IT security department. We are running a system test transfer, so there is no time for paperwork.",
            objective = 2, callout = "IT Staff Impersonation", summary = "She said the IT security department sent her, for a \"system test\" transfer"
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like some coffee while you wait?",
            answer = "No. I do not have time for coffee.",
            note = "A friendly offer, but it finds no evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Is this your first transfer here?",
            answer = "Does it matter? I want it done now.",
            note = "How often she comes does not show who she is, what she asks for or whose name she uses."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "How long have you been waiting?",
            answer = "Far too long. Please hurry.",
            note = "The waiting time finds no evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like a pen?",
            answer = "I have my own pen. Please, the transfer.",
            note = "A pen is not evidence."
        });
        s.questions.Add(new EmailScenario.Question
        {
            text = "Would you like a copy of the form?",
            answer = "No. I just want it approved.",
            note = "A copy of the form finds no evidence."
        });

        s.verdictOptions = ThreatChoices;
        s.verdictCorrect = 4;
        s.verdictFeedback = new[]
        {
            "Phishing is a fake email. A woman asked at the counter.",
            "No file or program is involved. A person asked for something.",
            "Nothing popped up on a screen. A woman spoke to the teller.",
            "Spam is bulk mail. This is one person at one counter.",
            "",
            "Nobody has logged in to anything. She tried to talk the teller into approving a transfer.",
            "Someone who has no ID, asks for the approval code and claims to be IT security is not normal."
        };
        s.verdictRight = "Correct. No proof of identity, a request for approval codes and a false claim to be IT security: that is social engineering.";
        s.verdictWrong = "Not quite. Compare what she said with the descriptions on your tablet.";

        s.responseOptions = new[]
        {
            "Approve it, because the IT department sent her",
            "Approve half the amount and check the rest later",
            "Refuse, check with the account holder and the manager, and report her",
            "Ask her to come back tomorrow and approve it then"
        };
        s.responseFeedback = new[]
        {
            "IT security does not send people to the counter with transfers, and nobody authorized this one. Approving would lose the bank the money.",
            "Even half is twenty-four thousand dollars. Nothing is approved for someone nobody has verified.",
            "Correct. Verifying with the real account holder and the manager, and reporting her, stops the theft and warns the bank.",
            "She would simply come back with a new story. Verify now and report her."
        };
        s.responseCorrect = 2;

        s.hints = Hints("Social Engineering");
        return s;
    }
}
