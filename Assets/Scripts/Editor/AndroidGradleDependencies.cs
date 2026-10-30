#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

// Android builds only. Assets/Plugins/Android/FirewallBridge.java uses Google's Credential Manager ("Sign in with Google"), which lives in libraries
// Unity does not add by itself; without them Gradle stops with "package androidx.credentials does not exist". This adds them to the exported project
// just before Gradle runs, so no hand-edited gradle template is needed. It does nothing a second time.
public class AndroidGradleDependencies : IPostGenerateGradleAndroidProject
{
    private const string Marker = "// FirewallDuty: Google sign-in libraries";

    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // "path" is the unityLibrary module (the one that contains the plugin's .java file).
        string gradleFile = Path.Combine(path, "build.gradle");
        if (!File.Exists(gradleFile))
        {
            Debug.LogWarning("[Firewall Duty] Could not find " + gradleFile + ", so the Google sign-in libraries were not added.");
            return;
        }

        string text = File.ReadAllText(gradleFile);
        if (text.Contains(Marker))
            return;

        text += "\n" + Marker + "\n" +
                "dependencies {\n" +
                "    implementation 'androidx.credentials:credentials:1.3.0'\n" +
                "    implementation 'androidx.credentials:credentials-play-services-auth:1.3.0'\n" +
                "    implementation 'com.google.android.libraries.identity.googleid:googleid:1.1.1'\n" +
                "    implementation 'androidx.core:core:1.13.1'\n" +
                "}\n";

        File.WriteAllText(gradleFile, text);
        Debug.Log("[Firewall Duty] Added the Google sign-in libraries to " + gradleFile);
    }
}
#endif
