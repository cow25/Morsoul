using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Populates the four hub art-overlay scenes and Prologue_Wilderness with
    /// generated environment dressing: a ground plane, perimeter barriers and
    /// named prop quads (trees, houses, fences, pews, mine beams...). Props
    /// live under a regenerated "Environment" root, so rerunning is idempotent
    /// and never touches hand-authored objects. Every prop carries an
    /// EntityVisual bound to a shared Assets/Art/Profiles/Prop_&lt;Type&gt;.asset —
    /// assigning a sprite to one profile upgrades every prop of that type.
    /// Blocking props get a BoxCollider2D sized to their footprint (canopies
    /// overhang; trunks block). Z is derived from Y so southern props draw in
    /// front of the player and northern props recede — a cheap top-down sort.
    /// </summary>
    public static class HubDressingBuilder
    {
        const string RootName = "Environment";
        const string ProfilesFolder = "Assets/Art/Profiles";
        const string ScenesFolder = "Assets/Scenes";

        sealed class PropDef
        {
            public Color Tint;
            public Vector2 Size;
            public Vector2 ColSize;
            public Vector2 ColOffset;
            public bool Blocks;
            public bool Ground;
        }

        // Biome-agnostic prop vocabulary. Tints only matter until a sprite is
        // assigned to the shared Prop_* profile; sizes/colliders are the
        // layout contract and should survive the art swap. ColSize/ColOffset
        // are WORLD-space footprints — PlaceProp divides by the prop scale to
        // counter the local-space multiplication BoxCollider2D applies.
        static readonly Dictionary<string, PropDef> Types = new Dictionary<string, PropDef>
        {
            ["FloorWood"]   = new PropDef { Tint = new Color(0.38f, 0.30f, 0.20f), Size = new Vector2(17f, 12f), Ground = true },
            ["WallWood"]    = new PropDef { Tint = new Color(0.40f, 0.32f, 0.24f), Size = new Vector2(1.0f, 1.0f), Blocks = true, ColSize = new Vector2(1f, 1f) },
            ["Counter"]     = new PropDef { Tint = new Color(0.45f, 0.33f, 0.20f), Size = new Vector2(2.6f, 0.8f), Blocks = true, ColSize = new Vector2(2.6f, 0.7f), ColOffset = new Vector2(0f, -0.05f) },
            ["Bookshelf"]   = new PropDef { Tint = new Color(0.32f, 0.24f, 0.16f), Size = new Vector2(1.2f, 1.6f), Blocks = true, ColSize = new Vector2(1.2f, 0.5f), ColOffset = new Vector2(0f, -0.55f) },
            ["Table"]       = new PropDef { Tint = new Color(0.48f, 0.36f, 0.22f), Size = new Vector2(1.4f, 1.0f), Blocks = true, ColSize = new Vector2(1.3f, 0.55f), ColOffset = new Vector2(0f, -0.2f) },
            ["Chair"]       = new PropDef { Tint = new Color(0.42f, 0.30f, 0.18f), Size = new Vector2(0.5f, 0.55f), Blocks = true, ColSize = new Vector2(0.45f, 0.4f) },
            ["Banner"]      = new PropDef { Tint = new Color(0.80f, 0.62f, 0.15f), Size = new Vector2(0.5f, 1.6f) },
            ["Doorway"]     = new PropDef { Tint = new Color(0.15f, 0.12f, 0.10f), Size = new Vector2(0.9f, 1.4f) },
            ["Stairs"]      = new PropDef { Tint = new Color(0.36f, 0.28f, 0.18f), Size = new Vector2(1.2f, 1.6f), Blocks = true, ColSize = new Vector2(1.2f, 0.55f), ColOffset = new Vector2(0f, 0.5f) },
            ["GuildWall"]   = new PropDef { Tint = new Color(0.52f, 0.55f, 0.62f), Size = new Vector2(3.4f, 1.7f), Blocks = true, ColSize = new Vector2(3.4f, 1.7f) },
            ["GuildRoof"]   = new PropDef { Tint = new Color(0.50f, 0.20f, 0.18f), Size = new Vector2(3.8f, 1.2f) },
            ["Well"]        = new PropDef { Tint = new Color(0.42f, 0.40f, 0.44f), Size = new Vector2(1.2f, 0.9f), Blocks = true, ColSize = new Vector2(1.1f, 0.7f), ColOffset = new Vector2(0f, -0.1f) },

            ["GroundGrass"] = new PropDef { Tint = new Color(0.17f, 0.31f, 0.17f), Size = new Vector2(17f, 12f), Ground = true },
            ["GroundWood"]  = new PropDef { Tint = new Color(0.36f, 0.28f, 0.19f), Size = new Vector2(17f, 12f), Ground = true },
            ["GroundDirt"]  = new PropDef { Tint = new Color(0.30f, 0.26f, 0.19f), Size = new Vector2(17f, 12f), Ground = true },
            ["GroundStone"] = new PropDef { Tint = new Color(0.29f, 0.27f, 0.25f), Size = new Vector2(17f, 12f), Ground = true },
            ["GroundSlag"]  = new PropDef { Tint = new Color(0.15f, 0.14f, 0.16f), Size = new Vector2(17f, 12f), Ground = true },
            ["Tree"]        = new PropDef { Tint = new Color(0.12f, 0.32f, 0.15f), Size = new Vector2(1.8f, 2.2f), Blocks = true, ColSize = new Vector2(0.7f, 0.5f), ColOffset = new Vector2(0f, -0.75f) },
            ["Pine"]        = new PropDef { Tint = new Color(0.08f, 0.22f, 0.13f), Size = new Vector2(1.5f, 2.4f), Blocks = true, ColSize = new Vector2(0.6f, 0.5f), ColOffset = new Vector2(0f, -0.85f) },
            ["DeadTree"]    = new PropDef { Tint = new Color(0.28f, 0.22f, 0.24f), Size = new Vector2(1.6f, 2.0f), Blocks = true, ColSize = new Vector2(0.6f, 0.5f), ColOffset = new Vector2(0f, -0.7f) },
            ["Bush"]        = new PropDef { Tint = new Color(0.15f, 0.38f, 0.19f), Size = new Vector2(1.0f, 0.7f) },
            ["Flower"]      = new PropDef { Tint = new Color(0.85f, 0.50f, 0.65f), Size = new Vector2(0.3f, 0.3f) },
            ["Grass"]       = new PropDef { Tint = new Color(0.20f, 0.44f, 0.20f), Size = new Vector2(0.45f, 0.28f) },
            ["Rock"]        = new PropDef { Tint = new Color(0.40f, 0.40f, 0.44f), Size = new Vector2(0.9f, 0.7f), Blocks = true, ColSize = new Vector2(0.85f, 0.55f) },
            ["SlagRock"]    = new PropDef { Tint = new Color(0.19f, 0.17f, 0.20f), Size = new Vector2(1.0f, 0.8f), Blocks = true, ColSize = new Vector2(0.95f, 0.6f) },
            ["Fence"]       = new PropDef { Tint = new Color(0.44f, 0.31f, 0.17f), Size = new Vector2(1.0f, 0.35f), Blocks = true, ColSize = new Vector2(1f, 0.35f) },
            ["PathDirt"]    = new PropDef { Tint = new Color(0.52f, 0.42f, 0.29f), Size = new Vector2(1.0f, 1.0f) },
            ["Carpet"]      = new PropDef { Tint = new Color(0.42f, 0.13f, 0.15f), Size = new Vector2(1.3f, 1.0f) },
            ["HouseWall"]   = new PropDef { Tint = new Color(0.68f, 0.58f, 0.46f), Size = new Vector2(3.0f, 1.7f), Blocks = true, ColSize = new Vector2(3f, 1.7f) },
            ["HouseRoof"]   = new PropDef { Tint = new Color(0.50f, 0.23f, 0.18f), Size = new Vector2(3.4f, 1.1f) },
            ["Stall"]       = new PropDef { Tint = new Color(0.55f, 0.42f, 0.28f), Size = new Vector2(1.6f, 1.1f), Blocks = true, ColSize = new Vector2(1.6f, 1.0f) },
            ["Crate"]       = new PropDef { Tint = new Color(0.48f, 0.35f, 0.19f), Size = new Vector2(0.7f, 0.7f), Blocks = true, ColSize = new Vector2(0.65f, 0.65f) },
            ["Barrel"]      = new PropDef { Tint = new Color(0.42f, 0.30f, 0.18f), Size = new Vector2(0.6f, 0.7f), Blocks = true, ColSize = new Vector2(0.55f, 0.6f) },
            ["Lamp"]        = new PropDef { Tint = new Color(0.85f, 0.72f, 0.35f), Size = new Vector2(0.3f, 1.5f), Blocks = true, ColSize = new Vector2(0.3f, 0.3f), ColOffset = new Vector2(0f, -0.6f) },
            ["Pew"]         = new PropDef { Tint = new Color(0.36f, 0.25f, 0.16f), Size = new Vector2(1.7f, 0.5f), Blocks = true, ColSize = new Vector2(1.7f, 0.45f) },
            ["Pillar"]      = new PropDef { Tint = new Color(0.55f, 0.52f, 0.48f), Size = new Vector2(0.8f, 2.2f), Blocks = true, ColSize = new Vector2(0.75f, 0.6f), ColOffset = new Vector2(0f, -0.75f) },
            ["Wall"]        = new PropDef { Tint = new Color(0.32f, 0.30f, 0.28f), Size = new Vector2(1.0f, 1.0f), Blocks = true, ColSize = new Vector2(1f, 1f) },
            ["Candle"]      = new PropDef { Tint = new Color(0.90f, 0.78f, 0.45f), Size = new Vector2(0.22f, 0.45f) },
            ["Beam"]        = new PropDef { Tint = new Color(0.33f, 0.24f, 0.15f), Size = new Vector2(0.4f, 2.0f), Blocks = true, ColSize = new Vector2(0.38f, 0.5f), ColOffset = new Vector2(0f, -0.7f) },
            ["Cart"]        = new PropDef { Tint = new Color(0.28f, 0.28f, 0.30f), Size = new Vector2(1.0f, 0.75f), Blocks = true, ColSize = new Vector2(0.95f, 0.65f) },
            ["Tent"]        = new PropDef { Tint = new Color(0.42f, 0.36f, 0.28f), Size = new Vector2(1.7f, 1.4f), Blocks = true, ColSize = new Vector2(1.6f, 1.2f) },
            ["Campfire"]    = new PropDef { Tint = new Color(0.85f, 0.45f, 0.15f), Size = new Vector2(0.7f, 0.4f), Blocks = true, ColSize = new Vector2(0.5f, 0.3f), ColOffset = new Vector2(0f, -0.05f) },
            ["Pond"]        = new PropDef { Tint = new Color(0.18f, 0.34f, 0.50f), Size = new Vector2(2.2f, 1.2f), Blocks = true, ColSize = new Vector2(2.1f, 1.05f) },
            ["Ruin"]        = new PropDef { Tint = new Color(0.45f, 0.44f, 0.40f), Size = new Vector2(0.9f, 1.6f), Blocks = true, ColSize = new Vector2(0.85f, 0.7f), ColOffset = new Vector2(0f, -0.4f) },
        };

        internal struct P
        {
            public string Type;
            public float X, Y;
            public P(string type, float x, float y) { Type = type; X = x; Y = y; }
        }

        [MenuItem("Morsoul/Hub/Dress Environment (Overlays + Prologue Wilderness)")]
        public static void DressAll()
        {
            DressScene($"{ScenesFolder}/Hub_LowerMarket_Art.unity", LowerMarket());
            DressScene($"{ScenesFolder}/Hub_Wilderness_Art.unity", Wilderness());
            DressScene($"{ScenesFolder}/Hub_CathedralNave_Art.unity", CathedralNave());
            DressScene($"{ScenesFolder}/Hub_IronHollow_Art.unity", IronHollow());
            DressScene($"{ScenesFolder}/Prologue_Wilderness.unity", PrologueWilderness());
            DressScene($"{ScenesFolder}/Hub_Guild.unity", GuildInterior());
            AssetDatabase.SaveAssets();
            Debug.Log("HubDressingBuilder: environment dressing rebuilt in 4 overlays + Prologue_Wilderness + Hub_Guild.");
        }

        // --- Layouts --------------------------------------------------------

        static List<P> LowerMarket()
        {
            var p = new List<P> { new P("GroundGrass", 0f, 0.5f) };
            // North skyline: two houses plus the Guild hall (larger, signed,
            // banner-flanked doorway at its south face).
            foreach (float x in new[] { -5f, -1f }) p.Add(new P("HouseWall", x, 4.4f));
            foreach (float x in new[] { -5f, -1f }) p.Add(new P("HouseRoof", x, 5.55f));
            p.Add(new P("GuildWall", 3f, 4.4f)); p.Add(new P("GuildRoof", 3f, 5.6f));
            p.Add(new P("Doorway", 3f, 3.62f));
            p.Add(new P("Banner", 1.6f, 4.1f)); p.Add(new P("Banner", 4.4f, 4.1f));
            p.Add(new P("Tree", -7.3f, 4.6f)); p.Add(new P("Pine", 6.3f, 4.7f));
            p.Add(new P("Tree", -3f, 5.2f)); p.Add(new P("Tree", 1f, 5.2f));
            // Plaza well between the stalls — a landmark players steer by.
            p.Add(new P("Well", 0f, -2.4f));
            // South fence line; east/west framed by trees.
            for (float x = -7f; x <= 7f; x += 1f) p.Add(new P("Fence", x, -3.9f));
            foreach (float y in new[] { -2.5f, -0.5f, 1.5f, 3.4f })
            {
                p.Add(new P("Tree", -7.4f, y));
                p.Add(new P("Pine", 7.4f, y));
            }
            // Market stalls flanking the plaza; crates and barrels nearby.
            p.Add(new P("Stall", -4.5f, -2.9f)); p.Add(new P("Stall", 4.5f, -2.9f));
            p.Add(new P("Crate", -3.6f, -3.3f)); p.Add(new P("Crate", 5.4f, -3.3f));
            p.Add(new P("Barrel", 3.6f, -3.4f)); p.Add(new P("Barrel", -5.5f, -3.3f));
            // Dirt paths: an east-west walk and a north-south walk crossing it.
            for (float x = -6f; x <= 6f; x += 1f) p.Add(new P("PathDirt", x, -1f));
            for (float y = -3.4f; y <= 3.4f; y += 1f) p.Add(new P("PathDirt", 0f, y));
            // Corner lamps and scatter decor.
            foreach (float x in new[] { -6.6f, 6.6f })
                foreach (float y in new[] { -3.2f, 3.4f })
                    p.Add(new P("Lamp", x, y));
            foreach (var (x, y) in new[] { (-6.2f, 3f), (-5.8f, -3.4f), (2f, 3.6f), (5.8f, 3.2f), (6.3f, 0.5f), (-2f, -3.5f) })
                p.Add(new P("Flower", x, y));
            foreach (var (x, y) in new[] { (-3f, 3.6f), (1.5f, -3.4f), (4f, 2.8f), (-6.5f, -1.5f), (0.8f, 3.4f) })
                p.Add(new P("Grass", x, y));
            return p;
        }

        static List<P> Wilderness()
        {
            var p = new List<P> { new P("GroundGrass", 0f, 0.5f) };
            // Dense double treeline north and south, alternating broadleaf/pine.
            for (float x = -7.5f; x <= 7.5f; x += 1.5f)
            {
                p.Add(new P((Mathf.RoundToInt(x * 2f) & 2) == 0 ? "Tree" : "Pine", x, 4.4f));
                p.Add(new P((Mathf.RoundToInt(x * 2f) & 2) == 0 ? "Pine" : "Tree", x, -4.0f));
            }
            for (float x = -6.8f; x <= 6.8f; x += 2.2f)
            {
                p.Add(new P("Pine", x, 5.3f));
                p.Add(new P("Tree", x, -4.8f));
            }
            // East/west closures.
            foreach (float y in new[] { -2.5f, -0.5f, 1.5f, 3.2f })
            {
                p.Add(new P("Tree", -7.6f, y));
                p.Add(new P("Pine", 7.6f, y));
            }
            // Interior accents and the ranger outpost camp by ForestOutpost.
            p.Add(new P("DeadTree", -5.6f, 3f)); p.Add(new P("DeadTree", 5.9f, 2.6f));
            p.Add(new P("Tree", -4.4f, 3.4f)); p.Add(new P("Pine", 3.1f, 3.5f));
            p.Add(new P("Tent", -5.6f, -2.7f)); p.Add(new P("Campfire", -4f, -3.1f));
            p.Add(new P("Crate", -3.5f, -2.8f)); p.Add(new P("Barrel", -5.9f, -1.4f));
            p.Add(new P("Pond", 2.4f, -3.4f));
            // Trail between TrailExit (west) and BriarGate (east).
            for (float x = -6f; x <= 6f; x += 1f) p.Add(new P("PathDirt", x, 0.2f));
            // Scatter.
            foreach (var (x, y) in new[] { (-1f, 3.5f), (1.8f, 2.8f), (-5.8f, 0.6f), (4.8f, 0.2f) })
                p.Add(new P("Bush", x, y));
            p.Add(new P("Rock", 0.5f, 3.4f)); p.Add(new P("Rock", -0.8f, -2.9f));
            foreach (var (x, y) in new[] { (-3.5f, 0.4f), (2f, 0.8f), (0f, 1.8f), (5.8f, -1.8f) })
                p.Add(new P("Flower", x, y));
            foreach (var (x, y) in new[] { (-4f, 0.8f), (-1.5f, -1.2f), (3f, 2.6f), (1f, -0.6f), (-2.5f, -3f), (-6f, 2.2f), (6.5f, -3f), (4f, -2.2f) })
                p.Add(new P("Grass", x, y));
            return p;
        }

        static List<P> CathedralNave()
        {
            var p = new List<P> { new P("GroundStone", 0f, 0.5f) };
            // Stone perimeter walls; door/stair interactables stand in front.
            for (float x = -7.5f; x <= 7.5f; x += 1f)
            {
                p.Add(new P("Wall", x, 5f));
                p.Add(new P("Wall", x, -4f));
            }
            for (float y = -3f; y <= 4f; y += 1f)
            {
                p.Add(new P("Wall", -7.5f, y));
                p.Add(new P("Wall", 7.5f, y));
            }
            // Four columns framing the nave.
            foreach (float x in new[] { -3.8f, 3.8f })
                foreach (float y in new[] { 2.3f, -2.6f })
                    p.Add(new P("Pillar", x, y));
            // Pews in two columns leaving a carpeted center aisle.
            foreach (float x in new[] { -1.9f, 1.9f })
                foreach (float y in new[] { -0.7f, 0.2f, 1.1f, 2f })
                    p.Add(new P("Pew", x, y));
            for (float y = -3.4f; y <= 2.6f; y += 1f) p.Add(new P("Carpet", 0f, y));
            // Altar candles and a side candle near Sylvan.
            p.Add(new P("Candle", 0.2f, 3.4f)); p.Add(new P("Candle", 1.8f, 3.4f));
            p.Add(new P("Candle", -4.9f, 3.4f));
            return p;
        }

        static List<P> IronHollow()
        {
            var p = new List<P> { new P("GroundSlag", 0f, 0.5f) };
            // Slag-rock perimeter instead of vegetation.
            for (float x = -7f; x <= 7f; x += 1.3f)
            {
                p.Add(new P("SlagRock", x, 4.6f));
                p.Add(new P("SlagRock", x, -3.9f));
            }
            for (float y = -2.8f; y <= 3.6f; y += 1.2f)
            {
                p.Add(new P("SlagRock", -7.4f, y));
                p.Add(new P("SlagRock", 7.4f, y));
            }
            // Timber tunnel ribs down both flanks.
            foreach (float y in new[] { -2.6f, -0.6f, 1.4f, 3.4f })
            {
                p.Add(new P("Beam", -5.6f, y));
                p.Add(new P("Beam", 7f, y));
            }
            // Mining clutter: carts, crates, barrels, interior slag.
            p.Add(new P("Cart", -4.8f, -3.3f)); p.Add(new P("Cart", 5f, 3.6f));
            p.Add(new P("Crate", -4f, -3.2f)); p.Add(new P("Crate", -2.2f, -3.4f));
            p.Add(new P("Barrel", -5f, -0.2f)); p.Add(new P("Barrel", 6.6f, -3.1f));
            foreach (var (x, y) in new[] { (0.8f, -3.4f), (-6.6f, 2.9f), (5.8f, -3.2f), (-1f, 3.7f) })
                p.Add(new P("SlagRock", x, y));
            // Wall lamps.
            foreach (float x in new[] { -6.8f, 6.8f })
                foreach (float y in new[] { 4f, -3.1f })
                    p.Add(new P("Lamp", x, y));
            return p;
        }

        static List<P> PrologueWilderness()
        {
            // Main scene already has treeline colliders, so decor here carries
            // none — scatter sits inside the blocked bands, not the path.
            var p = new List<P> { new P("GroundGrass", 7f, -0.3f) };
            for (float x = -9f; x <= 23f; x += 1.6f)
            {
                p.Add(new P((Mathf.RoundToInt(x * 10f) & 2) == 0 ? "Tree" : "Pine", x, 2.7f));
                p.Add(new P((Mathf.RoundToInt(x * 10f) & 2) == 0 ? "Pine" : "Tree", x, -3.5f));
            }
            // Ruins entrance framing at the west end.
            p.Add(new P("Ruin", -9.3f, 0.9f)); p.Add(new P("Ruin", -9.3f, -2.2f));
            // Scatter decor along the walkable strip edges.
            foreach (var (x, y) in new[] { (-6f, 1.3f), (2f, 1.4f), (9f, 1.2f), (16f, 1.3f), (-2f, -2.2f), (6f, -2.3f), (18f, -2.1f) })
                p.Add(new P("Bush", x, y));
            foreach (var (x, y) in new[] { (-4f, 1.5f), (11f, -2.3f), (21f, 1.4f) })
                p.Add(new P("Rock", x, y));
            foreach (var (x, y) in new[] { (-5f, -1.8f), (3f, -2.4f), (14f, 1.5f), (20f, -1.9f) })
                p.Add(new P("Flower", x, y));
            foreach (var (x, y) in new[] { (-7f, 1.1f), (0f, 1.5f), (5f, -2.4f), (10f, 1.4f), (15f, -2.3f), (22f, -2.2f) })
                p.Add(new P("Grass", x, y));
            p.Add(new P("DeadTree", 4f, 1.6f)); p.Add(new P("DeadTree", 19f, 1.7f));
            return p;
        }

        // --- Generation -----------------------------------------------------

        static void DressScene(string scenePath, List<P> layout)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"HubDressingBuilder: '{scenePath}' missing; skipped.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                // Regenerate only our own root; anything else is preserved.
                var old = FindRoot(scene, RootName);
                if (old != null) Object.DestroyImmediate(old);
                var root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);

                int n = 0;
                foreach (var placement in layout)
                {
                    PlaceProp(root.transform, placement, n);
                    n++;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Props sort just ahead of actors at the same Y (small negative
        /// bias), while actors y-sort dynamically via <see cref="YSort2D"/>.
        /// One tile of Y always outweighs the bias, so props only ever
        /// overhang an actor standing at their base — never an actor a full
        /// step south of them.
        /// </summary>
        const float PropDepthBias = -0.005f;

        static void PlaceProp(Transform parent, P placement, int index)
        {
            var def = Types[placement.Type];
            float z = def.Ground
                ? 0.45f
                : placement.Y * YSort2D.DepthPerUnitY + PropDepthBias + index * 0.00001f;
            var go = HubSceneFactory.NewQuadObject(
                $"Env_{placement.Type}_{index:D2}", def.Tint,
                new Vector3(placement.X, placement.Y, z));
            go.transform.localScale = new Vector3(def.Size.x, def.Size.y, 1f);
            go.transform.SetParent(parent, true);

            if (def.Blocks)
            {
                var collider = go.AddComponent<BoxCollider2D>();
                // BoxCollider2D size/offset are local-space and multiply by
                // the prop's transform scale — PropFootprint divides back out
                // so the authored world-space footprint is what lands.
                var scale = new Vector2(def.Size.x, def.Size.y);
                collider.size = PropFootprint.LocalSize(def.ColSize, scale);
                collider.offset = PropFootprint.LocalOffset(def.ColOffset, scale);
            }

            var visual = go.AddComponent<EntityVisual>();
            visual.profile = EnsurePropProfile(placement.Type);
            visual.Apply(visual.profile);
        }

        /// <summary>
        /// Places a layout under an existing parent in an already-open scene —
        /// used by the Guild interior builder, whose Environment root lives in
        /// the main scene (interiors have no art overlay).
        /// </summary>
        internal static void PlaceProps(Transform parent, List<P> layout)
        {
            for (int i = 0; i < layout.Count; i++)
                PlaceProp(parent, layout[i], i);
        }

        internal static List<P> GuildInterior()
        {
            // Two floors share one scene: ground hall y[-4.5,5.5], Ovan's
            // office upstairs offset to y[19.5,29.5]. The stair pair teleports
            // between them (StairInteractable), so the layout stays contiguous.
            var p = new List<P>
            {
                new P("GroundWood", 0f, 0.5f),
                new P("GroundWood", 0f, 24.5f),
            };
            // Perimeter walls, both floors — south wall leaves a doorway gap
            // at x=0 on the ground floor.
            for (float x = -7.5f; x <= 7.5f; x += 1f)
            {
                if (Mathf.Abs(x) > 1f) p.Add(new P("WallWood", x, -4.5f));
                p.Add(new P("WallWood", x, 5.5f));
                p.Add(new P("WallWood", x, 19.5f));
                p.Add(new P("WallWood", x, 29.5f));
            }
            for (float y = -3.5f; y <= 4.5f; y += 1f)
            {
                p.Add(new P("WallWood", -7.5f, y));
                p.Add(new P("WallWood", 7.5f, y));
            }
            for (float y = 20.5f; y <= 28.5f; y += 1f)
            {
                p.Add(new P("WallWood", -7.5f, y));
                p.Add(new P("WallWood", 7.5f, y));
            }
            // Ground floor: front counter with the clerk behind it, notice
            // board beside it, tables/chairs, shelf row on the west wall,
            // carpet approach, guild banner, stairs at the east side.
            p.Add(new P("Counter", 0f, 1.8f));
            p.Add(new P("Bookshelf", -6.6f, 4.6f)); p.Add(new P("Bookshelf", -5.4f, 4.6f));
            p.Add(new P("Bookshelf", -3.6f, 4.6f));
            p.Add(new P("Table", -3.5f, -1.8f)); p.Add(new P("Chair", -3.5f, -0.9f));
            p.Add(new P("Chair", -4.3f, -1.8f)); p.Add(new P("Chair", -2.7f, -1.8f));
            p.Add(new P("Table", 3.5f, -1.2f)); p.Add(new P("Chair", 3.5f, -0.3f));
            p.Add(new P("Chair", 2.8f, -1.9f));
            p.Add(new P("Carpet", 0f, -1.2f)); p.Add(new P("Carpet", 0f, -2.2f));
            p.Add(new P("Carpet", 0f, -0.2f));
            p.Add(new P("Banner", -1.8f, 4.9f)); p.Add(new P("Banner", 1.8f, 4.9f));
            p.Add(new P("Candle", -4f, 0.5f)); p.Add(new P("Candle", 4f, 0.5f));
            p.Add(new P("Stairs", 6.5f, 3.6f));
            p.Add(new P("Barrel", -6.5f, -3.6f)); p.Add(new P("Barrel", -5.8f, -3.9f));
            p.Add(new P("Crate", 6.5f, -3.7f));
            // Upstairs office: desk, visitor chairs, library wall, window
            // light rug, stair landing at the east.
            p.Add(new P("Counter", 0f, 24.8f));
            p.Add(new P("Chair", -0.7f, 23.4f)); p.Add(new P("Chair", 0.7f, 23.4f));
            p.Add(new P("Bookshelf", -5.8f, 28.6f)); p.Add(new P("Bookshelf", -4.6f, 28.6f));
            p.Add(new P("Bookshelf", 4.6f, 28.6f)); p.Add(new P("Bookshelf", 5.8f, 28.6f));
            p.Add(new P("Carpet", 0f, 23f)); p.Add(new P("Carpet", 0f, 22f));
            p.Add(new P("Banner", -2f, 28.9f)); p.Add(new P("Banner", 2f, 28.9f));
            p.Add(new P("Candle", -3f, 26.5f)); p.Add(new P("Candle", 3f, 26.5f));
            p.Add(new P("Stairs", 6.5f, 21f));
            return p;
        }

        /// <summary>One shared profile per prop type, named Prop_&lt;Type&gt;.</summary>
        static VisualProfile EnsurePropProfile(string type)
        {
            HubSceneFactory.EnsureFolder(ProfilesFolder);
            string path = $"{ProfilesFolder}/Prop_{type}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VisualProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            return profile;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
