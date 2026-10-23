using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OutsiderDriver.Audio;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Localization;
using OutsiderDriver.Player;
using OutsiderDriver.World;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class RadioAudioControllerTests
    {
        private GameObject _rootGo;
        private RadioAudioController _radioController;
        private RadioStationSO _commonStation;
        private RadioStationSO _localAlienStation;
        private TimeService _timeService;
        private PlayerStateService _playerState;
        private LocalizationService _localizationService;

        [SetUp]
        public void SetUp()
        {
            _rootGo = new GameObject("RadioAudioTestRoot");

            _timeService = new TimeService();
            _timeService.SetTime(new WorldTime(1, 10, 0)); // 10:00 AM

            _playerState = new PlayerStateService();
            _localizationService = new LocalizationService("en");

            _commonStation = ScriptableObject.CreateInstance<RadioStationSO>();
            _commonStation.Configure(
                "station_common",
                "Amber Wave Transit",
                "94.6 MHz",
                "Common",
                new[]
                {
                    new RadioProgramEntry(
                        "prog_morning",
                        "Morning Commute",
                        "Clear skies over Central Junction.",
                        "Common",
                        6,
                        17),
                    new RadioProgramEntry(
                        "prog_night",
                        "Night Drift",
                        "Quiet canals until dawn.",
                        "Common",
                        18,
                        5)
                });

            _localAlienStation = ScriptableObject.CreateInstance<RadioStationSO>();
            _localAlienStation.Configure(
                "station_vesper",
                "Vesperian Chimes",
                "108.2 MHz",
                "Local",
                new[]
                {
                    new RadioProgramEntry(
                        "prog_local_talk",
                        "Terrace Voices",
                        "Vesperian elders discussing the harvest.",
                        "Local",
                        0,
                        23)
                });

            var radioGo = new GameObject("RadioController");
            radioGo.transform.SetParent(_rootGo.transform, false);
            _radioController = radioGo.AddComponent<RadioAudioController>();
            _radioController.ConfigureStations(new List<RadioStationSO> { _commonStation, _localAlienStation }, startOn: true);
            _radioController.Construct(_timeService, _playerState, _localizationService);
        }

        [TearDown]
        public void TearDown()
        {
            if (_commonStation != null) Object.DestroyImmediate(_commonStation);
            if (_localAlienStation != null) Object.DestroyImmediate(_localAlienStation);
            if (_rootGo != null) Object.DestroyImmediate(_rootGo);
        }

        [Test]
        public void StationTuningAndSchedule_ResolvesProgramByWorldClockHour()
        {
            Assert.IsTrue(_radioController.IsRadioOn);
            Assert.AreEqual("station_common", _radioController.CurrentStation.StationId);
            Assert.AreEqual("prog_morning", _radioController.CurrentProgram.ProgramId);
            Assert.IsTrue(_radioController.IsCurrentProgramUnderstood);
            Assert.AreEqual("Clear skies over Central Junction.", _radioController.CurrentBroadcastCaption);

            // Advance clock to 20:00 (8:00 PM) and trigger hour tick via AdvanceTime
            _timeService.SetTime(new WorldTime(1, 19, 59));
            _timeService.AdvanceTime(2f * _timeService.RealSecondsPerInGameMinute);

            Assert.AreEqual(20, _timeService.CurrentTime.Hour);
            Assert.AreEqual("prog_night", _radioController.CurrentProgram.ProgramId);
            Assert.AreEqual("Quiet canals until dawn.", _radioController.CurrentBroadcastCaption);

            // Toggle radio power off and back on
            _radioController.ToggleRadio();
            Assert.IsFalse(_radioController.IsRadioOn);
            Assert.AreEqual(string.Empty, _radioController.CurrentBroadcastCaption);

            _radioController.ToggleRadio();
            Assert.IsTrue(_radioController.IsRadioOn);
            Assert.AreEqual("prog_night", _radioController.CurrentProgram.ProgramId);
        }

        [Test]
        public void AlienLanguageBroadcast_ObfuscatesCaptionUntilTranslatorModuleInstalled()
        {
            // Tune to the Local alien language station
            bool tuned = _radioController.TuneToStation("station_vesper");
            Assert.IsTrue(tuned);
            Assert.AreEqual("station_vesper", _radioController.CurrentStation.StationId);

            // Driver only starts with 'Common' installed -> 'Local' broadcast is not understood
            Assert.IsFalse(_radioController.IsCurrentProgramUnderstood);
            Assert.AreNotEqual("Vesperian elders discussing the harvest.", _radioController.CurrentBroadcastCaption);
            Assert.IsNotEmpty(_radioController.CurrentBroadcastCaption);

            // Install the 'Local' language module -> radio controller automatically refreshes comprehension
            bool installed = _playerState.InstallLanguageModule("Local");
            Assert.IsTrue(installed);
            Assert.IsTrue(_radioController.IsCurrentProgramUnderstood);
            Assert.AreEqual("Vesperian elders discussing the harvest.", _radioController.CurrentBroadcastCaption);
        }

        [Test]
        public void AmbientSoundscape_UpdatesBasedOnDriverModeAndWalkableZone()
        {
            var homeZoneGo = new GameObject("HomeZone");
            homeZoneGo.transform.SetParent(_rootGo.transform, false);
            var homeZone = homeZoneGo.AddComponent<WalkableZone>();
            homeZone.Configure("zone_home", "Apartment", WalkableZoneType.HomeInterior, new Rect(-2, -2, 4, 4));

            var sidewalkZoneGo = new GameObject("SidewalkZone");
            sidewalkZoneGo.transform.SetParent(_rootGo.transform, false);
            var sidewalkZone = sidewalkZoneGo.AddComponent<WalkableZone>();
            sidewalkZone.Configure("zone_street", "Sidewalk", WalkableZoneType.NeighborhoodSidewalk, new Rect(-2, -2, 4, 4));

            // 1. Default InTaxi mode -> CityStreetAndTraffic
            _radioController.UpdateSoundscapeForContext(DriverMode.InTaxi, sidewalkZone);
            Assert.AreEqual(AmbientSoundscapeMode.CityStreetAndTraffic, _radioController.CurrentSoundscape);

            // 2. OnFoot on sidewalk -> NeighborhoodCurbside
            _radioController.UpdateSoundscapeForContext(DriverMode.OnFoot, sidewalkZone);
            Assert.AreEqual(AmbientSoundscapeMode.NeighborhoodCurbside, _radioController.CurrentSoundscape);

            // 3. OnFoot inside apartment -> CozyHomeInterior
            _radioController.UpdateSoundscapeForContext(DriverMode.OnFoot, homeZone);
            Assert.AreEqual(AmbientSoundscapeMode.CozyHomeInterior, _radioController.CurrentSoundscape);
        }
    }
}
