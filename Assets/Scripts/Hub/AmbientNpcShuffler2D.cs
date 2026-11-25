using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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

            var structureTilemaps = CollectStructureTilemaps();
            for (int i = 0; i < visuals.Length; i++)
            {
                var v = visuals[i];
                if (v == null || !IsEligibleAmbientNpc(v.gameObject)) continue;
                candidates.Add(v.transform);
                if (IsSpotClear(v.transform.position, v.transform, structureTilemaps))
                    pool.Add(v.transform.position);
            }

            if (candidates.Count == 0) return;

            AddSceneExtraSpots(sceneName, pool, structureTilemaps);
            if (pool.Count == 0) return;

            // Fisher-Yates shuffle over the combined pool of safe spots.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var placed = new List<Vector3>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                Transform t = candidates[i];
                int chosenIdx = PickClearSpotIndex(pool, placed, t, structureTilemaps);
                if (chosenIdx < 0) continue;

                Vector3 chosen = pool[chosenIdx];
                pool.RemoveAt(chosenIdx);

                Vector3 jittered = chosen + new Vector3(
                    UnityEngine.Random.Range(-0.35f, 0.35f),
                    UnityEngine.Random.Range(-0.25f, 0.25f),
                    0f);

                // Keep original spot if jitter overlaps a solid collider, structure tile, or placed NPC.
                Vector3 target = IsSpotClear(jittered, t, structureTilemaps, placed) ? jittered : chosen;
                t.position = target;
                placed.Add(target);

                var stroller = t.GetComponent<AmbientStroller2D>();
                if (stroller != null && stroller.waypoints != null && stroller.waypoints.Length > 1)
                {
                    // Offset stroller waypoints relative to its new home spot if all remain clear.
                    Vector2 delta = (Vector2)target - stroller.waypoints[0];
                    bool allClear = true;
                    for (int w = 0; w < stroller.waypoints.Length; w++)
                    {
                        if (!IsSpotClear(stroller.waypoints[w] + delta, t, structureTilemaps))
                        {
                            allClear = false;
                            break;
                        }
                    }

                    if (allClear)
                    {
                        for (int w = 0; w < stroller.waypoints.Length; w++)
                            stroller.waypoints[w] += delta;
                    }
                    else
                    {
                        for (int w = 0; w < stroller.waypoints.Length; w++)
                            stroller.waypoints[w] = target;
                    }
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

        static List<Tilemap> CollectStructureTilemaps()
        {
            var list = new List<Tilemap>();
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var tm = tilemaps[i];
                if (tm == null) continue;
                string n = tm.name;
                if (n.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Structure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Roof", StringComparison.OrdinalIgnoreCase) >= 0)
                    list.Add(tm);
            }
            return list;
        }

        static int PickClearSpotIndex(List<Vector3> pool, List<Vector3> placed, Transform self, List<Tilemap> structureTilemaps)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (IsSpotClear(pool[i], self, structureTilemaps, placed))
                    return i;
            }
            return -1;
        }

        static bool IsSpotClear(Vector3 pos, Transform self, List<Tilemap> structureTilemaps, List<Vector3> placed = null)
        {
            var hits = Physics2D.OverlapCircleAll(pos, 0.45f);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.isTrigger) continue;
                if (self != null && (hit.transform == self || hit.transform.IsChildOf(self))) continue;
                // Other ambient NPCs are also being shuffled; spacing is enforced via `placed`.
                if (IsEligibleAmbientNpc(hit.gameObject)) continue;
                return false;
            }

            if (placed != null)
            {
                for (int i = 0; i < placed.Count; i++)
                {
                    if (Vector2.Distance(pos, placed[i]) < 0.9f)
                        return false;
                }
            }

            if (structureTilemaps != null)
            {
                for (int i = 0; i < structureTilemaps.Count; i++)
                {
                    var tm = structureTilemaps[i];
                    if (tm == null) continue;
                    if (tm.HasTile(tm.WorldToCell(pos)))
                        return false;
                }
            }
            return true;
        }

        static void TryAddSpot(List<Vector3> pool, Vector3 pos, List<Tilemap> structureTilemaps)
        {
            if (IsSpotClear(pos, null, structureTilemaps)) pool.Add(pos);
        }

        static void AddSceneExtraSpots(string sceneName, List<Vector3> pool, List<Tilemap> structureTilemaps)
        {
            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.2f, 2.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(4.5f, 1.8f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-3.0f, 6.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(1.5f, 2.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(5.2f, 6.0f, 0f), structureTilemaps);
            }
            else if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-6.8f, 5.8f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(6.5f, 5.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(6.2f, -4.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-6.2f, -4.8f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-13.5f, -1.2f, 0f), structureTilemaps);
            }
            else if (string.Equals(sceneName, "Hub_CathedralCourtyard", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-3.2f, 1.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(4.0f, -1.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-1.5f, -2.5f, 0f), structureTilemaps);
            }
            else if (string.Equals(sceneName, "Hub_CathedralNave", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-6.5f, -1.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(7.0f, -2.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-6.2f, -7.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(6.8f, -7.0f, 0f), structureTilemaps);
            }
            else if (string.Equals(sceneName, "Hub_Wilderness", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.5f, 1.8f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-2.0f, -2.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(1.8f, 1.4f, 0f), structureTilemaps);
            }
            else if (string.Equals(sceneName, "Hub_IronHollow", StringComparison.OrdinalIgnoreCase))
            {
                TryAddSpot(pool, new Vector3(-4.0f, 1.5f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(3.5f, -1.2f, 0f), structureTilemaps);
                TryAddSpot(pool, new Vector3(-1.8f, -2.2f, 0f), structureTilemaps);
            }
        }
    }
}
