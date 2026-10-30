using TMPro;
using UnityEngine;

// The countdown at the top of the screen, and a small "objectives done" counter. Reads everything from MissionSession.
public class MissionHud : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text objectivesText;
    [Tooltip("The timer turns red when this many seconds are left.")]
    [SerializeField, Min(0f)] private float warningSeconds = 60f;

    private string shownTime;
    private int shownObjectives = -1;

    private void Update()
    {
        MissionSession session = MissionSession.Instance;
        if (session == null)
            return;

        if (timerText != null)
        {
            string time = session.FormatTime();

            // Only touch the text when it changes (once a second), not every frame.
            if (time != shownTime)
            {
                shownTime = time;
                timerText.text = time;
                timerText.color = session.TimeRemaining <= warningSeconds ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
        }

        if (objectivesText != null)
        {
            int done = session.ObjectivesCompleted;
            if (done != shownObjectives)
            {
                shownObjectives = done;
                objectivesText.text = "Objectives " + done + " / 6";
            }
        }
    }
}
