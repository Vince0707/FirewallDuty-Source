using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// activity_profile.xml / ProfileActivity.java: name (editable), photo, level and EXP bar, global rank, and four totals.
// The screen is the Android layout itself (see AndroidLayout), so it is placed exactly as in Android Studio; this class is the
// behaviour of ProfileActivity. The medal emoji (the game font has none) is a coloured circle with the rank number.
public class ProfileScreen : MenuScreen
{
    private AndroidLayout.View layout;
    private AndroidLayout.View avatarLetter, photoView, nameText, levelText, expText, expBar, medal, rankPlace;
    private AndroidLayout.View totalPoints, missionsDone, bestScore, threatsFound;
    private RawImage photo;
    private UiWidgets.InputDialog nameDialog;
    private string currentUsername = "";
    private int visit;

    protected override void Build(RectTransform root)
    {
        // The screen-wide picture and shade replace the layout's own background views (they only fill the 914 x 411 frame).
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);
        layout = AndroidLayout.Inflate(frame, "activity_profile");
        if (layout == null)
            return;

        layout.Find("profileBackground").SetVisible(false);
        layout.Find("profileOverlay").SetVisible(false);

        avatarLetter = layout.Find("txtProfileAvatar");
        photoView = layout.Find("imgProfilePhoto");
        nameText = layout.Find("txtProfileName");
        levelText = layout.Find("txtProfileLevel");
        expText = layout.Find("txtExpProgress");
        expBar = layout.Find("progressExp");
        medal = layout.Find("txtRankMedal");
        rankPlace = layout.Find("txtRankPlace");
        totalPoints = layout.Find("txtTotalPoints");
        missionsDone = layout.Find("txtMissionsCompleted");
        bestScore = layout.Find("txtBestScore");
        threatsFound = layout.Find("txtThreatsIdentified");

        layout.Find("btnOpenAchievements").SetText("ACHIEVEMENTS");

        // A little more room for the pencil so its parts can be seen.
        layout.Find("btnEditName").attributes["padding"] = "2dp";

        // The medal: a circle with the rank number in it, where the emoji was.
        medal.attributes["layout_width"] = "40dp";
        medal.attributes["layout_height"] = "40dp";
        medal.SetBackground("profile_avatar_bg");
        medal.SetFontSize(18f);
        medal.text.fontStyle = TMPro.FontStyles.Bold;
        medal.SetText("-");

        // The photo is clipped to the circle (Android: clipToOutline) and fills it (centerCrop).
        UiKit.Ensure<Mask>(photoView.rect.gameObject).showMaskGraphic = true;
        RectTransform photoRect = UiKit.New(photoView.rect, "Photo");
        UiKit.Stretch(photoRect);
        photo = photoRect.gameObject.AddComponent<RawImage>();
        photo.raycastTarget = false;
        photo.enabled = false;

        layout.Find("btnProfileBack").SetBackground("briefing_panel_bg");   // Android's back button has no background of its own
        layout.Find("btnProfileBack").OnClick(() => App.Back());
        layout.Find("btnEditName").OnClick(ShowNameDialog);
        layout.Find("btnEditPhoto").OnClick(ChoosePhoto);
        layout.Find("btnOpenAchievements").OnClick(() => App.Show("Achievements"));

        nameDialog = UiWidgets.TextDialog(root, "Edit Display Name", "This name will also appear on the leaderboard.", "Enter your display name");
        nameDialog.Cancel.onClick.AddListener(() => nameDialog.Root.gameObject.SetActive(false));
        nameDialog.Save.onClick.AddListener(SaveName);

