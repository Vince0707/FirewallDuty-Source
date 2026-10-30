package com.firewallduty.bridge;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;
import android.os.CancellationSignal;
import android.util.Base64;

import androidx.core.content.ContextCompat;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;

// The two things the menu needs from the phone that Unity cannot do by itself, done exactly as the Android Studio app does them:
//   - Google sign-in (Credential Manager, "Sign in with Google")
//   - choosing a profile photo (the document picker)
// Results go back to the Unity object named by `receiver` as "ok|value" or "error|message".
public class FirewallBridge {

    // ---------------------------------------------------------------- Google

    public static void googleSignIn(final String webClientId, final String receiver) {
        final Activity activity = UnityPlayer.currentActivity;

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    GetSignInWithGoogleOption option =
                            new GetSignInWithGoogleOption.Builder(webClientId).build();

                    GetCredentialRequest request = new GetCredentialRequest.Builder()
                            .addCredentialOption(option)
                            .build();

                    CredentialManager manager = CredentialManager.create(activity);

                    manager.getCredentialAsync(
                            activity,
                            request,
                            new CancellationSignal(),
                            ContextCompat.getMainExecutor(activity),
                            new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                                @Override
                                public void onResult(GetCredentialResponse response) {
                                    Credential credential = response.getCredential();

                                    if (credential instanceof CustomCredential
                                            && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
                                            .equals(credential.getType())) {
                                        try {
                                            GoogleIdTokenCredential google =
                                                    GoogleIdTokenCredential.createFrom(credential.getData());
                                            String token = google.getIdToken();

                                            if (token == null || token.isEmpty()) {
                                                send(receiver, "OnGoogleResult", "error|Google ID token is empty.");
                                            } else {
                                                send(receiver, "OnGoogleResult", "ok|" + token);
                                            }
                                        } catch (Exception e) {
                                            send(receiver, "OnGoogleResult", "error|Google credential error: " + describe(e));
                                        }
                                    } else {
                                        send(receiver, "OnGoogleResult", "error|Unsupported Google credential.");
                                    }
                                }

                                @Override
                                public void onError(GetCredentialException exception) {
                                    send(receiver, "OnGoogleResult", "error|Google sign-in failed: " + describe(exception));
                                }
                            });
                } catch (Exception e) {
                    send(receiver, "OnGoogleResult", "error|Google error: " + describe(e));
                }
            }
        });
    }

    // ----------------------------------------------------------------- Photo

    public static void pickPhoto(final String receiver) {
        final Activity activity = UnityPlayer.currentActivity;

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    PickerFragment fragment = new PickerFragment();
                    fragment.receiver = receiver;
                    activity.getFragmentManager().beginTransaction().add(fragment, "firewall_picker").commit();
                } catch (Exception e) {
                    send(receiver, "OnPhotoResult", "error|" + describe(e));
                }
            }
        });
    }

    // A fragment with no screen: it only exists to receive the result of the picker.
    public static class PickerFragment extends Fragment {
        private static final int REQUEST = 4711;
        public String receiver;

        @Override
        public void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);

            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("image/*");
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            startActivityForResult(intent, REQUEST);
        }

        @Override
        public void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);

            if (requestCode == REQUEST) {
                if (resultCode == Activity.RESULT_OK && data != null && data.getData() != null) {
                    Uri uri = data.getData();

                    try {
                        // Keep permission to read this image on this device, like the Android app.
                        getActivity().getContentResolver().takePersistableUriPermission(
                                uri, Intent.FLAG_GRANT_READ_URI_PERMISSION);
                    } catch (Exception ignored) {
                        // Continue if a persistable permission is unavailable.
                    }

                    send(receiver, "OnPhotoResult", "ok|" + uri.toString());
                } else {
                    send(receiver, "OnPhotoResult", "cancel|");
                }

                getActivity().getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            }
        }
    }

    // The picture behind a photo uri, scaled down and as base64 JPEG ("" if it cannot be read).
    public static String readPhoto(String uriText, int maxSize) {
        try {
            Uri uri = Uri.parse(uriText);
            Activity activity = UnityPlayer.currentActivity;

            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream first = activity.getContentResolver().openInputStream(uri);
            BitmapFactory.decodeStream(first, null, bounds);
            if (first != null) first.close();

            int sample = 1;
            while (bounds.outWidth / sample > maxSize * 2 || bounds.outHeight / sample > maxSize * 2) {
                sample *= 2;
            }

            BitmapFactory.Options options = new BitmapFactory.Options();
            options.inSampleSize = sample;
            InputStream second = activity.getContentResolver().openInputStream(uri);
            Bitmap bitmap = BitmapFactory.decodeStream(second, null, options);
            if (second != null) second.close();

            if (bitmap == null) return "";

            float scale = Math.min(1f, (float) maxSize / Math.max(bitmap.getWidth(), bitmap.getHeight()));
            if (scale < 1f) {
                bitmap = Bitmap.createScaledBitmap(bitmap,
                        Math.max(1, Math.round(bitmap.getWidth() * scale)),
                        Math.max(1, Math.round(bitmap.getHeight() * scale)), true);
            }

            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            bitmap.compress(Bitmap.CompressFormat.JPEG, 90, bytes);
            return Base64.encodeToString(bytes.toByteArray(), Base64.NO_WRAP);
        } catch (Exception e) {
            return "";
        }
    }

    // ---------------------------------------------------------------- Helpers

    private static void send(String receiver, String method, String message) {
        UnityPlayer.UnitySendMessage(receiver, method, message);
    }

    private static String describe(Exception e) {
        if (e == null) return "Unknown error.";
        String message = e.getMessage();
        if (message == null || message.trim().isEmpty()) return e.getClass().getSimpleName();
        return message;
    }
}
