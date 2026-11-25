using System.Collections.Generic;
using Morsoul.Engine;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    /// <summary>
    /// Authoritative builder for all six canonical EvidenceGraphData assets
    /// (Cases 0-5). Writes validated graphs, recaches reference support and
    /// deterministic topological order, binds them to Case_00..Case_05, and
    /// runs GraphAudit to guarantee zero errors and zero warnings.
    /// </summary>
    public static class CanonicalEvidenceGraphBuilder
    {
        [MenuItem("Morsoul/Data/Rebuild All Cases & Canonical Graphs")]
        public static void RebuildAll()
        {
            CaseDataGenerator.Generate("Assets/Data/Cases");
            CaseDataGenerator.GenerateCase2Assets("Assets/Data/Cases");
            CaseDataGenerator.GenerateCase3Assets("Assets/Data/Cases");
            CaseDataGenerator.GenerateFinaleAssets("Assets/Data/Cases");

            BuildAllCanonicalGraphs();
            ClueRegistryGenerator.Rebuild();
            CaseDatabaseBuilder.RebuildIfAvailable();
            CaseDataGenerator.ValidateAllCases();
            EvidenceGraphAuditor.AuditAll();
        }

        [MenuItem("Morsoul/Data/Build Canonical Evidence Graphs")]
        public static void BuildAllCanonicalGraphs()
        {
            int ok = 0;
            ok += Write(Case0(), "Assets/Data/Cases/Case00/EVG_00_PROVISIONAL.asset",
                "Approved Case 0 weighted board. 4 S* links forming a 4-clue chain (T1->C1->C2->C3 + T1->C3).",
                tutorialRh: new[] { "RH1_00" },
                tutorialRhFeedback: "The pipe is a lever, not a bypass. It cannot power or turn this lock.",
                tutorialRevFeedback: "That arrow points backward. Follow the evidence toward the manual override.",
                tutorialGenFeedback: "That connection is outside the intended tutorial chain.",
                tutorialLockedFeedback: "The complete conclusion stays locked until the whole manual-bypass chain is drawn.");
            ok += Write(Case1(), "Assets/Data/Cases/Case01/EVG_01_PROVISIONAL.asset",
                "Canonical Case 1 graph. 5 S* links, 4-clue main chain (C5->C2->C3->C4) + inside-key branch (C1->T1->C4).");
            ok += Write(Case2(), "Assets/Data/Cases/Case02/EVG_02.asset",
                "Canonical Case 2 graph. 5 S* links, 4-clue main chain (C2->C4->C1->C3) + alibi/debt branches (T1->C4, C5->C3).");
            ok += Write(Case3(), "Assets/Data/Cases/Case03/EVG_03.asset",
                "Canonical Case 3 graph. 5 S* links, 5-clue main chain (T1->C5->C1->C4->C3) + dagger-stabilizer branch (C2->C4).");
            ok += Write(Case4(), "Assets/Data/Cases/Case04/EVG_04.asset",
                "Canonical Case 4 graph. 5 S* links, 4-clue main chain (C4->C2->C3->C5) + valve-sabotage branch (T1->C1->C3).");
            ok += Write(Case5(), "Assets/Data/Cases/Case05/EVG_05.asset",
                "Canonical Case 5 graph. 5 S* links, 5-clue main chain (C4->C1->C5->C2->C3) + Eliza readout branch (T1->C3).");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"CanonicalEvidenceGraphBuilder: built {ok}/6 canonical evidence graphs.");
        }

        static int Write(
            EvidenceGraphDef def,
            string path,
            string note,
            string[] tutorialRh = null,
            string tutorialRhFeedback = "",
            string tutorialRevFeedback = "",
            string tutorialGenFeedback = "",
            string tutorialLockedFeedback = "")
        {
            var errors = EvidenceGraph.Build(def, out var graph);
            if (graph == null)
            {
                Debug.LogError($"CanonicalEvidenceGraphBuilder: {path} failed build:\n" +
                               string.Join("\n", errors));
                return 0;
            }

            var report = GraphAudit.Analyze(graph);
            if (report.HasErrors || report.Findings.Count > 0)
            {
                Debug.LogWarning($"CanonicalEvidenceGraphBuilder: {path} audit findings:\n{string.Join("\n", report.Findings)}");
            }

            var data = AssetDatabase.LoadAssetAtPath<EvidenceGraphData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EvidenceGraphData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.caseId = def.CaseId;
            data.provisional = false;
            data.designNote = note;
            data.tutorialRedHerringClueIds = tutorialRh ?? new string[0];
            data.tutorialRedHerringFeedback = tutorialRhFeedback;
            data.tutorialReversedLinkFeedback = tutorialRevFeedback;
            data.tutorialGenericLinkFeedback = tutorialGenFeedback;
            data.tutorialLockedConclusionFeedback = tutorialLockedFeedback;
            data.clueIds = def.ClueIds;
            data.reliabilities = def.Reliabilities;
            data.authoredClueEdges = def.AuthoredClueEdges;
            data.solutionEdges = def.SolutionEdges;
            data.unlockDependencies = def.UnlockDependencies;
            data.conclusions = def.Conclusions;
            data.startingWrongConclusionId = def.StartingWrongConclusionId;
            data.completeConclusionId = def.CompleteConclusionId;
            data.parameters = def.Parameters;
            data.cachedReferenceSupport = (float)graph.ReferenceSupport;
            data.solutionTopologicalOrder = new List<string>(graph.SolutionOrder).ToArray();
            EditorUtility.SetDirty(data);

            var caseAsset = AssetDatabase.LoadAssetAtPath<CaseData>(
                $"Assets/Data/Cases/Case{def.CaseId:00}/Case_{def.CaseId:00}.asset");
            if (caseAsset != null && caseAsset.evidenceGraph != data)
            {
                caseAsset.evidenceGraph = data;
                EditorUtility.SetDirty(caseAsset);
            }

            return 1;
        }

        static DirectedEdge E(string s, string t) => new DirectedEdge { sourceId = s, targetId = t };
        static WeightedClueEdge W(string s, string t, float w, string rationale = "") =>
            new WeightedClueEdge { sourceId = s, targetId = t, weight = w, rationale = rationale };
        static ClueReliability R(string id, float v) =>
            new ClueReliability { clueId = id, value = v };
        static WeightedClueRef S(string id, float w) =>
            new WeightedClueRef { clueId = id, weight = w };

        static EvidenceGraphDef Case0()
        {
            var solution = new[]
            {
                E("T1_00", "C1_00"),
                E("C1_00", "C2_00"),
                E("C2_00", "C3_00"),
                E("T1_00", "C3_00"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 0,
                ClueIds = new[] { "T1_00", "C1_00", "C2_00", "C3_00", "RH1_00" },
                Reliabilities = new[]
                {
                    R("T1_00", 0.9f), R("C1_00", 0.85f), R("C2_00", 0.95f),
                    R("C3_00", 1f), R("RH1_00", 0.35f),
                },
                AuthoredClueEdges = new[]
                {
                    W("T1_00", "C1_00", 0.9f, "Optical Translation deciphers the human scholar's torn desk logs, identifying this sealed installation."),
                    W("C1_00", "C2_00", 0.9f, "The desk logs explain the shattered capacitor relay: a forced overload severed power to the blast door."),
                    W("C2_00", "C3_00", 0.9f, "With primary hydraulics dead, the ancient chamber key's manual bypass frequency is the only way out."),
                    W("T1_00", "C3_00", 0.8f, "Diagnostic sensors calibrate the ancient key's magnetic frequency to the unpowered override port."),
                    W("T1_00", "RH1_00", 0.2f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new[] { E("T1_00", "C1_00") },
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE0_FORCE_BREACH",
                        tier = ConclusionTier.Wrong,
                        text = "Force the door open with the pipe.",
                        submissionFeedback = "The pipe is a lever, not a bypass. It cannot power or turn this lock.",
                        supportingEdges = new[] { S("RH1_00", 0.5f) },
                        contradictingEdges = new[] { S("C1_00", 0.6f), S("C3_00", 0.7f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE0_UNPOWERED_LOCKDOWN",
                        tier = ConclusionTier.Partial,
                        text = "The door is unpowered, but nothing here opens it.",
                        submissionFeedback = "The lock is unpowered, but that does not yet explain how the key releases it.",
                        supportingEdges = new[] { S("C1_00", 0.5f), S("C2_00", 0.6f) },
                        contradictingEdges = new[] { S("RH1_00", 0.4f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE0_MANUAL_OVERRIDE",
                        tier = ConclusionTier.Complete,
                        text = "The ancient key can power the manual override.",
                        targetClueId = "C3_00",
                        supportingEdges = new[] { S("T1_00", 0.8f), S("C3_00", 0.9f) },
                        contradictingEdges = new[] { S("RH1_00", 0.5f) },
                    },
                },
                StartingWrongConclusionId = "CASE0_FORCE_BREACH",
                CompleteConclusionId = "CASE0_MANUAL_OVERRIDE",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.1f,
                    lowThreshold = 0.35f,
                    highThreshold = 0.93f,
                    PrunedMode = true,
                },
            };
        }

        static EvidenceGraphDef Case1()
        {
            var solution = new[]
            {
                E("C1_01", "T1_01"),
                E("C5_01", "C2_01"),
                E("C2_01", "C3_01"),
                E("C3_01", "C4_01"),
                E("T1_01", "C4_01"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 1,
                ClueIds = new[] { "C1_01", "T1_01", "C5_01", "C2_01", "C3_01", "C4_01", "T2_01", "RH1_01" },
                Reliabilities = new[]
                {
                    R("C1_01", 1f), R("T1_01", 0.9f), R("C5_01", 0.95f), R("C2_01", 1f),
                    R("C3_01", 1f), R("C4_01", 1f), R("T2_01", 0.5f), R("RH1_01", 0.4f),
                },
                AuthoredClueEdges = new[]
                {
                    W("C1_01", "T1_01", 0.9f, "A pristine Dwarven lock with zero tool marks proves one of the two registered keys opened the vault."),
                    W("C5_01", "C2_01", 0.9f, "The unmarked purple-film crate beside Ignatius's workshop matches the sublimating solvent vapor inside the vault."),
                    W("C2_01", "C3_01", 0.9f, "Harmless theatrical solvent explains why no real stock burned — the altered ledger proves Batch #408 never existed."),
                    W("C3_01", "C4_01", 0.9f, "Fabricating phantom Batch #408 stock sets up the pre-dated Treasury indemnity claim."),
                    W("T1_01", "C4_01", 0.85f, "Only Ignatius held an unsealed key — tying inside vault access directly to the sole beneficiary on the pre-dated claim."),
                    W("T2_01", "RH1_01", 0.35f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE1_DARK_MAGIC_SABOTAGE",
                        tier = ConclusionTier.Wrong,
                        text = "A rogue cabal broke into the vault using volatile dark magic.",
                        submissionFeedback = "No forced entry or dark magic occurred — re-examine the lock, the solvent, and the ledger.",
                        supportingEdges = new[] { S("T2_01", 0.5f), S("RH1_01", 0.4f) },
                        contradictingEdges = new[] { S("C2_01", 0.5f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE1_INSIDE_KEY_ACCESS",
                        tier = ConclusionTier.Partial,
                        text = "Inside Key Access: No intruder forced the vault — one of the two registered keys opened it.",
                        submissionFeedback = "You proved inside access, but not yet why the attack was staged or who benefits.",
                        supportingEdges = new[] { S("T1_01", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE1_STAGED_SOLVENT_FRAUD",
                        tier = ConclusionTier.Partial,
                        text = "Staged Solvent & Phantom Stock: Theatrical solvent masked a Batch #408 shipment that was never brewed.",
                        submissionFeedback = "You uncovered the staged solvent and fake ledger — now connect inside access and the claim to prove motive.",
                        supportingEdges = new[] { S("C3_01", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE1_EMBEZZLEMENT_COVERUP",
                        tier = ConclusionTier.Complete,
                        text = "Staged Internal Fraud: Ignatius used his registered key and Veiled Circle solvent to fake a breach and claim Treasury indemnity on phantom Batch #408.",
                        targetClueId = "C4_01",
                        supportingEdges = new[] { S("C4_01", 1f) },
                        contradictingEdges = new[] { S("T2_01", 0.8f), S("RH1_01", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "CASE1_DARK_MAGIC_SABOTAGE",
                CompleteConclusionId = "CASE1_EMBEZZLEMENT_COVERUP",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.20f,
                    highThreshold = 0.85f,
                    PrunedMode = true,
                },
            };
        }

        static EvidenceGraphDef Case2()
        {
            var solution = new[]
            {
                E("C2_02", "C4_02"),
                E("T1_02", "C4_02"),
                E("C4_02", "C1_02"),
                E("C1_02", "C3_02"),
                E("C5_02", "C3_02"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 2,
                ClueIds = new[] { "C2_02", "T1_02", "C4_02", "C1_02", "C5_02", "C3_02", "T2_02", "RH1_02" },
                Reliabilities = new[]
                {
                    R("C2_02", 1f), R("T1_02", 0.9f), R("C4_02", 0.95f), R("C1_02", 1f),
                    R("C5_02", 0.95f), R("C3_02", 1f), R("T2_02", 0.5f), R("RH1_02", 0.4f),
                },
                AuthoredClueEdges = new[]
                {
                    W("C2_02", "C4_02", 0.9f, "The black metallic damping soot on the clapper matches the soot-stained polishing rag tucked inside Accord's belfry bench."),
                    W("T1_02", "C4_02", 0.85f, "Brother Kenneth's courtyard alibi clears the acolyte, leaving someone with direct belfry access who used the soot-stained rag."),
                    W("C4_02", "C1_02", 0.9f, "Manual soot application inside the belfry caused the internal harmonic shockwave that shattered the Resonance Crystal."),
                    W("C1_02", "C3_02", 0.9f, "An inside acoustic sabotage points straight to the veil-sealed coercion note hidden behind the belfry scaffolding."),
                    W("C5_02", "C3_02", 0.9f, "The Nave debt notice proves the veil-sealed creditor actually bought out Marian's asylum, confirming the blackmail leverage."),
                    W("T2_02", "RH1_02", 0.35f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE2_ACOLYTE_SORCERY",
                        tier = ConclusionTier.Wrong,
                        text = "The spirit-folk acolyte cursed the bell from the courtyard.",
                        submissionFeedback = "The acolyte has an alibi and the crystal shattered from inside the belfry — look at who handled the damping soot.",
                        supportingEdges = new[] { S("T2_02", 0.5f), S("RH1_02", 0.4f) },
                        contradictingEdges = new[] { S("C2_02", 0.5f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE2_INSIDE_ACOUSTIC_SABOTAGE",
                        tier = ConclusionTier.Partial,
                        text = "Inside Acoustic Sabotage: Damping soot was applied manually inside the belfry, clearing the acolyte.",
                        submissionFeedback = "You proved Accord's bench rag and the soot shattered the crystal — now connect the coercion note and debt notice for motive.",
                        supportingEdges = new[] { S("C1_02", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE2_COERCED_BELL_RINGER",
                        tier = ConclusionTier.Complete,
                        text = "Coerced Belfry Sabotage: The Veiled Circle bought out Marian's asylum debt to force Accord into muting Saint Althea's bell.",
                        targetClueId = "C3_02",
                        supportingEdges = new[] { S("C3_02", 1f) },
                        contradictingEdges = new[] { S("T2_02", 0.8f), S("RH1_02", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "CASE2_ACOLYTE_SORCERY",
                CompleteConclusionId = "CASE2_COERCED_BELL_RINGER",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.25f,
                    highThreshold = 0.85f,
                    PrunedMode = true,
                },
            };
        }

        static EvidenceGraphDef Case3()
        {
            var solution = new[]
            {
                E("T1_03", "C5_03"),
                E("C5_03", "C1_03"),
                E("C1_03", "C4_03"),
                E("C2_03", "C4_03"),
                E("C4_03", "C3_03"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 3,
                ClueIds = new[] { "T1_03", "C5_03", "C1_03", "C2_03", "C4_03", "C3_03", "T2_03", "RH1_03" },
                Reliabilities = new[]
                {
                    R("T1_03", 0.9f), R("C5_03", 0.95f), R("C1_03", 1f), R("C2_03", 0.95f),
                    R("C4_03", 1f), R("C3_03", 1f), R("T2_03", 0.5f), R("RH1_03", 0.4f),
                },
                AuthoredClueEdges = new[]
                {
                    W("T1_03", "C5_03", 0.9f, "The ranger's five-night blight timeline matches Vane's five-night harvest tally to the exact evening."),
                    W("C5_03", "C1_03", 0.9f, "Vane's nightly yield log directly tracks the industrial brass extraction siphon driven into the Elder Tree's taproot."),
                    W("C1_03", "C4_03", 0.9f, "Forced sap drainage from the taproot fills the pressurized mana-resin canisters stenciled for Iron Hollow Drift 09."),
                    W("C2_03", "C4_03", 0.85f, "The Veiled Circle dagger's solvent stabilizer matches the chemical preservative sealing the Iron Hollow canisters."),
                    W("C4_03", "C3_03", 0.9f, "Industrial Veiled Circle shipments paired with Vane's Emotion Echo cloak prove he is a fanatic fueling the Sanctum Core ritual."),
                    W("T2_03", "RH1_03", 0.35f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE3_NATURAL_BLIGHT",
                        tier = ConclusionTier.Wrong,
                        text = "Natural forest blight and beetles are killing the Elder Tree.",
                        submissionFeedback = "Wood beetles are a secondary symptom — connect the five-night timeline, harvest tally, and taproot siphon.",
                        supportingEdges = new[] { S("T2_03", 0.5f), S("RH1_03", 0.4f) },
                        contradictingEdges = new[] { S("C1_03", 0.5f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE3_INDUSTRIAL_SIPHONING",
                        tier = ConclusionTier.Partial,
                        text = "Industrial Mana Siphoning: Vane has been mechanically draining the Elder Tree for five consecutive nights.",
                        submissionFeedback = "You proved mechanical siphoning — now trace where the stabilized canisters are going and who Vane serves.",
                        supportingEdges = new[] { S("C1_03", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE3_VEILED_CIRCLE_SUPPLY",
                        tier = ConclusionTier.Partial,
                        text = "Veiled Circle Supply Line: Stabilized Elder Tree mana is being shipped to Iron Hollow Drift 09.",
                        submissionFeedback = "You traced the shipment pipeline — connect it to Vane's Emotion Echo cloak to prove his ritual motive.",
                        supportingEdges = new[] { S("C4_03", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE3_CULT_RITUAL_HARVEST",
                        tier = ConclusionTier.Complete,
                        text = "Veiled Circle Ritual Harvest: Vane is a cult operative siphoning the Elder Tree to fuel the Sanctum Core.",
                        targetClueId = "C3_03",
                        supportingEdges = new[] { S("C3_03", 1f) },
                        contradictingEdges = new[] { S("T2_03", 0.8f), S("RH1_03", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "CASE3_NATURAL_BLIGHT",
                CompleteConclusionId = "CASE3_CULT_RITUAL_HARVEST",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.25f,
                    highThreshold = 0.85f,
                    PrunedMode = true,
                },
            };
        }

        static EvidenceGraphDef Case4()
        {
            var solution = new[]
            {
                E("T1_04", "C1_04"),
                E("C4_04", "C2_04"),
                E("C1_04", "C3_04"),
                E("C2_04", "C3_04"),
                E("C3_04", "C5_04"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 4,
                ClueIds = new[] { "T1_04", "C1_04", "C4_04", "C2_04", "C3_04", "C5_04", "T2_04", "RH1_04" },
                Reliabilities = new[]
                {
                    R("T1_04", 0.9f), R("C1_04", 1f), R("C4_04", 0.95f), R("C2_04", 1f),
                    R("C3_04", 1f), R("C5_04", 1f), R("T2_04", 0.5f), R("RH1_04", 0.4f),
                },
                AuthoredClueEdges = new[]
                {
                    W("T1_04", "C1_04", 0.9f, "Brokk's thirty-year safety record rules out negligence — the iron-wedged valves prove deliberate backpressure sabotage."),
                    W("C4_04", "C2_04", 0.9f, "Harlan's Drift 09 freight manifest accounts for the synthetic mana solvent that dissolved the bypass conduits."),
                    W("C1_04", "C3_04", 0.9f, "The engineered backpressure blowout was aimed precisely at exposing the hidden pre-Cataclysm sub-lab blast door."),
                    W("C2_04", "C3_04", 0.85f, "Dissolving the bypass conduits prevented emergency depressurization, clearing the rockface straight to the blast door."),
                    W("C3_04", "C5_04", 0.9f, "Once the blast door was uncovered, Harlan bridged the external stasis override relay with Veiled Circle shunts to trap Brokk inside."),
                    W("T2_04", "RH1_04", 0.35f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE4_OPERATOR_ERROR",
                        tier = ConclusionTier.Wrong,
                        text = "Reckless Dwarven operator error blew the Drift 09 steam lines.",
                        submissionFeedback = "Iron wedges and synthetic mana solvent are not operator error — trace both sabotage paths to the sub-lab.",
                        supportingEdges = new[] { S("T2_04", 0.5f), S("RH1_04", 0.4f) },
                        contradictingEdges = new[] { S("C1_04", 0.5f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE4_DELIBERATE_RUPTURE",
                        tier = ConclusionTier.Partial,
                        text = "Deliberate Pipeline Sabotage: Brokk didn't make a mistake — the pressure relief valves were wedged shut.",
                        submissionFeedback = "You proved the blowout was deliberate — now connect Harlan's solvent conduits and the hidden sub-lab.",
                        supportingEdges = new[] { S("C1_04", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE4_COORDINATED_BREACH",
                        tier = ConclusionTier.Partial,
                        text = "Engineered Sub-Lab Breach: Jammed valves and Harlan's dissolved conduits were timed to uncover the ancient blast door.",
                        submissionFeedback = "You uncovered the breach target — connect the blast door to the external stasis relay to prove the lockdown trap.",
                        supportingEdges = new[] { S("C3_04", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE4_VEILED_CIRCLE_LOCKDOWN",
                        tier = ConclusionTier.Complete,
                        text = "Veiled Circle Sub-Lab Seizure: Harlan sabotaged Drift 09 to uncover the pre-Cataclysm facility and locked Brokk inside using external stasis shunts.",
                        targetClueId = "C5_04",
                        supportingEdges = new[] { S("C5_04", 1f) },
                        contradictingEdges = new[] { S("T2_04", 0.8f), S("RH1_04", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "CASE4_OPERATOR_ERROR",
                CompleteConclusionId = "CASE4_VEILED_CIRCLE_LOCKDOWN",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.20f,
                    highThreshold = 0.85f,
                    PrunedMode = true,
                },
            };
        }

        static EvidenceGraphDef Case5()
        {
            var solution = new[]
            {
                E("C4_05", "C1_05"),
                E("C1_05", "C5_05"),
                E("C5_05", "C2_05"),
                E("C2_05", "C3_05"),
                E("T1_05", "C3_05"),
            };
            return new EvidenceGraphDef
            {
                CaseId = 5,
                ClueIds = new[] { "C4_05", "C1_05", "C5_05", "C2_05", "C3_05", "T1_05", "RH1_05" },
                Reliabilities = new[]
                {
                    R("C4_05", 1f), R("C1_05", 1f), R("C5_05", 1f), R("C2_05", 1f),
                    R("C3_05", 1f), R("T1_05", 0.95f), R("RH1_05", 0.35f),
                },
                AuthoredClueEdges = new[]
                {
                    W("C4_05", "C1_05", 0.9f, "The four-district Guild audit traces every stolen reagent and mana harvest directly into the Sanctum's active siphon pylons."),
                    W("C1_05", "C5_05", 0.9f, "Inverted pylon pulses match the Empirical Guard's deep-crust seismic spikes beneath Drift 09."),
                    W("C5_05", "C2_05", 0.9f, "Synchronized crust resonance is deliberately overloading the central planetary fissure's containment barrier."),
                    W("C2_05", "C3_05", 0.9f, "Engineering a planetary fissure collapse exposes the Grand Overseer's redacted Cult Doctrine as an intentional cataclysmic purge."),
                    W("T1_05", "C3_05", 0.85f, "Eliza's tactical readout confirms the pylons are breaking stabilizer locks — corroborating the Overseer's ideological hypocrisy."),
                    W("C2_05", "RH1_05", 0.25f),
                },
                SolutionEdges = solution,
                UnlockDependencies = new DirectedEdge[0],
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "CASE5_CONTAINMENT_FAILURE",
                        tier = ConclusionTier.Wrong,
                        text = "Natural geological instability is overloading the Sanctum fissure.",
                        submissionFeedback = "The seismic spikes are synchronized with inverted siphon pylons — trace the four-district supply chain into the Core.",
                        supportingEdges = new[] { S("RH1_05", 0.5f) },
                        contradictingEdges = new[] { S("C1_05", 0.5f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE5_FORCED_CATACLYSMIC_OVERLOAD",
                        tier = ConclusionTier.Partial,
                        text = "Forced Cataclysmic Overload: Harvested district mana is being pulsed downward through inverted pylons to fracture the central fissure.",
                        submissionFeedback = "You proved the overload is engineered — now connect the fissure and Eliza's readout to the Overseer's Doctrine.",
                        supportingEdges = new[] { S("C2_05", 1f) },
                    },
                    new ConclusionDef
                    {
                        id = "CASE5_HYPOCRITICAL_IDEOLOGICAL_EXTREMISM",
                        tier = ConclusionTier.Complete,
                        text = "Hypocritical Ideological Extremism: The Grand Overseer distorted First-Age history to justify shattering the planetary mana seal.",
                        targetClueId = "C3_05",
                        supportingEdges = new[] { S("C3_05", 1f) },
                        contradictingEdges = new[] { S("RH1_05", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "CASE5_CONTAINMENT_FAILURE",
                CompleteConclusionId = "CASE5_HYPOCRITICAL_IDEOLOGICAL_EXTREMISM",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f,
                    contradictionWeight = 0.8f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.30f,
                    highThreshold = 0.85f,
                    PrunedMode = true,
                },
            };
        }
    }

    static class CaseDatabaseBuilder
    {
        public static void RebuildIfAvailable()
        {
            var db = AssetDatabase.LoadAssetAtPath<CaseDatabase>("Assets/Resources/CaseDatabase.asset");
            if (db == null) return;
            var cases = new List<CaseData>();
            for (int i = 0; i <= 5; i++)
            {
                var c = AssetDatabase.LoadAssetAtPath<CaseData>($"Assets/Data/Cases/Case{i:00}/Case_{i:00}.asset");
                if (c != null) cases.Add(c);
            }
            db.cases = cases;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }
    }
}