        layout.Relayout();
    }

    public override void OnShow(object arg)
    {
        if (layout == null)
            return;

        visit++;
        int myVisit = visit;

        if (!PlayerSession.IsSignedIn)
        {
            App.Toast("Please sign in to view your profile.");
            App.Back();
            return;
        }

        // What we already know, straight away; then the fresh record.
        Display(PlayerSession.Player);

        FirebaseRest.GetPlayer(PlayerSession.Player.uid, (result, player) =>
        {
            if (myVisit != visit)
                return;

            if (!result.ok)
            {
                App.Toast("Unable to load profile: " + result.error);
                return;
            }

            if (player == null)
            {
                App.Toast("Player profile not found.");
                return;
            }

            // SignIn keeps this phone's newer progress when the online record has not caught up yet, so show what it kept.
            PlayerSession.SignIn(player);
            Display(PlayerSession.Player);
            LoadRank(myVisit);
        });
    }

    private void Display(PlayerData player)
    {
        currentUsername = LeaderboardScreen.DisplayName(player);
        nameText.SetText(currentUsername);
        ShowInitial();

        // The photo, if it can be read on this phone.
        if (!string.IsNullOrEmpty(player.photoUri))
        {
            Texture2D texture = NativeBridge.LoadPhoto(player.photoUri);
            if (texture != null)
                ShowPhoto(texture);
        }

        levelText.SetText("LEVEL " + player.level);

        // Progress within the current 200 EXP bar.
        int inBar = player.exp % PlayerSession.ExpPerLevel;
        expBar.SetProgress(inBar / (float)PlayerSession.ExpPerLevel);
        expText.SetText(inBar + "/" + PlayerSession.ExpPerLevel + " EXP");

        totalPoints.SetText(player.totalPoints.ToString());
        missionsDone.SetText(player.missionsCompleted.ToString());
        bestScore.SetText(player.bestScore.ToString());
        threatsFound.SetText(player.threatsIdentified.ToString());

        SetRank(-1, "Unranked");
    }

    private void ShowInitial()
    {
        string trimmed = currentUsername.Trim();
        avatarLetter.SetText(trimmed.Length == 0 ? "P" : trimmed.Substring(0, 1).ToUpperInvariant());
        avatarLetter.SetVisible(true);
        photoView.SetVisible(false);
        photo.enabled = false;
        layout.Relayout();
    }

    private void ShowPhoto(Texture2D texture)
    {
        photo.texture = texture;
        photo.enabled = true;

        CoverFit fit = photo.GetComponent<CoverFit>();
        if (fit == null)
            fit = photo.gameObject.AddComponent<CoverFit>();
        fit.aspect = (float)texture.width / texture.height;

        photoView.SetVisible(true);
        avatarLetter.SetVisible(false);
        layout.Relayout();
    }

    // ---- Rank ---------------------------------------------------------------------------------------------

    private void LoadRank(int myVisit)
    {
        FirebaseRest.TopPlayers(1000, (result, players) =>
        {
            if (myVisit != visit)
                return;

            if (!result.ok)
            {
                SetRank(-1, "Unavailable");
                return;
            }

            LeaderboardScreen.MergeOwnRow(players);
            LeaderboardScreen.Sort(players);

            int rank = -1;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].uid == PlayerSession.Player.uid)
                {
                    rank = i + 1;
                    break;
                }
            }

            SetRank(rank, rank <= 0 ? "Unranked" : Ordinal(rank) + " Place");
        });
    }

    private void SetRank(int rank, string text)
    {
        Color color = UiKit.Hex("#5C7C99");
        if (rank == 1) color = UiKit.Hex("#FFC400");
        else if (rank == 2) color = UiKit.Hex("#C0C7CE");
        else if (rank == 3) color = UiKit.Hex("#CD7F32");
        else if (rank > 3) color = UiKit.Hex("#29B6F6");

        medal.SetBackgroundColor(color);
        medal.SetText(rank > 0 ? rank.ToString() : "-");
        rankPlace.SetText(text);
        layout.Relayout();
    }

    private static string Ordinal(int number)
    {
        if (number % 100 >= 11 && number % 100 <= 13) return number + "th";

        switch (number % 10)
        {
            case 1: return number + "st";
            case 2: return number + "nd";
            case 3: return number + "rd";
            default: return number + "th";
        }
    }

    // ---- Edit name ----------------------------------------------------------------------------------------

    private void ShowNameDialog()
    {
        nameDialog.Input.text = currentUsername;
        nameDialog.Error.text = "";
        nameDialog.Root.gameObject.SetActive(true);
        nameDialog.Root.SetAsLastSibling();
    }

    private void SaveName()
    {
        string newName = nameDialog.Input.text.Trim();

        if (newName.Length == 0) { nameDialog.Error.text = "Name cannot be empty"; return; }
        if (newName.Length > 24) { nameDialog.Error.text = "Use 24 characters or fewer"; return; }

        FirebaseRest.SetPlayerFields(PlayerSession.Player.uid, new Dictionary<string, object> { { "username", newName } }, result =>
        {
            if (!result.ok)
            {
                App.Toast("Could not update name: " + result.error);
                return;
            }

            PlayerSession.Player.username = newName;
            currentUsername = newName;
            nameText.SetText(newName);
            if (!photoView.IsVisible)
                ShowInitial();
            else
                layout.Relayout();

            nameDialog.Root.gameObject.SetActive(false);
            App.Toast("Display name updated!");
        });
    }

    // ---- Photo --------------------------------------------------------------------------------------------

    private void ChoosePhoto()
    {
        NativeBridge.PickPhoto((ok, value) =>
        {
            if (!ok)
            {
                if (!string.IsNullOrEmpty(value))
                    App.Toast(value);

                return;
            }

            FirebaseRest.SetPlayerFields(PlayerSession.Player.uid, new Dictionary<string, object> { { "photoUri", value } }, result =>
            {
                if (!result.ok)
                {
                    App.Toast("Could not save photo: " + result.error);
                    return;
                }

                PlayerSession.Player.photoUri = value;
                Texture2D texture = NativeBridge.LoadPhoto(value);
                if (texture != null)
                    ShowPhoto(texture);

                App.Toast("Profile photo updated!");
            });
        });
    }
}
