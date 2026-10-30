using System;
using UnityEngine;

// The phone-only things: Google sign-in and picking a profile photo. They run through Assets/Plugins/Android/FirewallBridge.java
// on a real Android build; in the Editor they report that they are not available.
public class NativeBridge : MonoBehaviour
{
    private static NativeBridge instance;
    private static Action<bool, string> googleDone;
    private static Action<bool, string> photoDone;

    public static bool OnDevice => Application.platform == RuntimePlatform.Android && !Application.isEditor;

    private static NativeBridge Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("NativeBridge");   // UnitySendMessage finds the receiver by this name
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NativeBridge>();
            }

            return instance;
        }
    }

    // done(true, googleIdToken) or done(false, message)
    public static void GoogleSignIn(string webClientId, Action<bool, string> done)
    {
        if (!OnDevice)
        {
            done(false, "Google sign-in only works on an Android phone (in the Editor, use email and password).");
            return;
        }

        if (string.IsNullOrEmpty(webClientId))
        {
            done(false, "Google sign-in is not set up: no web client id was found in google-services.json.");
            return;
        }

        googleDone = done;
        Call("googleSignIn", webClientId, Instance.gameObject.name);
    }

    // done(true, photoUri), done(false, "") when cancelled, or done(false, message)
    public static void PickPhoto(Action<bool, string> done)
    {
        if (!OnDevice)
        {
            done(false, "Choosing a photo only works on an Android phone.");
            return;
        }

        photoDone = done;
        Call("pickPhoto", Instance.gameObject.name);
    }

    // The picture behind a photo uri as a texture, or null when it cannot be read (for example on another phone).
    public static Texture2D LoadPhoto(string uri)
    {
        if (!OnDevice || string.IsNullOrEmpty(uri))
            return null;

        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.firewallduty.bridge.FirewallBridge"))
            {
                string base64 = bridge.CallStatic<string>("readPhoto", uri, 256);
                if (string.IsNullOrEmpty(base64))
                    return null;

                Texture2D texture = new Texture2D(2, 2);
                return texture.LoadImage(Convert.FromBase64String(base64)) ? texture : null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NativeBridge] Could not read the photo: " + e.Message);
            return null;
        }
    }

    private static void Call(string method, params object[] args)
    {
        try
        {
            using (AndroidJavaClass bridge = new AndroidJavaClass("com.firewallduty.bridge.FirewallBridge"))
                bridge.CallStatic(method, args);
        }
        catch (Exception e)
        {
            Fail(method == "googleSignIn" ? googleDone : photoDone, "The phone bridge is missing from this build: " + e.Message);
        }
    }

    private static void Fail(Action<bool, string> done, string message)
    {
        done?.Invoke(false, message);
    }

    // Called by FirewallBridge.java through UnitySendMessage.
    private void OnGoogleResult(string message)
    {
        Deliver(ref googleDone, message);
    }

    private void OnPhotoResult(string message)
    {
        Deliver(ref photoDone, message);
    }

    private static void Deliver(ref Action<bool, string> done, string message)
    {
        Action<bool, string> callback = done;
        done = null;

        int bar = message.IndexOf('|');
        string kind = bar >= 0 ? message.Substring(0, bar) : message;
        string value = bar >= 0 ? message.Substring(bar + 1) : "";

        callback?.Invoke(kind == "ok", kind == "cancel" ? "" : value);
    }
}
