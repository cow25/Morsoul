using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    public string speakerName = "Eliza";
    [TextArea] public string dialogueText = "You're awake. Good.";

    public void TriggerDialogue()
    {
        Debug.Log($"{speakerName}: {dialogueText}");
    }
}
