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
            var core = Morsoul.Core.CoreRuntime.Instance;
            var session = core?.Session;
            if (session != null && !string.IsNullOrEmpty(clue.id))
            {
                var resolved = core.ResolveClue(clue.id);
                if (resolved != null)
                {
                    var flow = FindAnyObjectByType<Morsoul.Flow.GameFlow>();
                    if (flow != null)
                    {
                        flow.HandleClueRegistered(resolved);
                    }
                    else
                    {
                        session.RegisterClue(resolved);
                        if (resolved.type == Morsoul.Data.ClueType.Latent)
                        {
                            session.RegisterLatentClue(resolved);
                        }
                        var owner = core.ResolveCase(resolved.caseId);
                        if (owner != null && !string.IsNullOrEmpty(owner.uneaseFlagKey))
                        {
                            session.SetFlag(owner.uneaseFlagKey, false);
                        }
                    }
                }
                else
                {
                    session.RegisterClueId(clue.id);
                    if (Morsoul.Core.SessionState.TryParseRunicLogSlot(clue.id, out int slot))
                    {
                        session.SetFlag(Morsoul.Core.SessionState.LatentLogFlagFor(slot + 1));
                        session.MarkRunicLog(slot);
                        session.LatentLogCollected = true;
                    }
                }
            }
            Debug.Log($"Added to Diary: {clue.id}. Total clues: {collectedClues.Count}");
            Morsoul.Core.AudioCoordinator.PlayBankSfx(b => b.sfxClueCollected, 0.3f);
        }
    }

    public bool HasClue(string clueId)
    {
        if (string.IsNullOrEmpty(clueId)) return false;
        if (Morsoul.Core.CoreRuntime.Instance?.Session?.HasClue(clueId) == true) return true;
        for (int i = 0; i < collectedClues.Count; i++)
        {
            var c = collectedClues[i];
            if (c != null && c.id == clueId) return true;
        }
        return false;
    }
}