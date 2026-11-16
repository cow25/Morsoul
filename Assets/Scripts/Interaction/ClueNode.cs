using UnityEngine;

public class ClueNode : MonoBehaviour
{
    public enum Kind { Physical, Dialogue, Item }
    public enum Tool { None, GolemEye, AetherSight, Both }
    public enum State { Hidden, Revealed, Collected }

    public string id;
    public Kind kind;
    public Tool requiredTool;
    [TextArea] public string defaultDescription;
    public State state = State.Hidden;
    
    [Tooltip("Assign a particle system to play when scanned")]
    public ParticleSystem scanParticles;

    [Tooltip("Optional visual object to display in the Inspect UI if this GameObject has no MeshRenderer")]
    public GameObject inspectVisual;

    public bool IsScanned { get; private set; }

    public void MarkScanned()
    {
        IsScanned = true;
        if (state == State.Hidden)
            state = State.Revealed;
    }

    public void Collect()
    {
        if (state == State.Collected) return;
        IsScanned = true;
        state = State.Collected;
        Debug.Log($"Collected Clue: {id}");

        if (kind == Kind.Item)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log($"{kind} clue remains in the world.");
        }
    }
}