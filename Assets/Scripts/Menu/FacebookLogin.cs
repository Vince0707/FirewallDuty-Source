using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Facebook sign-in by "device login" (developers.facebook.com/docs/facebook-login/for-devices): no Facebook SDK, no phone screens.
//   1. Ask Facebook for a code.   2. The player types the code at facebook.com/device and approves.
//   3. Poll until Facebook hands over an access token.   FirebaseRest.SignInWithFacebook then turns it into a Firebase account.
// Needs, on developers.facebook.com: Facebook Login > Settings > "Login for Devices" turned ON.
// The app id and the client token (not the app secret, which must never be in the game) are in Resources/MenuUI/facebook_config.json.
public static class FacebookLogin
{
    [Serializable]
    private class Config
    {
        public string appId;
        public string clientToken;
    }

    public class Session
    {
        public bool cancelled;
        public void Cancel() { cancelled = true; }
    }

    private static Config config;

    public static bool IsConfigured
    {
        get
        {
            Load();
            return config != null && !string.IsNullOrEmpty(config.appId) && !string.IsNullOrEmpty(config.clientToken);
        }
    }

    private static void Load()
    {
        if (config != null)
            return;

        TextAsset asset = Resources.Load<TextAsset>("MenuUI/facebook_config");
        if (asset != null)
            config = JsonUtility.FromJson<Config>(asset.text);
    }

    // onCode(code, address): show the code and where to type it. onDone(true, accessToken) or onDone(false, message) ("" if cancelled).
    public static Session Start(Action<string, string> onCode, Action<bool, string> onDone)
    {
        Session session = new Session();
        FirebaseRest.Runner.StartCoroutine(Run(session, onCode, onDone));
        return session;
    }

    private static IEnumerator Run(Session session, Action<string, string> onCode, Action<bool, string> onDone)
    {
        Load();
        string appToken = config.appId + "|" + config.clientToken;

        // 1. the code
        WWWForm start = new WWWForm();
        start.AddField("access_token", appToken);
        start.AddField("scope", "public_profile");   // "email" is not allowed for this app until it is added under Permissions in the Facebook Login use case

        string code = null;
        float interval = 5f, lifetime = 420f;

        using (UnityWebRequest request = UnityWebRequest.Post("https://graph.facebook.com/v19.0/device/login", start))
        {
            yield return request.SendWebRequest();

            Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || json == null || MiniJson.GetString(json, "code") == null)
            {
                onDone(false, Describe(request, json, "Could not start Facebook login. Is \"Login for Devices\" turned on for the app?"));
                yield break;
            }

            code = MiniJson.GetString(json, "code");
            string userCode = MiniJson.GetString(json, "user_code");
            string address = MiniJson.GetString(json, "verification_uri") ?? "https://www.facebook.com/device";
            if (float.TryParse(MiniJson.GetString(json, "interval"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float i))
                interval = Mathf.Max(2f, i);
            if (float.TryParse(MiniJson.GetString(json, "expires_in"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float e))
                lifetime = e;

            onCode(userCode, address);
        }

        // 2 + 3. wait for the player to approve
        float waited = 0f;
        while (!session.cancelled && waited < lifetime)
        {
            yield return new WaitForSecondsRealtime(interval);
            waited += interval;

            if (session.cancelled)
                break;

            WWWForm poll = new WWWForm();
            poll.AddField("access_token", appToken);
            poll.AddField("code", code);

            using (UnityWebRequest request = UnityWebRequest.Post("https://graph.facebook.com/v19.0/device/login_status", poll))
            {
                yield return request.SendWebRequest();

                Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);
                string token = MiniJson.GetString(json, "access_token");

                if (!string.IsNullOrEmpty(token))
                {
                    onDone(true, token);
                    yield break;
                }

                // Facebook answers with an error while the player has not finished: "pending", "slow down", "declined", "expired".
                string subcode = MiniJson.GetString(json, "error", "error_subcode");
                if (subcode == "1349174")
                    continue;                        // still waiting
                if (subcode == "1349172")
                {
                    interval += 5f;                  // asked to slow down
                    continue;
                }
                if (subcode == "1349173")
                {
                    onDone(false, "Facebook login was declined.");
                    yield break;
                }
                if (subcode == "1349152")
                    break;                           // the code ran out

                if (request.result == UnityWebRequest.Result.ConnectionError)
                    continue;                        // a bad moment for the network: try again
            }
        }

        onDone(false, session.cancelled ? "" : "The Facebook code expired. Please try again.");
    }

    private static string Describe(UnityWebRequest request, Dictionary<string, object> json, string fallback)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return "No internet connection.";

        string message = MiniJson.GetString(json, "error", "message");
        return string.IsNullOrEmpty(message) ? fallback : message + " (" + fallback + ")";
    }
}
