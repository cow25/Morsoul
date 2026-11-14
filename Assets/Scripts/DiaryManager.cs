using UnityEngine;
using System.Collections.Generic;

public class DiaryManager : MonoBehaviour
{
    public static DiaryManager Instance { get; private set; }

    public List<ClueNode> collectedClues = new List<ClueNode>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        DontDestroyOnLoad(gameObject); 
    }

    public void AddClue(ClueNode clue)
    {
        if (!collectedClues.Contains(clue))
        {
            collectedClues.Add(clue);
            var session = Morsoul.Core.CoreRuntime.Instance?.Session;
            if (session != null && !string.IsNullOrEmpty(clue.id))
            {
                session.RegisterClueId(clue.id);
                if (clue.id.StartsWith("L1_")) session.SetFlag(Morsoul.Core.SessionState.LatentLogFlagFor(1));
                else if (clue.id.StartsWith("L2_")) session.SetFlag(Morsoul.Core.SessionState.LatentLogFlagFor(2));
                else if (clue.id.StartsWith("L3_")) session.SetFlag(Morsoul.Core.SessionState.LatentLogFlagFor(3));
                else if (clue.id.StartsWith("L4_")) session.SetFlag(Morsoul.Core.SessionState.LatentLogFlagFor(4));
            }
            Debug.Log($"Added to Diary: {clue.id}. Total clues: {collectedClues.Count}");
        }
    }

    public bool HasClue(string clueId)
    {
        return collectedClues.Exists(c => c.id == clueId);
    }
}