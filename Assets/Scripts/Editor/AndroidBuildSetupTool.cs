using System.IO;
using UnityEditor;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif
using UnityEngine;

// Tools > Firewall Duty > Android > Prepare Android Build
// Gives the Unity build the same identity as the Android Studio app (so Firebase and Google sign-in accept it), landscape only,
// with the scenes in order. The dependencies for Google sign-in are added to every Android build by the post-processor below.
public static class AndroidBuildSetupTool
{
    public const string PackageName = "com.example.firewall_3d";   // package_name in google-services.json

    [MenuItem("Tools/Firewall Duty/Android/Prepare Android Build")]
    public static void Prepare()
    {
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, PackageName);
        PlayerSettings.productName = "Firewall Duty";

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel25)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;

        string report = MenuSceneTool.SetBuildList();

        Debug.Log("Android build prepared.\n" +
                  "  Package name: " + PackageName + " (same as the Android Studio app)\n" +
                  "  Orientation: landscape\n" +
                  "  Minimum Android: API 25\n" + report +
                  "\nFor Google sign-in: add the SHA-1 of the keystore you build with to the Firebase project\n" +
                  "(Firebase console > Project settings > Your apps > com.example.firewall_3d > Add fingerprint),\n" +
                  "then download nothing new - the web client id is already in google-services.json.");
    }
}

#if UNITY_ANDROID
// Adds the Google sign-in libraries (the same ones the Android Studio app uses) to the exported Gradle project.
public class FirewallGradleDependencies : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string gradle = Path.Combine(path, "build.gradle");
        if (!File.Exists(gradle))
            return;

        string text = File.ReadAllText(gradle);
        if (text.Contains("androidx.credentials:credentials"))
            return;

        const string marker = "dependencies {";
        int at = text.IndexOf(marker, System.StringComparison.Ordinal);
        if (at < 0)
        {
            Debug.LogWarning("[FirewallDuty] Could not add the Google sign-in libraries: no 'dependencies {' block in " + gradle);
            return;
        }

        string add = "\n    implementation 'androidx.credentials:credentials:1.3.0'" +
                     "\n    implementation 'androidx.credentials:credentials-play-services-auth:1.3.0'" +
                     "\n    implementation 'com.google.android.libraries.identity.googleid:googleid:1.1.1'";

        File.WriteAllText(gradle, text.Insert(at + marker.Length, add));
    }
}
#endif
