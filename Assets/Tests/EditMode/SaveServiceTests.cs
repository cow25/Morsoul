using System.IO;
using Morsoul.Core;
using Morsoul.Data;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class SaveServiceTests
    {
        string _dir;
        SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(),
                "MorsoulSaveTests_" + Path.GetRandomFileName());
            _service = new SaveService(Path.Combine(_dir, "checkpoint.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static SaveState SampleState()
        {
            return new SaveState
            {
                playerName = "Vera",
                currentCase = 2,
                checkpointSceneName = "Hub_LowerMarket",
                collectedClueIds = { "C1_01", "T1_01" },
                boardEdges = { "IN1_01" },
                runicLogsCollected = new System.Collections.Generic.List<bool> { true, false, false, false },
                caseGrades = { Grade.Clean, Grade.Standard },
                uneaseFlags = { new UneaseFlag("Case1_UneaseActive", true) },
                toolsUnlocked = { Tool.GolemEye, Tool.EmotionEcho },
                focusPercent = 62.5f,
                strainedScanUsed = true
            };
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            _service.Save(SampleState());
            Assert.IsTrue(_service.HasSave);
            Assert.IsTrue(_service.TryLoad(out var loaded, out string error), error);

            Assert.AreEqual("Vera", loaded.playerName);
            Assert.AreEqual(2, loaded.currentCase);
            Assert.AreEqual("Hub_LowerMarket", loaded.checkpointSceneName);
            CollectionAssert.AreEqual(new[] { "C1_01", "T1_01" }, loaded.collectedClueIds);
            CollectionAssert.AreEqual(new[] { "IN1_01" }, loaded.boardEdges);
            CollectionAssert.AreEqual(new[] { true, false, false, false },
                loaded.runicLogsCollected);
            CollectionAssert.AreEqual(new[] { Grade.Clean, Grade.Standard }, loaded.caseGrades);
            Assert.AreEqual(1, loaded.uneaseFlags.Count);
            Assert.AreEqual("Case1_UneaseActive", loaded.uneaseFlags[0].key);
            Assert.IsTrue(loaded.uneaseFlags[0].value);
            CollectionAssert.AreEqual(new[] { Tool.GolemEye, Tool.EmotionEcho },
                loaded.toolsUnlocked);
            Assert.AreEqual(62.5f, loaded.focusPercent, 0.001f);
            Assert.IsTrue(loaded.strainedScanUsed);
        }

        [Test]
        public void TamperedPayload_SignatureMismatch()
        {
            _service.Save(SampleState());
            string path = Path.Combine(_dir, "checkpoint.json");
            string json = File.ReadAllText(path);
            // Flip a value inside the payload: "currentCase":2 -> 7.
            json = json.Replace("currentCase\\\":2", "currentCase\\\":7");
            Assert.AreNotEqual(json, File.ReadAllText(path));
            File.WriteAllText(path, json);

            Assert.IsFalse(_service.TryLoad(out _, out string error));
            StringAssert.Contains("signature", error);
        }

        [Test]
        public void MalformedFile_Rejected_NotThrown()
        {
            string path = Path.Combine(_dir, "checkpoint.json");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(path, "{ this is not json !!!");
            Assert.IsFalse(_service.TryLoad(out _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void MissingFile_HasSaveFalse_TryLoadFails()
        {
            Assert.IsFalse(_service.HasSave);
            Assert.IsFalse(_service.TryLoad(out _, out _));
        }

        [Test]
        public void Overwrite_ReplacesPriorState()
        {
            _service.Save(SampleState());
            var second = SampleState();
            second.playerName = "Odo";
            second.currentCase = 5;
            _service.Save(second);
            Assert.IsTrue(_service.TryLoad(out var loaded, out _));
            Assert.AreEqual("Odo", loaded.playerName);
            Assert.AreEqual(5, loaded.currentCase);
        }

        [Test]
        public void DeleteSave_RemovesFile()
        {
            _service.Save(SampleState());
            _service.DeleteSave();
            Assert.IsFalse(_service.HasSave);
        }
    }
}
