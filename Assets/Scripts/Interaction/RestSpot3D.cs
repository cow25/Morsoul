using Morsoul.Core;
using UnityEngine;

/// <summary>
/// 3D rest bench/campfire interactable that recovers Focus while the player
/// holds [E] while aiming at it via <see cref="InteractionProbe"/>.
/// </summary>
public class RestSpot3D : MonoBehaviour
{
    [SerializeField] private string spotLabel = "Bench";

    public string GetPromptAction() => "[Hold E] Rest";

    public string GetPromptDescription()
    {
        var focus = CoreRuntime.Ensure()?.Focus;
        return focus != null
            ? $"{spotLabel} — Focus {Mathf.RoundToInt(focus.Percent)}/{Mathf.RoundToInt(focus.Maximum)}"
            : $"{spotLabel} — Recover Focus";
    }

    public void TickRest(float deltaTime)
    {
        CoreRuntime.Ensure()?.Focus?.TickRest(deltaTime);
    }
}

