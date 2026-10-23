using System;
using System.Collections.Generic;
using OutsiderDriver.Player;
using OutsiderDriver.World;

namespace OutsiderDriver.Audio
{
    /// <summary>
    /// Contextual ambient city and domestic soundscape profiles.
    /// </summary>
    public enum AmbientSoundscapeMode
    {
        /// <summary>
        /// Driving inside the taxi cabin surrounded by city traffic and street hum.
        /// </summary>
        CityStreetAndTraffic = 0,

        /// <summary>
        /// Walking on the neighborhood curb/sidewalk outside the apartment.
        /// </summary>
        NeighborhoodCurbside = 1,

        /// <summary>
        /// Inside the warm, sheltered apartment home interior.
        /// </summary>
        CozyHomeInterior = 2
    }

    /// <summary>
    /// Contract for the alien radio tuner and ambient city soundscape controller
    /// (GAME_DESIGN §5 "Radio and information", Decisions H-008, H-009).
    /// </summary>
    public interface IRadioAudioService
    {
        bool IsRadioOn { get; }
        int CurrentStationIndex { get; }
        RadioStationSO CurrentStation { get; }
        RadioProgramEntry CurrentProgram { get; }
        bool IsCurrentProgramUnderstood { get; }
        string CurrentBroadcastCaption { get; }
        AmbientSoundscapeMode CurrentSoundscape { get; }

        float RadioVolume { get; set; }
        float AmbientVolume { get; set; }

        IReadOnlyList<RadioStationSO> Stations { get; }

        event Action<bool> OnRadioPowerChanged;
        event Action<RadioStationSO, RadioProgramEntry> OnBroadcastChanged;
        event Action<AmbientSoundscapeMode> OnSoundscapeChanged;

        void SetRadioPower(bool powerOn);
        void ToggleRadio();
        void NextStation();
        void PreviousStation();
        bool TuneToStation(string stationId);
        void RefreshCurrentBroadcast();
        void UpdateSoundscapeForContext(DriverMode driverMode, WalkableZone currentZone);
    }
}
