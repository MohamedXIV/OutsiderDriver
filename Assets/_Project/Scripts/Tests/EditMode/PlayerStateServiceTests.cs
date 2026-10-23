using NUnit.Framework;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;

namespace OutsiderDriver.Tests.EditMode
{
    public class PlayerStateServiceTests
    {
        [Test]
        public void DefaultProfile_StartsWithCreditsCoverAndCommonLanguage()
        {
            var service = new PlayerStateService();

            Assert.AreEqual(100, service.Credits);
            Assert.AreEqual(100, service.CoverIntegrity);
            Assert.IsTrue(service.HasGpsModule);
            Assert.IsTrue(service.HasRadioModule);
            Assert.IsTrue(service.CanUnderstandLanguage(PlayerProfileData.DefaultCommonLanguageId));
            Assert.IsTrue(service.CanUnderstandLanguage("common"));
            Assert.IsFalse(service.CanUnderstandLanguage("Vesperian"));
        }

        [Test]
        public void Credits_AddAndSpend_UpdatesBalanceAndFiresEvents()
        {
            var service = new PlayerStateService();
            int lastReportedCredits = -1;
            service.OnCreditsChanged += c => lastReportedCredits = c;

            service.AddCredits(50);
            Assert.AreEqual(150, service.Credits);
            Assert.AreEqual(150, lastReportedCredits);

            bool spentOk = service.TrySpendCredits(120);
            Assert.IsTrue(spentOk);
            Assert.AreEqual(30, service.Credits);
            Assert.AreEqual(30, lastReportedCredits);

            bool overSpend = service.TrySpendCredits(50);
            Assert.IsFalse(overSpend);
            Assert.AreEqual(30, service.Credits);
        }

        [Test]
        public void InstallLanguageModule_AddsLanguageIdWithoutDuplicates()
        {
            var service = new PlayerStateService();
            string installedLang = null;
            service.OnLanguageInstalled += lang => installedLang = lang;

            bool firstInstall = service.InstallLanguageModule("Vesperian");
            bool duplicateInstall = service.InstallLanguageModule("vesperian");

            Assert.IsTrue(firstInstall);
            Assert.IsFalse(duplicateInstall);
            Assert.AreEqual("Vesperian", installedLang);
            Assert.IsTrue(service.CanUnderstandLanguage("VESPERIAN"));
        }

        [Test]
        public void RecordResidentInteraction_TracksAffinityTrustAndSuspicionSeparately()
        {
            var service = new PlayerStateService();

            service.RecordResidentInteraction("res_nyx", affinityDelta: 15, trustDelta: 5, suspicionDelta: 20, currentTotalMinutes: 1200);
            service.RecordResidentInteraction("res_nyx", affinityDelta: 10, trustDelta: -2, suspicionDelta: 95, currentTotalMinutes: 1350);

            var resident = service.GetOrAddResident("res_nyx");
            Assert.AreEqual(2, resident.EncounterCount);
            Assert.AreEqual(25, resident.Affinity);
            Assert.AreEqual(3, resident.PersonalTrust);
            Assert.AreEqual(100, resident.SuspicionOfHumanity); // Clamped at 100
            Assert.AreEqual(1350, resident.LastEncounterTotalMinutes);
        }

        [Test]
        public void SaveStateData_JsonRoundTrip_RestoresTimeProfileAndRelationships()
        {
            var timeService = new TimeService(null);
            timeService.SetTime(new WorldTime(day: 4, hour: 22, minute: 45));

            var originalService = new PlayerStateService(timeService);
            originalService.AddCredits(250);
            originalService.AdjustCoverIntegrity(-15);
            originalService.HasRadioModule = false;
            originalService.InstallLanguageModule("Krellish");
            originalService.RecordResidentInteraction("res_sol", affinityDelta: 30, trustDelta: 40, suspicionDelta: 10, currentTotalMinutes: 5000);

            SaveStateData exported = originalService.ExportSaveState();
            string json = exported.ToJson(prettyPrint: false);

            SaveStateData deserialized = SaveStateData.FromJson(json);

            var targetTimeService = new TimeService(null);
            var restoredService = new PlayerStateService(targetTimeService);
            restoredService.LoadSaveState(deserialized);

            Assert.AreEqual(new WorldTime(4, 22, 45), targetTimeService.CurrentTime);
            Assert.AreEqual(350, restoredService.Credits);
            Assert.AreEqual(85, restoredService.CoverIntegrity);
            Assert.IsFalse(restoredService.HasRadioModule);
            Assert.IsTrue(restoredService.CanUnderstandLanguage("Krellish"));

            var restoredResident = restoredService.GetOrAddResident("res_sol");
            Assert.AreEqual(1, restoredResident.EncounterCount);
            Assert.AreEqual(30, restoredResident.Affinity);
            Assert.AreEqual(40, restoredResident.PersonalTrust);
            Assert.AreEqual(10, restoredResident.SuspicionOfHumanity);
            Assert.AreEqual(5000, restoredResident.LastEncounterTotalMinutes);
        }
    }
}
