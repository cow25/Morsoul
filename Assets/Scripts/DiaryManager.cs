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
            Debug.Log($"Added to Diary: {clue.id}. Total clues: {collectedClues.Count}");
        }
    }

    public bool HasClue(string clueId)
    {
        return collectedClues.Exists(c => c.id == clueId);
    }
}