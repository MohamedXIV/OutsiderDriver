using System;
using System.Collections.Generic;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Authoritative domain service managing the player's credits, language translator chips,
    /// cover integrity, and known resident relationships.
    /// </summary>
    public interface IPlayerStateService
    {
        int Credits { get; }
        int CoverIntegrity { get; }
        bool HasGpsModule { get; set; }
        bool HasRadioModule { get; set; }
        IReadOnlyList<string> InstalledLanguageIds { get; }
        PlayerWorldStateData WorldState { get; }

        event Action<int> OnCreditsChanged;
        event Action<int> OnCoverIntegrityChanged;
        event Action<string> OnLanguageInstalled;
        event Action<ResidentRelationshipData> OnResidentUpdated;

        void AddCredits(int amount);
        bool TrySpendCredits(int amount);
        void AdjustCoverIntegrity(int delta);
        bool CanUnderstandLanguage(string languageId);
        bool InstallLanguageModule(string languageId);

        ResidentRelationshipData GetOrAddResident(string residentId);
        void RecordResidentInteraction(string residentId, int affinityDelta, int trustDelta, int suspicionDelta, int currentTotalMinutes);
        void UpdateWorldPosition(UnityEngine.Vector2 position, string driverMode, string facing = null);

        SaveStateData ExportSaveState();
        void LoadSaveState(SaveStateData saveState);
    }
}
