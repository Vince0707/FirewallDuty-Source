using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Google sign-in for the Unity Editor (and any place without the phone's own Google sheet), by Google's "device flow":
//   1. Ask Google for a code.   2. The player types it at google.com/device and approves.   3. Poll until Google returns an ID token.
// FirebaseRest.SignInWithGoogle then turns the ID token into a Firebase account, exactly as the phone sign-in does.
// Needs an OAuth client of type "TVs and Limited Input devices" in the Google Cloud project; its id and secret go in
// Resources/MenuUI/google_device_config.json. (For installed apps Google does not treat that secret as confidential.)
// On a real Android phone the game uses the phone's own sign-in instead (NativeBridge).
public static class GoogleDeviceLogin
{
    [Serializable]
    private class Config
    {
        public string clientId;
        public string clientSecret;
    }

    private static Config config;

    public static bool IsConfigured
    {
        get
        {
            Load();
            return config != null && !string.IsNullOrEmpty(config.clientId) && !string.IsNullOrEmpty(config.clientSecret)
                   && !config.clientId.StartsWith("PASTE");
        }
    }

    private static void Load()
    {
        if (config != null)
            return;

        TextAsset asset = Resources.Load<TextAsset>("MenuUI/google_device_config");
        if (asset != null)
            config = JsonUtility.FromJson<Config>(asset.text);
    }

    // onCode(code, address) shows the code; onDone(true, idToken) or onDone(false, message) ("" if cancelled).
    public static FacebookLogin.Session Start(Action<string, string> onCode, Action<bool, string> onDone)
    {
        FacebookLogin.Session session = new FacebookLogin.Session();
        FirebaseRest.Runner.StartCoroutine(Run(session, onCode, onDone));
        return session;
    }

    private static IEnumerator Run(FacebookLogin.Session session, Action<string, string> onCode, Action<bool, string> onDone)
    {
        Load();

        WWWForm start = new WWWForm();
        start.AddField("client_id", config.clientId);
        start.AddField("scope", "openid email profile");

        string deviceCode;
        float interval = 5f, lifetime = 900f;

        using (UnityWebRequest request = UnityWebRequest.Post("https://oauth2.googleapis.com/device/code", start))
        {
            yield return request.SendWebRequest();

            Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);
            deviceCode = MiniJson.GetString(json, "device_code");

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                onDone(false, "No internet connection.");
                yield break;
            }

            if (string.IsNullOrEmpty(deviceCode))
            {
                string reason = MiniJson.GetString(json, "error_description") ?? MiniJson.GetString(json, "error") ?? "Could not start Google login.";
                onDone(false, reason);
                yield break;
            }

            string userCode = MiniJson.GetString(json, "user_code");
            string address = MiniJson.GetString(json, "verification_url") ?? "https://www.google.com/device";
            if (float.TryParse(MiniJson.GetString(json, "interval"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float i))
                interval = Mathf.Max(2f, i);
            if (float.TryParse(MiniJson.GetString(json, "expires_in"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float e))
                lifetime = e;

            onCode(userCode, address);
        }

        float waited = 0f;
        while (!session.cancelled && waited < lifetime)
        {
            yield return new WaitForSecondsRealtime(interval);
            waited += interval;

            if (session.cancelled)
                break;

            WWWForm poll = new WWWForm();
            poll.AddField("client_id", config.clientId);
            poll.AddField("client_secret", config.clientSecret);
            poll.AddField("device_code", deviceCode);
            poll.AddField("grant_type", "urn:ietf:params:oauth:grant-type:device_code");

            using (UnityWebRequest request = UnityWebRequest.Post("https://oauth2.googleapis.com/token", poll))
            {
                yield return request.SendWebRequest();

                Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);
                string idToken = MiniJson.GetString(json, "id_token");

                if (!string.IsNullOrEmpty(idToken))
                {
                    onDone(true, idToken);
                    yield break;
                }

                string error = MiniJson.GetString(json, "error");
                if (error == "authorization_pending")
                    continue;
                if (error == "slow_down")
                {
                    interval += 5f;
                    continue;
                }
                if (error == "access_denied")
                {
                    onDone(false, "Google login was declined.");
                    yield break;
                }
                if (error == "expired_token")
                    break;

                if (request.result == UnityWebRequest.Result.ConnectionError)
                    continue;

                if (!string.IsNullOrEmpty(error))
                {
                    onDone(false, MiniJson.GetString(json, "error_description") ?? error);
                    yield break;
                }
            }
        }

        onDone(false, session.cancelled ? "" : "The Google code expired. Please try again.");
    }
}
