using System.IO;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Core.Persistence;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class SaveServiceTests
    {
        private string _testSaveDir;
        private TimeService _timeService;
        private PlayerStateService _playerState;
        private SaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _testSaveDir = Path.Combine(Path.GetTempPath(), "OutsiderDriver_SaveTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testSaveDir);

            _timeService = new TimeService();
            _timeService.SetTime(new WorldTime(day: 2, hour: 14, minute: 30));

            _playerState = new PlayerStateService(_timeService);
            _saveService = new SaveService(_playerState, _testSaveDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testSaveDir))
            {
                try
                {
                    Directory.Delete(_testSaveDir, recursive: true);
                }
                catch
                {
                    // Ignore temp directory deletion cleanup issues on Windows
                }
            }
        }

        [Test]
        public void Save_WritesAtomicJsonToDiskAndFiresEvent()
        {
            _playerState.AddCredits(350);
            _playerState.AdjustCoverIntegrity(-15);
            _playerState.InstallLanguageModule("Local");

            SaveStateData capturedSave = null;
            _saveService.OnSaveCompleted += s => capturedSave = s;

            bool saved = _saveService.Save();
            Assert.IsTrue(saved);
            Assert.IsNotNull(capturedSave);
            Assert.AreEqual(450, capturedSave.Profile.Credits);
            Assert.AreEqual(85, capturedSave.Profile.CoverIntegrity);
            Assert.AreEqual(2, capturedSave.Day);
            Assert.AreEqual(14, capturedSave.Hour);
            Assert.AreEqual(30, capturedSave.Minute);

            string expectedFilePath = _saveService.GetSaveFilePath(ISaveService.DefaultSlotName);
            Assert.IsTrue(File.Exists(expectedFilePath));

            string json = File.ReadAllText(expectedFilePath);
            Assert.IsTrue(json.Contains("450"));
            Assert.IsTrue(json.Contains("Local"));
        }

        [Test]
        public void Load_RestoresPlayerProfileAndTimeFromDisk()
        {
            // 1. Prepare and save modified state
            _playerState.AddCredits(500);
            _playerState.AdjustCoverIntegrity(-20);
            _playerState.InstallLanguageModule("TradeCant");
            _playerState.RecordResidentInteraction("res_lyra", affinityDelta: 15, trustDelta: 10, suspicionDelta: 0, currentTotalMinutes: 200);

            _timeService.SetTime(new WorldTime(day: 3, hour: 21, minute: 45));

            bool saved = _saveService.Save("slot_alpha");
            Assert.IsTrue(saved);

            // 2. Create fresh target state and service
            var freshTime = new TimeService();
            freshTime.SetTime(new WorldTime(day: 1, hour: 8, minute: 0));

            var freshState = new PlayerStateService(freshTime);
            var freshSaveService = new SaveService(freshState, _testSaveDir);

            Assert.AreEqual(100, freshState.Credits);
            Assert.AreEqual(100, freshState.CoverIntegrity);
            Assert.AreEqual(1, freshTime.CurrentTime.Day);

            // 3. Load from disk
            SaveStateData capturedLoaded = null;
            freshSaveService.OnLoadCompleted += s => capturedLoaded = s;

            bool loaded = freshSaveService.Load("slot_alpha");
            Assert.IsTrue(loaded);
            Assert.IsNotNull(capturedLoaded);

            // 4. Verify runtime state was restored
            Assert.AreEqual(600, freshState.Credits);
            Assert.AreEqual(80, freshState.CoverIntegrity);
            Assert.IsTrue(freshState.CanUnderstandLanguage("TradeCant"));

            var resident = freshState.GetOrAddResident("res_lyra");
            Assert.AreEqual(15, resident.Affinity);
            Assert.AreEqual(10, resident.PersonalTrust);

            Assert.AreEqual(3, freshTime.CurrentTime.Day);
            Assert.AreEqual(21, freshTime.CurrentTime.Hour);
            Assert.AreEqual(45, freshTime.CurrentTime.Minute);
        }

        [Test]
        public void DeleteSave_RemovesPrimaryAndBackupFiles()
        {
            _saveService.Save();
            string path = _saveService.GetSaveFilePath(ISaveService.DefaultSlotName);
            Assert.IsTrue(File.Exists(path));
            Assert.IsTrue(_saveService.HasSaveFile());

            // Save again to produce a .bak backup
            _saveService.Save();
            Assert.IsTrue(File.Exists(path + ".bak"));

            bool deleted = _saveService.DeleteSave();
            Assert.IsTrue(deleted);
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".bak"));
            Assert.IsFalse(_saveService.HasSaveFile());
        }

        [Test]
        public void Load_WhenPrimaryMissingOrCorrupt_RecoversFromBackupFile()
        {
            _playerState.AddCredits(200);
            _saveService.Save(); // creates primary save file with 300 credits (100 base + 200)

            _playerState.AddCredits(100);
            _saveService.Save(); // backs up 300-credits save to .bak and writes 400-credits to primary

            string primaryPath = _saveService.GetSaveFilePath(ISaveService.DefaultSlotName);
            string backupPath = primaryPath + ".bak";

            Assert.IsTrue(File.Exists(primaryPath));
            Assert.IsTrue(File.Exists(backupPath));

            // Corrupt the primary save file with garbage data
            File.WriteAllText(primaryPath, "CORRUPT_NOT_VALID_JSON_$$$");

            var freshTime = new TimeService();
            var freshState = new PlayerStateService(freshTime);
            var freshSaveService = new SaveService(freshState, _testSaveDir);

            // Load should fail primary and cleanly recover the 300-credits state from .bak
            bool loaded = freshSaveService.Load();
            Assert.IsTrue(loaded);
            Assert.AreEqual(300, freshState.Credits);
        }
    }
}
