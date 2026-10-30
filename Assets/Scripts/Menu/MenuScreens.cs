// Which class builds which screen. A screen that is not built yet shows a placeholder, so the flow still works end to end.
public static class MenuScreens
{
    public static MenuScreen Create(string id)
    {
        switch (id)
        {
            case "Login": return new LoginScreen();
            case "SignUp": return new SignUpScreen();
            case "ForgotPassword": return new ForgotPasswordScreen();
            case "GameMenu": return new GameMenuScreen();

            case "OperationsHub": return new OperationsHubScreen();
            case "MissionSelection": return new MissionSelectionScreen();
            case "SceneSelection": return new SceneSelectionScreen();
            case "Loading": return new LoadingScreen();
            case "Story": return new StoryScreen();
            case "MissionBriefing": return new MissionBriefingScreen();
            case "MissionOutcome": return new MissionOutcomeScreen();

            case "Continue": return new ContinueScreen();
            case "Trivia": return new TriviaScreen();
            case "Leaderboard": return new LeaderboardScreen();
            case "Profile": return new ProfileScreen();
            case "Achievements": return new AchievementsScreen();
            case "Settings": return new SettingsScreen();

            case "Tablet": return new TabletScreen();

            default: return null;
        }
    }
}
