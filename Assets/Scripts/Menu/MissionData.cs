using System;

// The ten missions of each environment and the six objectives of each threat (SceneSelectionActivity / MissionBriefingActivity).
public static class MissionData
{
    public static readonly string[] OfficeTitles =
    {
        "Payroll Information Update", "After-hours Login Activity", "Confidential File Access", "Inbox Advertisement Flood",
        "Prize Giveaway Scam", "Fake IT Support Call", "Confidential Information Request", "Suspicious Attachment",
        "Emergency Meeting Invitation", "The Urgent Executive Request"
    };

    public static readonly string[] OfficeThreats =
    {
        "Phishing Email", "Unauthorized Access", "Unauthorized Access", "Spam Email", "Spam Email",
        "Social Engineering", "Social Engineering", "Malware Files", "Phishing Email", "Social Engineering"
    };

    public static readonly string[] BankTitles =
    {
        "Fake Security Alert", "Suspicious Bank Attachment", "Unverified Customer Request", "Impersonated Bank Manager",
        "Confidential Account Inquiry", "Suspicious Login Attempt", "Unauthorized Account Access", "Inbox Advertisement Flood",
        "Urgent Account Verification", "The Suspicious Transaction Request"
    };

    public static readonly string[] BankThreats =
    {
        "Fake Pop-up Windows", "Malware Files", "Social Engineering", "Social Engineering", "Social Engineering",
        "Unauthorized Access", "Unauthorized Access", "Spam Email", "Phishing Email", "Social Engineering"
    };

    public static bool IsBank(string environment)
    {
        return string.Equals(environment, "bank", StringComparison.OrdinalIgnoreCase);
    }

    // The 3D scene that plays this environment.
    public static string SceneName(string environment)
    {
        return IsBank(environment) ? "Bank_scene" : "Company_scene";
    }

    public static string[] Objectives(string threat)
    {
        switch ((threat ?? "").Trim().ToLowerInvariant())
        {
            case "phishing email":
                return new[]
                {
                    "Verify the sender's identity", "Analyze the message and its links", "Examine the requested information",
                    "Identify suspicious email indicators", "Determine whether the message is legitimate", "Submit the correct security response"
                };
            case "unauthorized access":
                return new[]
                {
                    "Review the access records", "Identify unusual account activity", "Verify the user's identity",
                    "Check the account's access permissions", "Determine whether the access is authorized", "Submit the correct security response"
                };
            case "spam email":
                return new[]
                {
                    "Review the suspicious messages", "Check the sender's legitimacy", "Inspect suspicious links",
                    "Identify repeated or unwanted messages", "Determine whether the email is spam", "Submit the correct security response"
                };
            case "social engineering":
                return new[]
                {
                    "Review the request or interaction", "Verify the person's identity", "Check whether the request is authorized",
                    "Identify manipulation tactics", "Determine the safest course of action", "Submit the correct security response"
                };
            case "malware files":
                return new[]
                {
                    "Examine the suspicious file", "Verify the sender or source", "Inspect the file name and extension",
                    "Identify possible malware indicators", "Determine whether the file is safe", "Submit the correct security response"
                };
            case "fake pop-up windows":
                return new[]
                {
                    "Inspect the security warning", "Verify whether the alert is legitimate", "Identify suspicious instructions",
                    "Check for misleading warnings or claims", "Determine the safest response", "Submit the correct security response"
                };
            default:
                return new[]
                {
                    "Review the reported incident", "Investigate the available evidence", "Identify suspicious activity",
                    "Classify the possible cybersecurity threat", "Determine the appropriate response", "Submit your security decision"
                };
        }
    }

    public static string[] StorySlides(string environment)
    {
        if (IsBank(environment))
        {
            return new[]
            {
                "You begin your day at the bank, ready to help maintain a safe and reliable environment for employees and customers. Security is important because financial information must be protected.",
                "As you prepare for work, you learn that banks can be targeted by phishing messages, fake security alerts, malware, and attempts to access confidential customer accounts.",
                "Upon arriving at the bank, you access the cybersecurity training system. You will practice recognizing suspicious alerts, protecting financial information, and responding carefully to unusual requests.",
                "Your bank security training begins now. Stay alert, examine suspicious activity, and choose safe responses to protect the organization and its customers. Your first mission is about to begin."
            };
        }

        return new[]
        {
            "You wake up early in the morning for your first day at a new job. After getting ready, you check your phone and notice a company notification reminding employees about the importance of cybersecurity in the workplace.",
            "As you travel to work, news reports and online articles discuss the growing number of cyber attacks affecting businesses and organizations. Phishing emails, malware infections, social engineering scams, and unauthorized access incidents have become increasingly common.",
            "Upon arriving at work, you receive access to the company's cybersecurity training system. The system is designed to help employees recognize cyber threats, investigate suspicious activities, and make safe security decisions in different workplace situations.",
            "Your journey begins as you enter the Firewall Duty system, where you will learn how to identify cyber threats, analyze evidence, and protect organizations from potential security breaches. The decisions you make throughout your training will determine your cybersecurity awareness and overall performance."
        };
    }
}
