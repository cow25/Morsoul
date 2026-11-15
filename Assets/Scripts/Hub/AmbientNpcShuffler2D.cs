using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub
{
    /// <summary>
    /// Shuffles non-essential ambient/prop NPCs across safe floor spots on each
    /// hub visit so the Guild and regional hubs feel alive without ever moving
    /// key characters, clerks, vendors, suspects, or quest interactables.
    /// </summary>
    public class AmbientNpcShuffler2D : MonoBehaviour
    {
        static readonly string[] ExcludedKeywords =
        {
            "Player", "Eliza", "Carrier", "Ovan", "Clerk", "Reception",
            "Vendor", "Merchant", "Shop", "Stall", "Altar", "Lyra",
            "Sylvan", "Accord", "Ignatius", "Vane", "Harlan", "Thrun",
            "Kaelen", "Brokk", "Valerius", "Board", "Desk", "Door",
            "Gate", "Lift", "Rest", "Well", "Fire", "Prop",
            "Barkeeper", "Archivist"
        };

        static readonly string[] AmbientKeywords =
        {
            "Crowd", "Townsperson", "Patron", "Scholar", "Waiter",
            "Acolyte", "Novice", "Guard", "Watchman", "Ranger",
            "Miner", "Citizen", "Villager", "Worker", "Student",
            "Stroller", "Ambient", "Townsfolk", "Adventurer", "Briefing"
        };

        void Start()
        {
            ShuffleAmbientNpcs(SceneManager.GetActiveScene().name);
        }

        public static void ShuffleAmbientNpcs(string sceneName)
        {
            var visuals = FindObjectsByType<EntityVisual>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var candidates = new List<Transform>();
            var pool = new List<Vector3>();

            for (int i = 0; i < visuals.Length; i++)
            {
                var v = visuals[i];
                if (v == null || !IsEligibleAmbientNpc(v.gameObject)) continue;
                candidates.Add(v.transform);
                pool.Add(v.transform.position);
            }

            if (candidates.Count == 0) return;

            AddSceneExtraSpots(sceneName, pool);

            // Fisher-Yates shuffle over the combined pool of safe spots.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform t = candidates[i];
                Vector3 chosen = pool[i % pool.Count];
                Vector3 jittered = chosen + new Vector3(
                    UnityEngine.Random.Range(-0.35f, 0.35f),
                    UnityEngine.Random.Range(-0.25f, 0.25f),
                    0f);

                // Keep original spot if jitter overlaps a solid collider.
                Vector3 target = IsSpotClear(jittered, t) ? jittered : chosen;
                t.position = target;

                var stroller = t.GetComponent<AmbientStroller2D>();
                if (stroller != null && stroller.waypoints != null && stroller.waypoints.Length > 1)
                {
                    // Offset stroller waypoints relative to its new home spot.
                    Vector2 delta = (Vector2)target - stroller.waypoints[0];
                    for (int w = 0; w < stroller.waypoints.Length; w++)
                        stroller.waypoints[w] += delta;
                }
            }
        }

        public static bool IsEligibleAmbientNpc(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<HubPlayerController>() != null) return false;
            if (go.GetComponent<CarrierInteractable>() != null) return false;
            if (go.GetComponent<GateInteractable>() != null) return false;
            if (go.GetComponent<StairInteractable>() != null) return false;
            if (go.GetComponent<InspectionInteractable>() != null) return false;
            if (go.GetComponent<RestSpot>() != null) return false;
            if (go.GetComponent<ElizaTalkInteractable>() != null) return false;
            if (go.GetComponent<ElizaCompanionFollower>() != null) return false;

            var npc = go.GetComponent<NpcInteractable>();
            if (npc != null && npc.interrogationCaseId >= 0) return false;

            string name = go.name;
            for (int i = 0; i < ExcludedKeywords.Length; i++)
            {
                if (name.IndexOf(ExcludedKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            if (go.GetComponent<AmbientStroller2D>() != null)
                return true;

            for (int i = 0; i < AmbientKeywords.Length; i++)
            {
                if (name.IndexOf(AmbientKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        static bool IsSpotClear(Vector3 pos, Transform self)
        {
            var hit = Physics2D.OverlapCircle(pos, 0.32f);
            return hit == null || hit.isTrigger || hit.transform == self;
        }

        static void TryAddSpot(List<Vector3> pool, Vector3 pos)
        {
            if (IsSpotClear(pos, null)) pool.Add(pos);
        }

        static void AddSceneExtraSpots(string sceneName, List<Vector3> pool)
        {
            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.2f, 2.5f, 0f));
                TryAddSpot(pool, new Vector3(4.5f, 1.8f, 0f));
                TryAddSpot(pool, new Vector3(-3.0f, 6.2f, 0f));
                TryAddSpot(pool, new Vector3(1.5f, 2.2f, 0f));
                TryAddSpot(pool, new Vector3(5.2f, 6.0f, 0f));
            }
            else if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-6.8f, 5.8f, 0f));
                TryAddSpot(pool, new Vector3(6.5f, 5.2f, 0f));
                TryAddSpot(pool, new Vector3(6.2f, -4.5f, 0f));
                TryAddSpot(pool, new Vector3(-6.2f, -4.8f, 0f));
                TryAddSpot(pool, new Vector3(-13.5f, -1.2f, 0f));
            }
            else if (string.Equals(sceneName, "Hub_CathedralCourtyard", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-3.2f, 1.5f, 0f));
                TryAddSpot(pool, new Vector3(4.0f, -1.2f, 0f));
                TryAddSpot(pool, new Vector3(-1.5f, -2.5f, 0f));
            }
            else if (string.Equals(sceneName, "Hub_CathedralNave", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-6.5f, -1.5f, 0f));
                TryAddSpot(pool, new Vector3(7.0f, -2.5f, 0f));
                TryAddSpot(pool, new Vector3(-6.2f, -7.2f, 0f));
                TryAddSpot(pool, new Vector3(6.8f, -7.0f, 0f));
            }
            else if (string.Equals(sceneName, "Hub_Wilderness", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.5f, 1.8f, 0f));
                TryAddSpot(pool, new Vector3(-2.0f, -2.5f, 0f));
                TryAddSpot(pool, new Vector3(1.8f, 1.4f, 0f));
            }
            else if (string.Equals(sceneName, "Hub_IronHollow", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.0f, 1.5f, 0f));
                TryAddSpot(pool, new Vector3(3.5f, -1.2f, 0f));
                TryAddSpot(pool, new Vector3(-1.8f, -2.2f, 0f));
            }
        }
    }
}
