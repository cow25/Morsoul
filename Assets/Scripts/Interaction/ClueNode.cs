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

    public void Collect()
    {
        if (state == State.Collected) return;
        state = State.Collected;
        Debug.Log($"Collected Clue: {id}");
        // Later, we will call GameManager and DiaryManager here
    }
}