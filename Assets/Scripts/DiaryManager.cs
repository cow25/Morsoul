using UnityEngine;
using System.Collections.Generic;

public class DiaryManager : MonoBehaviour
{
    public List<ClueNode> collectedClues = new List<ClueNode>();

    public void AddClue(ClueNode clue)
    {
        if (!collectedClues.Contains(clue))
        {
            collectedClues.Add(clue);
            Debug.Log($"Added to Diary: {clue.id}. Total clues: {collectedClues.Count}");
        }
    }
}