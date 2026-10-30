using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Talks to the SAME Firebase project as the Android app, over its public web (REST) interface, so Unity needs no Firebase SDK:
//   - Authentication: email + password sign-in, sign-up and password-reset email
//   - Firestore: the "players" collection (uid, fullName, username, email, level, exp, unlockedAchievements, photoUri)
// The project id and web API key come from Resources/MenuUI/firebase_config.json (made by "Import Android Assets").
// Google and Facebook sign-in need the phone's own sign-in screens, so they are not available inside Unity.
public static class FirebaseRest
{
    [Serializable]
    private class Config
    {
        public string projectId;
        public string apiKey;
        public string webClientId;
    }

    public class Result
    {
        public bool ok;
        public string error;      // a readable message when ok is false
    }

    public class AuthResult : Result
    {
        public string uid;
        public string email;
        public string idToken;
        public string refreshToken;
        public string displayName;
    }

    private static Config config;
    private static string idToken;
    public static string AuthUid { get { return authUid; } }
    private static string authUid;
    private static string refreshToken;
    private static DateTime tokenExpiry = DateTime.MinValue;

    public static bool IsConfigured
    {
        get
        {
            LoadConfig();
            return config != null && !string.IsNullOrEmpty(config.apiKey);
        }
    }

    private static void LoadConfig()
    {
        if (config != null)
            return;

        TextAsset asset = Resources.Load<TextAsset>("MenuUI/firebase_config");
        if (asset != null)
            config = JsonUtility.FromJson<Config>(asset.text);
    }

    public static string WebClientId
    {
        get
        {
            LoadConfig();
            return config != null ? config.webClientId : null;
        }
    }

    // Requests run on their own object that survives scene changes, so a save started at the end of a mission (just before
    // going back to the menu) still finishes.
    private class RunnerBehaviour : MonoBehaviour { }

    private static MonoBehaviour runner;

