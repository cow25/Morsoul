using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Dialogue;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class DialogueRunnerTests
    {
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        DialogueData NewDialogue(params DialogueNodeData[] nodes)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = "DLG_TEST";
            data.nodes = nodes;
            data.startNodeId = nodes != null && nodes.Length > 0 ? nodes[0].id : "";
            _assets.Add(data);
            return data;
        }

        ClueData NewClue(string id)
        {
            var clue = ScriptableObject.CreateInstance<ClueData>();
            clue.id = id;
            clue.displayName = id;
            _assets.Add(clue);
            return clue;
        }

        static DialogueNodeData Node(string id, string next = null, string requires = null,
            string sets = null, ClueData clue = null, DialogueOptionData[] options = null,
            string speaker = "NPC", string text = "Text.")
        {
            return new DialogueNodeData
            {
                id = id,
                speakerName = speaker,
                text = text,
                registersClue = clue,
                setsFlag = sets,
                requiresFlag = requires,
                options = options,
                nextNodeId = next,
            };
        }

        static DialogueOptionData Opt(string label, string next, string requires = null) =>
            new DialogueOptionData { label = label, nextNodeId = next, requiresFlag = requires };

        static HashSet<string> Flags(params string[] flags) =>
            new HashSet<string>(flags);

        [Test]
        public void Ctor_NullData_Throws()
        {
            Assert.That(() => new DialogueRunner(null, Flags()), Throws.ArgumentNullException);
        }

        [Test]
        public void LinearAdvance_WalksNodes_ThenEnds()
        {
            var data = NewDialogue(Node("a", "b"), Node("b", "c"), Node("c"));
            var runner = new DialogueRunner(data, Flags());
            int ended = 0;
            runner.DialogueEnded += () => ended++;

            Assert.That(runner.Current.id, Is.EqualTo("a"));
            runner.Advance();
            Assert.That(runner.Current.id, Is.EqualTo("b"));
            runner.Advance();
            Assert.That(runner.Current.id, Is.EqualTo("c"));
            runner.Advance();

            Assert.That(runner.Current, Is.Null);
            Assert.That(runner.IsEnded, Is.True);
            Assert.That(ended, Is.EqualTo(1));

            runner.Advance(); // no-op after end
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void Options_BranchToChosenNode_AndBlockAdvance()
        {
            var data = NewDialogue(
                Node("a", next: "ignored",
                    options: new[] { Opt("Left", "l"), Opt("Right", "r") }),
                Node("l"), Node("r"));
            var runner = new DialogueRunner(data, Flags());

            Assert.That(runner.HasOptions, Is.True);
            Assert.That(runner.CurrentOptions.Count, Is.EqualTo(2));

            runner.Advance(); // options override nextNodeId: no-op
            Assert.That(runner.Current.id, Is.EqualTo("a"));

            runner.Choose(1);
            Assert.That(runner.Current.id, Is.EqualTo("r"));
            Assert.That(runner.HasOptions, Is.False);

            Assert.That(() => runner.Choose(0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void GatedNode_SkippedWhenFlagAbsent_ShownWhenPresent()
        {
            var data = NewDialogue(
                Node("a", "gated"),
                Node("gated", "b", requires: "metOvan"),
                Node("b"));

            var absent = new DialogueRunner(data, Flags());
            absent.Advance();
            Assert.That(absent.Current.id, Is.EqualTo("b"));

            var present = new DialogueRunner(data, Flags("metOvan"));
            present.Advance();
            Assert.That(present.Current.id, Is.EqualTo("gated"));
        }

        [Test]
        public void GatedOption_HiddenUntilFlagSet()
        {
            var data = NewDialogue(
                Node("a", options: new[]
                {
                    Opt("Public", "pub"),
                    Opt("Secret", "sec", requires: "knowsName"),
                }),
                Node("pub"), Node("sec"));

            var absent = new DialogueRunner(data, Flags());
            Assert.That(absent.CurrentOptions.Count, Is.EqualTo(1));
            Assert.That(absent.CurrentOptions[0].label, Is.EqualTo("Public"));

            var present = new DialogueRunner(data, Flags("knowsName"));
            Assert.That(present.CurrentOptions.Count, Is.EqualTo(2));
            present.Choose(1);
            Assert.That(present.Current.id, Is.EqualTo("sec"));
        }

        [Test]
        public void SubstituteName_ReplacesToken()
        {
            var data = NewDialogue(Node("a"));
            var runner = new DialogueRunner(data, Flags());

            Assert.That(runner.PlayerName, Is.EqualTo("Detective"));
            Assert.That(runner.SubstituteName("Hello {PlayerName}."),
                Is.EqualTo("Hello Detective."));

            runner.PlayerName = "Rowan";
            Assert.That(runner.SubstituteName("{PlayerName} and {PlayerName}"),
                Is.EqualTo("Rowan and Rowan"));
            Assert.That(runner.SubstituteName("No token."), Is.EqualTo("No token."));
            Assert.That(runner.SubstituteName(null), Is.EqualTo(""));
        }

        [Test]
        public void ShownNode_FiresClueAndFlag_OncePerShow_AndFlagApplies()
        {
            var clue = NewClue("C_TEST");
            var data = NewDialogue(
                Node("a", "b"),
                Node("b", "c", sets: "metNpc", clue: clue),
                Node("c", requires: "metNpc"));
            var runner = new DialogueRunner(data, Flags());
            int clueCount = 0, flagCount = 0;
            ClueData seenClue = null;
            string seenFlag = null;
            runner.ClueRegistered += c => { clueCount++; seenClue = c; };
            runner.FlagSet += f => { flagCount++; seenFlag = f; };

            runner.Advance(); // shows b
            Assert.That(clueCount, Is.EqualTo(1));
            Assert.That(seenClue, Is.SameAs(clue));
            Assert.That(flagCount, Is.EqualTo(1));
            Assert.That(seenFlag, Is.EqualTo("metNpc"));

            runner.Advance(); // c is gated by metNpc, which b just set
            Assert.That(runner.Current.id, Is.EqualTo("c"));
            Assert.That(clueCount, Is.EqualTo(1)); // c registers nothing
            Assert.That(flagCount, Is.EqualTo(1));
        }

        [Test]
        public void NodeShown_FiresAfterClueAndFlag()
        {
            var clue = NewClue("C_ORDER");
            var data = NewDialogue(
                Node("a", "b"),
                Node("b", sets: "f2", clue: clue));
            var runner = new DialogueRunner(data, Flags());
            var order = new List<string>();
            runner.ClueRegistered += _ => order.Add("clue");
            runner.FlagSet += _ => order.Add("flag");
            runner.NodeShown += n => order.Add("shown:" + n.id);

            runner.Advance();

            Assert.That(order, Is.EqualTo(new[] { "clue", "flag", "shown:b" }));
        }

        [Test]
        public void MissingNextNodeId_EndsDialogue()
        {
            var data = NewDialogue(
                Node("a", "b"),
                Node("b", "nowhere")); // dangling reference
            var runner = new DialogueRunner(data, Flags());
            int ended = 0;
            runner.DialogueEnded += () => ended++;

            runner.Advance();
            Assert.That(runner.Current.id, Is.EqualTo("b"));
            runner.Advance();
            Assert.That(runner.IsEnded, Is.True);
            Assert.That(ended, Is.EqualTo(1));

            // Empty option target also ends.
            var data2 = NewDialogue(Node("x", options: new[] { Opt("Done", "") }));
            var runner2 = new DialogueRunner(data2, Flags());
            runner2.Choose(0);
            Assert.That(runner2.IsEnded, Is.True);
        }

        [Test]
        public void GatedSkipLoop_EndsViaHopCap()
        {
            var data = NewDialogue(
                Node("a", "b"),
                Node("b", "c", requires: "never1"),
                Node("c", "b", requires: "never2"));
            var runner = new DialogueRunner(data, Flags());
            int ended = 0;
            runner.DialogueEnded += () => ended++;

            runner.Advance(); // b and c skip into each other forever

            Assert.That(runner.IsEnded, Is.True);
            Assert.That(runner.Current, Is.Null);
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void ShowLoop_EndsViaHopCap()
        {
            var data = NewDialogue(Node("a", "b"), Node("b", "a"));
            var runner = new DialogueRunner(data, Flags());
            int ended = 0;
            runner.DialogueEnded += () => ended++;

            for (int i = 0; i < DialogueRunner.MaxHops + 10 && !runner.IsEnded; i++)
                runner.Advance();

            Assert.That(runner.IsEnded, Is.True);
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void EmptyStart_EndsImmediately()
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = "DLG_EMPTY";
            data.nodes = new DialogueNodeData[0];
            data.startNodeId = "";
            _assets.Add(data);

            var runner = new DialogueRunner(data, Flags());
            Assert.That(runner.IsEnded, Is.True);
            Assert.That(runner.Current, Is.Null);
        }
    }
}
