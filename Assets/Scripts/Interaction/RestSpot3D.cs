using Morsoul.Core;
using UnityEngine;

/// <summary>
/// 3D rest bench/campfire interactable that recovers Focus while the player
/// holds [E] while aiming at it via <see cref="InteractionProbe"/>.
/// </summary>
public class RestSpot3D : MonoBehaviour
{
    [SerializeField] private string spotLabel = "Bench";

    public string GetPromptAction()
    {
        var core = CoreRuntime.Instance;
        if (core == null) return "[Hold E] Rest";
        int rem = core.Session.GetRemainingRests(core.Session.CurrentCase);
        return rem > 0 ? $"[Hold E] Rest ({rem} left)" : "Rest exhausted for case";
    }

    public string GetPromptDescription()
    {
        var focus = CoreRuntime.Ensure()?.Focus;
        return focus != null
            ? $"{spotLabel} — Focus {Mathf.RoundToInt(focus.Percent)}/{Mathf.RoundToInt(focus.Maximum)}"
            : $"{spotLabel} — Recover Focus";
    }

    public void TickRest(float deltaTime)
    {
        var core = CoreRuntime.Ensure();
        if (core == null || core.Focus.Percent >= core.Focus.Maximum) return;
        int caseId = core.Session.CurrentCase;
        if (core.Session.GetRemainingRests(caseId) <= 0) return;
        core.Focus.TickRest(deltaTime);
    }
}

