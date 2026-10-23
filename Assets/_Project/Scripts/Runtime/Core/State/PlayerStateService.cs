using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OutsiderDriver.Core.Time;

namespace OutsiderDriver.Core.State
{
    /// <summary>
    /// Concrete domain service managing runtime player profile state and save snapshots.
    /// </summary>
    public sealed class PlayerStateService : IPlayerStateService
    {
        private readonly ITimeService _timeService;
        private PlayerProfileData _profile;
        private readonly Dictionary<string, ResidentRelationshipData> _residentLookup = new Dictionary<string, ResidentRelationshipData>(StringComparer.OrdinalIgnoreCase);

        public int Credits => _profile.Credits;
        public int CoverIntegrity => _profile.CoverIntegrity;

        public bool HasGpsModule
        {
            get => _profile.HasGpsModule;
            set => _profile.HasGpsModule = value;
        }

        public bool HasRadioModule
        {
            get => _profile.HasRadioModule;
            set => _profile.HasRadioModule = value;
        }

        public IReadOnlyList<string> InstalledLanguageIds => _profile.InstalledLanguageIds;

        public event Action<int> OnCreditsChanged;
        public event Action<int> OnCoverIntegrityChanged;
        public event Action<string> OnLanguageInstalled;
        public event Action<ResidentRelationshipData> OnResidentUpdated;

        [Inject]
        public PlayerStateService(ITimeService timeService = null)
        {
            _timeService = timeService;
            _profile = new PlayerProfileData();
            RebuildResidentLookup();
        }

        public void AddCredits(int amount)
        {
            if (amount <= 0) return;
            _profile.Credits += amount;
            OnCreditsChanged?.Invoke(_profile.Credits);
        }

        public bool TrySpendCredits(int amount)
        {
            if (amount < 0 || _profile.Credits < amount)
            {
                return false;
            }

            if (amount == 0) return true;

            _profile.Credits -= amount;
            OnCreditsChanged?.Invoke(_profile.Credits);
            return true;
        }

        public void AdjustCoverIntegrity(int delta)
        {
            int updated = Mathf.Clamp(_profile.CoverIntegrity + delta, 0, 100);
            if (updated == _profile.CoverIntegrity) return;

            _profile.CoverIntegrity = updated;
            OnCoverIntegrityChanged?.Invoke(_profile.CoverIntegrity);
        }

        public bool CanUnderstandLanguage(string languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId)) return true;

            for (int i = 0; i < _profile.InstalledLanguageIds.Count; i++)
            {
                if (string.Equals(_profile.InstalledLanguageIds[i], languageId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool InstallLanguageModule(string languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId) || CanUnderstandLanguage(languageId))
            {
                return false;
            }

            _profile.InstalledLanguageIds.Add(languageId);
            OnLanguageInstalled?.Invoke(languageId);
            return true;
        }

        public ResidentRelationshipData GetOrAddResident(string residentId)
        {
            if (string.IsNullOrWhiteSpace(residentId))
            {
                throw new ArgumentException("Resident ID cannot be null or empty.", nameof(residentId));
            }

            if (_residentLookup.TryGetValue(residentId, out var existing))
            {
                return existing;
            }

            var created = new ResidentRelationshipData(residentId);
            _profile.KnownResidents.Add(created);
            _residentLookup[residentId] = created;
            return created;
        }

        public void RecordResidentInteraction(string residentId, int affinityDelta, int trustDelta, int suspicionDelta, int currentTotalMinutes)
        {
            var data = GetOrAddResident(residentId);
            data.EncounterCount++;
            data.Affinity += affinityDelta;
            data.PersonalTrust += trustDelta;
            data.SuspicionOfHumanity = Mathf.Clamp(data.SuspicionOfHumanity + suspicionDelta, 0, 100);
            data.LastEncounterTotalMinutes = currentTotalMinutes;

            OnResidentUpdated?.Invoke(data);
        }

        public SaveStateData ExportSaveState()
        {
            var save = new SaveStateData
            {
                Version = 1,
                Profile = JsonUtility.FromJson<PlayerProfileData>(JsonUtility.ToJson(_profile))
            };

            if (_timeService != null)
            {
                save.SetWorldTime(_timeService.CurrentTime);
            }

            return save;
        }

        public void LoadSaveState(SaveStateData saveState)
        {
            if (saveState == null) return;

            _profile = saveState.Profile != null
                ? JsonUtility.FromJson<PlayerProfileData>(JsonUtility.ToJson(saveState.Profile))
                : new PlayerProfileData();

            _profile.InstalledLanguageIds ??= new List<string>();
            _profile.KnownResidents ??= new List<ResidentRelationshipData>();
            RebuildResidentLookup();

            if (_timeService != null)
            {
                _timeService.SetTime(saveState.GetWorldTime());
            }

            OnCreditsChanged?.Invoke(_profile.Credits);
            OnCoverIntegrityChanged?.Invoke(_profile.CoverIntegrity);
        }

        private void RebuildResidentLookup()
        {
            _residentLookup.Clear();
            for (int i = 0; i < _profile.KnownResidents.Count; i++)
            {
                var resident = _profile.KnownResidents[i];
                if (resident != null && !string.IsNullOrWhiteSpace(resident.ResidentId))
                {
                    _residentLookup[resident.ResidentId] = resident;
                }
            }
        }
    }
}
