using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// activity_leaderboard.xml / LeaderboardActivity.java: every player, highest EXP first (level breaks ties).
public class LeaderboardScreen : MenuScreen
{
    private RectTransform rows;
    private int visit;

    protected override void Build(RectTransform root)
    {
        UiKit.Cover(root, "Background", UiKit.Picture("login_bg"));
        Image overlay = UiKit.Solid(root, "Overlay", UiKit.Hex("#70000000"));
        UiKit.Stretch(overlay.rectTransform);

        RectTransform frame = UiLayout.Frame(root);

        Image title = UiKit.Pic(frame, "Title", UiKit.Shape("leaderboard_title_bg"));
        UiLayout.TopCenter(title.rectTransform, 12f, 300f, 40f);
        TextMeshProUGUI titleText = UiKit.Text(title.transform, "LEADERBOARDS", 20f, Color.white, FontStyles.Bold);
        UiLayout.Spaced(titleText, 0.16f);
        UiKit.Stretch(titleText.rectTransform);

        UiLayout.BackButton(frame, 1f, 40f, 110f, 45f, 15f, () => App.Back());

        // The table: left 150, right 45, top 75, bottom 65.
        Image table = UiKit.Pic(frame, "Table", UiKit.Shape("leaderboard_table_bg"));
        UiKit.Stretch(table.rectTransform, 150f, 65f, 45f, 75f);

        Image header = UiKit.Solid(table.transform, "Header", UiKit.Hex("#123858"));
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = new Vector2(1f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.offsetMin = new Vector2(0f, -50f);
        header.rectTransform.offsetMax = Vector2.zero;
        header.gameObject.AddComponent<RectMask2D>();

        RectTransform headerRow = UiKit.HBox(header.transform, "Cells", 0f, TextAnchor.MiddleCenter, false, false);
        UiKit.Stretch(headerRow);
        headerRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        headerRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        Cell(headerRow, "RANK", 1f, 14f, UiKit.Hex("#9DB9D0"), 0.08f);
        Cell(headerRow, "PLAYER", 2f, 14f, UiKit.Hex("#9DB9D0"), 0.08f);
        Cell(headerRow, "LEVEL", 1f, 14f, UiKit.Hex("#9DB9D0"), 0.08f);
        Cell(headerRow, "EXP", 1f, 14f, UiKit.Hex("#9DB9D0"), 0.08f);

        rows = UiKit.Scroll(table.transform, "Rows", 0f, 0);
        UiKit.Stretch(UiLayout.ScrollRoot(rows), 0f, 0f, 0f, 50f);
        rows.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
    }

    private static void Cell(RectTransform row, string text, float weight, float size, Color color, float spacing)
    {
        TextMeshProUGUI cell = UiKit.Text(row, text, size, color, FontStyles.Bold);
        UiLayout.Spaced(cell, spacing);
        cell.textWrappingMode = TextWrappingModes.NoWrap;
        cell.overflowMode = TextOverflowModes.Ellipsis;
        UiKit.Size(cell, 10f, -1f);
        UiKit.Flexible(cell, weight, 1f);
    }

    public override void OnShow(object arg)
    {
        Clear();
        visit++;
        int myVisit = visit;

        // Firestore rules require users to be signed in to read players.
        if (!PlayerSession.IsSignedIn)
        {
            Message("Please sign in to view the leaderboard.");
            return;
        }

        Message("Loading...");

        FirebaseRest.TopPlayers(1000, (result, players) =>
        {
            if (myVisit != visit)
                return;

            Clear();

            if (!result.ok)
            {
                Message("Unable to load leaderboard. Please try again.");
                App.Toast("Leaderboard error: " + result.error);
                return;
            }

            MergeOwnRow(players);
            Sort(players);

            if (players.Count == 0)
            {
                Message("No players found yet.");
                return;
            }

            for (int i = 0; i < players.Count; i++)
                AddRow(i + 1, DisplayName(players[i]), players[i].level, players[i].exp);
        });
    }

    // Your own row uses what this phone knows if that is ahead of the online record (a save that has not arrived yet), so the
    // leaderboard and the Profile agree.
    public static void MergeOwnRow(List<PlayerData> players)
    {
        if (!PlayerSession.IsSignedIn)
            return;

        PlayerData me = PlayerSession.Player;
        PlayerData row = players.Find(p => p.uid == me.uid);

        if (row == null)
        {
            if (me.exp > 0)
                players.Add(me);

            return;
        }

        if (me.exp > row.exp)
        {
            row.exp = me.exp;
            row.level = Mathf.Max(row.level, me.level);
        }
    }

    public static string DisplayName(PlayerData player)
    {
        if (!string.IsNullOrWhiteSpace(player.username)) return player.username;
        if (!string.IsNullOrWhiteSpace(player.fullName)) return player.fullName;
        return "PLAYER";
    }

    // Highest EXP first; level breaks ties.
    public static void Sort(List<PlayerData> players)
    {
        players.Sort((a, b) =>
        {
            int byExp = b.exp.CompareTo(a.exp);
            return byExp != 0 ? byExp : b.level.CompareTo(a.level);
        });
    }

    private void Clear()
    {
        for (int i = rows.childCount - 1; i >= 0; i--)
            Object.Destroy(rows.GetChild(i).gameObject);
    }

    private void Message(string text)
    {
        TextMeshProUGUI label = UiKit.Text(rows, text, 16f, Color.white);
        UiKit.Size(label, -1f, 60f);
    }

    private void AddRow(int rank, string player, int level, int exp)
    {
        RectTransform row = UiKit.HBox(rows, "Row" + rank, 0f, TextAnchor.MiddleCenter, false, false);
        HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
        Image background = row.gameObject.AddComponent<Image>();
        background.color = UiKit.Hex("#123858");
        UiKit.Size(row, -1f, 42f);

        Color rankColor = rank == 1 ? UiKit.Hex("#FFD400") : Color.white;
        Cell(row, rank.ToString(), 1f, 16f, rankColor, 0.10f);
        Cell(row, player, 2f, 16f, Color.white, 0.10f);
        Cell(row, level.ToString(), 1f, 16f, Color.white, 0.10f);
        Cell(row, exp.ToString(), 1f, 16f, Color.white, 0.10f);

        Image divider = UiKit.Solid(rows, "Divider", UiKit.Hex("#061B2B"));
        UiKit.Size(divider, -1f, 2f);
    }
}
