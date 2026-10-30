using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string interactionName = "Interact";
    [Tooltip("How close the player must be (meters, measured from the camera to the point the crosshair hits) for this object to be targeted.")]
    [SerializeField, Min(0.1f)] private float interactionRange = 3f;
    [Tooltip("Show the outline glow while the crosshair targets this object.")]
    [SerializeField] private bool highlightWhenTargeted = true;

    private InteractableHighlight highlight;

    public string InteractionName => interactionName;
    public float InteractionRange => interactionRange;

    // Override to make an object temporarily non-interactive (e.g. a locked door). It is then neither highlighted nor triggered.
    public virtual bool CanInteract => true;

    // When two targets are both under the crosshair, the one with the higher priority wins (a computer to inspect beats the person sitting at it).
    public virtual int Priority => 0;

    // Override to glow only some of the time (a character glows only when it is their turn to be talked to). Checked every frame while targeted.
    public virtual bool WantsHighlight => true;

    // Optional text for UI that wants to describe the target (the hand button itself shows no text).
    public virtual string PromptText => interactionName;

    public virtual void Interact()
    {
        UnityEngine.Debug.Log("Interacted with: " + interactionName);
    }

    // Called by PlayerInteraction. Override when the interaction depends on who/where the player is.
    public virtual void Interact(Transform interactor)
    {
        Interact();
    }

    // For objects with nothing to outline (an invisible box over a screen).
    public void DisableHighlight()
    {
        highlightWhenTargeted = false;
    }

    // Called by PlayerInteraction when this object gains or loses the crosshair.
    public void SetHighlighted(bool value)
    {
        if (value && !highlightWhenTargeted)
            return;

        if (highlight == null)
        {
            if (!value)
                return;

            highlight = GetComponent<InteractableHighlight>();
            if (highlight == null)
                highlight = gameObject.AddComponent<InteractableHighlight>();
        }

        highlight.SetHighlighted(value);
    }

    protected virtual void OnDisable()
    {
        SetHighlighted(false);
    }
}