    internal static MonoBehaviour Runner
    {
        get
        {
            if (runner == null)
            {
                GameObject go = new GameObject("FirebaseRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<RunnerBehaviour>();
            }

            return runner;
        }
    }

    // ---- Authentication --------------------------------------------------------------------------------------

    public static void SignIn(string email, string password, Action<AuthResult> done)
    {
        Runner.StartCoroutine(AuthCall("accounts:signInWithPassword", email, password, done));
    }

    public static void SignUp(string email, string password, Action<AuthResult> done)
    {
        Runner.StartCoroutine(AuthCall("accounts:signUp", email, password, done));
    }

    private static IEnumerator AuthCall(string method, string email, string password, Action<AuthResult> done)
    {
        AuthResult result = new AuthResult();

        if (!IsConfigured)
        {
            result.error = "Firebase is not set up in Unity. Run Tools > Firewall Duty > Menu UI > Import Android Assets.";
            done(result);
            yield break;
        }

        string body = MiniJson.Serialize(new Dictionary<string, object> { { "email", email }, { "password", password }, { "returnSecureToken", true } });
        string url = "https://identitytoolkit.googleapis.com/v1/" + method + "?key=" + config.apiKey;

        using (UnityWebRequest request = PostJson(url, body))
        {
            yield return request.SendWebRequest();

            Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.error = FriendlyAuthError(request, json);
                done(result);
                yield break;
            }

            result.ok = true;
            result.uid = MiniJson.GetString(json, "localId");
            result.email = MiniJson.GetString(json, "email");
            result.idToken = MiniJson.GetString(json, "idToken");
            result.refreshToken = MiniJson.GetString(json, "refreshToken");
            result.displayName = MiniJson.GetString(json, "displayName");

            idToken = result.idToken;
            authUid = result.uid;
            refreshToken = result.refreshToken;
            tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, ParseSeconds(MiniJson.GetString(json, "expiresIn")) - 60));
            done(result);
        }
    }

    // Google sign-in: the phone's own Google screen gives an ID token; Firebase exchanges it for an account
    // (the same thing GoogleAuthProvider.getCredential + signInWithCredential does in the Android app).
    public static void SignInWithGoogle(string googleIdToken, Action<AuthResult> done)
    {
        Runner.StartCoroutine(GoogleCall("id_token=" + googleIdToken + "&providerId=google.com", done));
    }

    // Facebook sign-in: the access token Facebook gave is exchanged for a Firebase account.
    public static void SignInWithFacebook(string facebookAccessToken, Action<AuthResult> done)
    {
        Runner.StartCoroutine(GoogleCall("access_token=" + facebookAccessToken + "&providerId=facebook.com", done));
    }

    // Despite the name this is the same call for any provider; postBody says which one and carries its token.
    private static IEnumerator GoogleCall(string providerBody, Action<AuthResult> done)
    {
        AuthResult result = new AuthResult();

        if (!IsConfigured)
        {
            result.error = "Firebase is not set up in Unity. Run Tools > Firewall Duty > Menu UI > Import Android Assets.";
            done(result);
            yield break;
        }

        string body = MiniJson.Serialize(new Dictionary<string, object>
        {
            { "postBody", providerBody },
            { "requestUri", "http://localhost" },
            { "returnIdpCredential", true },
            { "returnSecureToken", true }
        });

        using (UnityWebRequest request = PostJson("https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=" + config.apiKey, body))
        {
            yield return request.SendWebRequest();

            Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.error = FriendlyAuthError(request, json);
                done(result);
                yield break;
            }

            result.ok = true;
            result.uid = MiniJson.GetString(json, "localId");
            result.email = MiniJson.GetString(json, "email");
            result.idToken = MiniJson.GetString(json, "idToken");
            result.refreshToken = MiniJson.GetString(json, "refreshToken");
            result.displayName = MiniJson.GetString(json, "displayName");

            idToken = result.idToken;
            authUid = result.uid;
            refreshToken = result.refreshToken;
            tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, ParseSeconds(MiniJson.GetString(json, "expiresIn")) - 60));
            done(result);
        }
    }

    public static string RefreshToken => refreshToken;

    // Signs in again from a refresh token saved on the phone (no password, no Google/Facebook screen).
    public static void SignInWithRefreshToken(string savedToken, Action<AuthResult> done)
    {
        Runner.StartCoroutine(RefreshCall(savedToken, done));
    }

    private static IEnumerator RefreshCall(string savedToken, Action<AuthResult> done)
    {
        AuthResult result = new AuthResult();

        if (!IsConfigured || string.IsNullOrEmpty(savedToken))
        {
            result.error = "No saved sign-in.";
            done(result);
            yield break;
        }

        string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(savedToken);

        using (UnityWebRequest request = new UnityWebRequest("https://securetoken.googleapis.com/v1/token?key=" + config.apiKey, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            yield return request.SendWebRequest();

            Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);

            if (request.result != UnityWebRequest.Result.Success || MiniJson.GetString(json, "id_token") == null)
            {
                result.error = request.result == UnityWebRequest.Result.ConnectionError ? "No internet connection." : "The saved sign-in expired.";
                done(result);
                yield break;
            }

            result.ok = true;
            result.uid = MiniJson.GetString(json, "user_id");
            result.idToken = MiniJson.GetString(json, "id_token");
            result.refreshToken = MiniJson.GetString(json, "refresh_token") ?? savedToken;

            idToken = result.idToken;
            authUid = result.uid;
            refreshToken = result.refreshToken;
            tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, ParseSeconds(MiniJson.GetString(json, "expires_in")) - 60));
            done(result);
        }
    }

    public static void SendPasswordReset(string email, Action<Result> done)
    {
        Runner.StartCoroutine(PasswordResetCall(email, done));
    }

    private static IEnumerator PasswordResetCall(string email, Action<Result> done)
    {
        Result result = new Result();

        if (!IsConfigured)
        {
            result.error = "Firebase is not set up in Unity.";
            done(result);
            yield break;
        }

        string body = MiniJson.Serialize(new Dictionary<string, object> { { "requestType", "PASSWORD_RESET" }, { "email", email } });
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=" + config.apiKey;

        using (UnityWebRequest request = PostJson(url, body))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                result.error = FriendlyAuthError(request, MiniJson.ParseObject(request.downloadHandler.text));
            else
                result.ok = true;

            done(result);
        }
    }

    public static void SignOut()
    {
        idToken = null;
        refreshToken = null;
        tokenExpiry = DateTime.MinValue;
    }

    // Tokens last an hour; get a new one quietly when it is close to running out.
    private static IEnumerator EnsureToken()
    {
        if (string.IsNullOrEmpty(refreshToken) || DateTime.UtcNow < tokenExpiry)
            yield break;

        string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(refreshToken);
        string url = "https://securetoken.googleapis.com/v1/token?key=" + config.apiKey;

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Dictionary<string, object> json = MiniJson.ParseObject(request.downloadHandler.text);
                idToken = MiniJson.GetString(json, "id_token") ?? idToken;
                refreshToken = MiniJson.GetString(json, "refresh_token") ?? refreshToken;
                tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, ParseSeconds(MiniJson.GetString(json, "expires_in")) - 60));
            }
        }
    }

    // ---- Firestore: players ----------------------------------------------------------------------------------

    public static void GetPlayer(string uid, Action<Result, PlayerData> done)
    {
        Runner.StartCoroutine(GetPlayerCall(uid, done));
    }

    private static IEnumerator GetPlayerCall(string uid, Action<Result, PlayerData> done)
    {
        Result result = new Result();
        yield return EnsureToken();

        using (UnityWebRequest request = UnityWebRequest.Get(DocUrl(uid)))
        {
            Authorize(request);
            yield return request.SendWebRequest();

            if (request.responseCode == 404)
            {
                result.ok = true;   // signed in, but no record yet
                done(result, null);
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.error = FriendlyFirestoreError(request);
                done(result, null);
                yield break;
            }

            result.ok = true;
            done(result, PlayerData.FromDocument(MiniJson.ParseObject(request.downloadHandler.text)));
        }
    }

    // Creates or updates the named fields of players/{uid}; other fields are left alone.
    public static void SetPlayerFields(string uid, Dictionary<string, object> fields, Action<Result> done)
    {
        Runner.StartCoroutine(SetFieldsCall(uid, fields, done));
    }

    private static IEnumerator SetFieldsCall(string uid, Dictionary<string, object> fields, Action<Result> done)
    {
        Result result = new Result();
        yield return EnsureToken();

        StringBuilder mask = new StringBuilder();
        Dictionary<string, object> typed = new Dictionary<string, object>();

        foreach (KeyValuePair<string, object> field in fields)
        {
            typed[field.Key] = ToFirestoreValue(field.Value);
            mask.Append(mask.Length == 0 ? "?" : "&").Append("updateMask.fieldPaths=").Append(UnityWebRequest.EscapeURL(field.Key));
        }

        string body = MiniJson.Serialize(new Dictionary<string, object> { { "fields", typed } });

        using (UnityWebRequest request = new UnityWebRequest(DocUrl(uid) + mask, "PATCH"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            Authorize(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                result.error = FriendlyFirestoreError(request);
            else
                result.ok = true;

            done?.Invoke(result);
        }
    }

    // The top players by experience (the leaderboard).
    public static void TopPlayers(int limit, Action<Result, List<PlayerData>> done)
    {
        Runner.StartCoroutine(TopPlayersCall(limit, done));
    }

    private static IEnumerator TopPlayersCall(int limit, Action<Result, List<PlayerData>> done)
    {
        Result result = new Result();
        yield return EnsureToken();

        Dictionary<string, object> query = new Dictionary<string, object>
        {
            { "structuredQuery", new Dictionary<string, object>
                {
                    { "from", new List<object> { new Dictionary<string, object> { { "collectionId", "players" } } } },
                    { "orderBy", new List<object> { new Dictionary<string, object>
                        {
                            { "field", new Dictionary<string, object> { { "fieldPath", "exp" } } },
                            { "direction", "DESCENDING" }
                        } } },
                    { "limit", limit }
                }
            }
        };

        string url = "https://firestore.googleapis.com/v1/projects/" + config.projectId + "/databases/(default)/documents:runQuery";

        using (UnityWebRequest request = PostJson(url, MiniJson.Serialize(query)))
        {
            Authorize(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                result.error = FriendlyFirestoreError(request);
                done(result, null);
                yield break;
            }

            List<PlayerData> players = new List<PlayerData>();
            List<object> rows = MiniJson.Parse(request.downloadHandler.text) as List<object>;

            if (rows != null)
            {
                foreach (object row in rows)
                {
                    object doc = MiniJson.Get(row, "document");
                    if (doc != null)
                        players.Add(PlayerData.FromDocument(doc as Dictionary<string, object>));
                }
            }

            result.ok = true;
            done(result, players);
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------

    private static string DocUrl(string uid)
    {
        return "https://firestore.googleapis.com/v1/projects/" + config.projectId + "/databases/(default)/documents/players/" + UnityWebRequest.EscapeURL(uid);
    }

    private static UnityWebRequest PostJson(string url, string body)
    {
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    private static void Authorize(UnityWebRequest request)
    {
        if (!string.IsNullOrEmpty(idToken))
            request.SetRequestHeader("Authorization", "Bearer " + idToken);
    }

    private static object ToFirestoreValue(object v)
    {
        if (v == null) return new Dictionary<string, object> { { "nullValue", null } };
        if (v is string s) return new Dictionary<string, object> { { "stringValue", s } };
        if (v is bool b) return new Dictionary<string, object> { { "booleanValue", b } };
        if (v is int || v is long) return new Dictionary<string, object> { { "integerValue", Convert.ToString(v) } };
        if (v is float || v is double) return new Dictionary<string, object> { { "doubleValue", Convert.ToDouble(v) } };

        if (v is IEnumerable list)
        {
            List<object> values = new List<object>();
            foreach (object item in list)
                values.Add(ToFirestoreValue(item));

            return new Dictionary<string, object> { { "arrayValue", new Dictionary<string, object> { { "values", values } } } };
        }

        return new Dictionary<string, object> { { "stringValue", v.ToString() } };
    }

    private static double ParseSeconds(string s)
    {
        return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 3600.0;
    }

    // Firebase answers with codes like EMAIL_NOT_FOUND; show something a player can act on.
    private static string FriendlyAuthError(UnityWebRequest request, Dictionary<string, object> json)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return "No internet connection.";

        string code = MiniJson.GetString(json, "error", "message") ?? "";

        if (code.StartsWith("EMAIL_NOT_FOUND")) return "No account found with that email.";
        if (code.StartsWith("INVALID_PASSWORD") || code.StartsWith("INVALID_LOGIN_CREDENTIALS")) return "Wrong Password";
        if (code.StartsWith("USER_DISABLED")) return "This account has been disabled.";
        if (code.StartsWith("EMAIL_EXISTS")) return "An account with this email already exists.";
        if (code.StartsWith("WEAK_PASSWORD")) return "Password should be at least 6 characters.";
        if (code.StartsWith("INVALID_EMAIL")) return "Please enter a valid email address.";
        if (code.StartsWith("TOO_MANY_ATTEMPTS")) return "Too many attempts. Please try again later.";
        if (code.StartsWith("OPERATION_NOT_ALLOWED")) return "Email and password sign-in is not enabled for this project.";

        return string.IsNullOrEmpty(code) ? "Request failed (" + request.responseCode + ")." : code;
    }

    private static string FriendlyFirestoreError(UnityWebRequest request)
    {
        if (request.result == UnityWebRequest.Result.ConnectionError)
            return "No internet connection.";

        string message = MiniJson.GetString(MiniJson.ParseObject(request.downloadHandler.text), "error", "message");
        return string.IsNullOrEmpty(message) ? "Request failed (" + request.responseCode + ")." : message;
    }
}
