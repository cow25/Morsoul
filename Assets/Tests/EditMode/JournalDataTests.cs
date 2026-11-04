using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Journal;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// EditMode coverage for the Clue Journal's pure helpers (JournalFilter):
    /// tab membership, dossier flag-gating, locked-log masking, preview
    /// truncation and log titles. UI rendering itself is not testable in
    /// EditMode — that is exactly why the rules live in a static class.
    /// </summary>
    public class JournalDataTests
    {
        readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
                if (obj != null)
                    Object.DestroyImmediate(obj);
            _objects.Clear();
        }

        ClueData NewClue(ClueType type)
        {
            var clue = ScriptableObject.CreateInstance<ClueData>();
            clue.type = type;
            _objects.Add(clue);
            return clue;
        }

        RunicLogData NewLog(int id)
        {
            var log = ScriptableObject.CreateInstance<RunicLogData>();
            log.id = id;
            log.contents = "Secret contents.";
            _objects.Add(log);
            return log;
        }

        // --- Tab membership -----------------------------------------------------

        [Test]
        public void ForTab_PhysicalTab_AdmitsPhysicalAndRedHerring()
        {
            Assert.That(JournalFilter.ForTab(ClueType.Physical, JournalTab.Physical), Is.True);
            Assert.That(JournalFilter.ForTab(ClueType.RedHerring, JournalTab.Physical), Is.True);
            Assert.That(JournalFilter.ForTab(ClueType.Testimony, JournalTab.Physical), Is.False);
            Assert.That(JournalFilter.ForTab(ClueType.Latent, JournalTab.Physical), Is.False);
        }

        [Test]
        public void ForTab_TestimonyTab_AdmitsTestimonyOnly()
        {
            Assert.That(JournalFilter.ForTab(ClueType.Testimony, JournalTab.Testimony), Is.True);
            Assert.That(JournalFilter.ForTab(ClueType.Physical, JournalTab.Testimony), Is.False);
            Assert.That(JournalFilter.ForTab(ClueType.RedHerring, JournalTab.Testimony), Is.False);
            Assert.That(JournalFilter.ForTab(ClueType.Latent, JournalTab.Testimony), Is.False);
        }

        [Test]
        public void ForTab_NonClueTabs_RejectEveryClueType()
        {
            Assert.That(JournalFilter.ForTab(ClueType.Physical, JournalTab.Logs), Is.False);
            Assert.That(JournalFilter.ForTab(ClueType.Testimony, JournalTab.Dossier), Is.False);
        }

        [Test]
        public void CluesForTab_FiltersWrongTypes_AndDropsNulls()
        {
            var physical = NewClue(ClueType.Physical);
            var testimony = NewClue(ClueType.Testimony);
            var clues = new List<ClueData> { physical, null, testimony };

            var result = JournalFilter.CluesForTab(clues, JournalTab.Physical);
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Is.SameAs(physical));
            Assert.That(JournalFilter.CluesForTab(null, JournalTab.Physical), Is.Empty);
        }

        // --- Preview truncation ---------------------------------------------------

        [Test]
        public void Preview_ShortText_ReturnedWhole()
        {
            Assert.That(JournalFilter.Preview("Short."), Is.EqualTo("Short."));
            Assert.That(JournalFilter.Preview(null), Is.EqualTo(""));
            Assert.That(JournalFilter.Preview(""), Is.EqualTo(""));
        }

        [Test]
        public void Preview_LongText_TruncatesAtEighty_WithEllipsis()
        {
            var text = new string('x', 120);
            var preview = JournalFilter.Preview(text);
            Assert.That(preview.Length, Is.EqualTo(JournalFilter.PreviewLength + 1));
            Assert.That(preview, Does.StartWith(new string('x', JournalFilter.PreviewLength)));
            Assert.That(preview, Does.EndWith("…"));
        }

        // --- Locked-log masking -----------------------------------------------------

        [Test]
        public void Mask_ReplacesNonWhitespace_PreservesWordShape()
        {
            var masked = JournalFilter.Mask("The seal is broken");
            Assert.That(masked, Is.EqualTo("??? ???? ?? ??????"));
            Assert.That(masked.Length, Is.EqualTo("The seal is broken".Length));
        }

        [Test]
        public void Mask_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(JournalFilter.Mask(null), Is.EqualTo(""));
            Assert.That(JournalFilter.Mask(""), Is.EqualTo(""));
        }

        [Test]
        public void IsLogCollected_OnlyForRegisteredLogs()
        {
            var log = NewLog(3);
            var other = NewLog(4);
            var collected = new List<RunicLogData> { log };

            Assert.That(JournalFilter.IsLogCollected(log, collected), Is.True);
            Assert.That(JournalFilter.IsLogCollected(other, collected), Is.False);
            Assert.That(JournalFilter.IsLogCollected(null, collected), Is.False);
            Assert.That(JournalFilter.IsLogCollected(log, null), Is.False);
        }

        [Test]
        public void LogTitle_UsesNumericId()
        {
            Assert.That(JournalFilter.LogTitle(NewLog(7)), Is.EqualTo("Runic Log 7"));
            Assert.That(JournalFilter.LogTitle(null), Is.EqualTo("Runic Log ?"));
        }

        // --- Dossier & flag gating ----------------------------------------------------

        [Test]
        public void IsDossierVisible_NoRequirement_AlwaysVisible()
        {
            Assert.That(JournalFilter.IsDossierVisible(null, null), Is.True);
            Assert.That(JournalFilter.IsDossierVisible("", null), Is.True);
            Assert.That(JournalFilter.IsDossierVisible("", new HashSet<string>()), Is.True);
        }

        [Test]
        public void IsDossierVisible_RequiredFlag_MustBePresent()
        {
            var flags = new HashSet<string>();
            Assert.That(JournalFilter.IsDossierVisible("metSuspect", flags), Is.False);
            Assert.That(JournalFilter.IsDossierVisible("metSuspect", null), Is.False);

            flags.Add("metSuspect");
            Assert.That(JournalFilter.IsDossierVisible("metSuspect", flags), Is.True);
        }

        [Test]
        public void HasFlag_DissonanceGating()
        {
            var flags = new HashSet<string> { "unease_C1" };
            Assert.That(JournalFilter.HasFlag(flags, "unease_C1"), Is.True);
            Assert.That(JournalFilter.HasFlag(flags, "unease_C2"), Is.False);
            Assert.That(JournalFilter.HasFlag(flags, ""), Is.False);
            Assert.That(JournalFilter.HasFlag(null, "unease_C1"), Is.False);
        }
    }
}
