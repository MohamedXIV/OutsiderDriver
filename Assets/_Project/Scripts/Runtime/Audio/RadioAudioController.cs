using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Dialogue;
using OutsiderDriver.Localization;
using OutsiderDriver.Player;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.World;

namespace OutsiderDriver.Audio
{
    /// <summary>
    /// Manages in-world alien radio stations, time-scheduled broadcasts, language module comprehension
    /// for spoken radio segments, and contextual ambient city/interior soundscapes
    /// (GAME_DESIGN §5 "Radio and information", Decisions H-008, H-009).
    /// </summary>
    [DisallowMultipleComponent]
    public class RadioAudioController : MonoBehaviour, IRadioAudioService
    {
        [Header("Radio Stations")]
        [SerializeField] private List<RadioStationSO> _stations = new List<RadioStationSO>();
        [SerializeField] private bool _startWithRadioOn = true;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _radioAudioSource;
        [SerializeField] private AudioSource _ambientAudioSource;

        [Header("Ambient Soundscape Clips (Optional)")]
        [SerializeField] private AudioClip _cityTrafficAmbienceClip;
        [SerializeField] private AudioClip _neighborhoodCurbsideClip;
        [SerializeField] private AudioClip _cozyHomeInteriorClip;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float _radioVolume = 0.75f;
        [Range(0f, 1f)]
        [SerializeField] private float _ambientVolume = 0.6f;

        private ITimeService _timeService;
        private IPlayerStateService _playerState;
        private ILocalizationService _localizationService;
        private PlayerController _playerController;

        private bool _isRadioOn;
        private int _currentStationIndex;
        private RadioProgramEntry _currentProgram;
        private bool _isCurrentProgramUnderstood = true;
        private string _currentBroadcastCaption = string.Empty;
        private AmbientSoundscapeMode _currentSoundscape = AmbientSoundscapeMode.CityStreetAndTraffic;

        public bool IsRadioOn => _isRadioOn;
        public int CurrentStationIndex => _currentStationIndex;
        public RadioStationSO CurrentStation =>
            (_stations.Count > 0 && _currentStationIndex >= 0 && _currentStationIndex < _stations.Count)
                ? _stations[_currentStationIndex]
                : null;

        public RadioProgramEntry CurrentProgram => _currentProgram;
        public bool IsCurrentProgramUnderstood => _isCurrentProgramUnderstood;
        public string CurrentBroadcastCaption => _currentBroadcastCaption;
        public AmbientSoundscapeMode CurrentSoundscape => _currentSoundscape;

        public float RadioVolume
        {
            get => _radioVolume;
            set
            {
                _radioVolume = Mathf.Clamp01(value);
                ApplyAudioSourceVolumes();
            }
        }

        public float AmbientVolume
        {
            get => _ambientVolume;
            set
            {
                _ambientVolume = Mathf.Clamp01(value);
                ApplyAudioSourceVolumes();
            }
        }

        public IReadOnlyList<RadioStationSO> Stations => _stations;

        public event Action<bool> OnRadioPowerChanged;
        public event Action<RadioStationSO, RadioProgramEntry> OnBroadcastChanged;
        public event Action<AmbientSoundscapeMode> OnSoundscapeChanged;

        [Inject]
        public void Construct(
            ITimeService timeService = null,
            IPlayerStateService playerState = null,
            ILocalizationService localizationService = null,
            PlayerController playerController = null)
        {
            UnsubscribeEvents();

            _timeService = timeService;
            _playerState = playerState;
            _localizationService = localizationService;
            _playerController = playerController;

            SubscribeEvents();
            RefreshCurrentBroadcast();
            SyncSoundscapeWithPlayer();
        }

        public void ConfigureStations(IEnumerable<RadioStationSO> stations, bool startOn = true)
        {
            _stations.Clear();
            if (stations != null)
            {
                foreach (var station in stations)
                {
                    if (station != null)
                    {
                        _stations.Add(station);
                    }
                }
            }

            _currentStationIndex = 0;
            _startWithRadioOn = startOn;
            SetRadioPower(startOn);
        }

        private void Awake()
        {
            EnsureAudioSources();
            _isRadioOn = _startWithRadioOn;
            ApplyAudioSourceVolumes();
        }

        private void Start()
        {
            if (_playerController == null)
            {
                _playerController = FindAnyObjectByType<PlayerController>();
                SubscribePlayerEvents();
            }

            RefreshCurrentBroadcast();
            SyncSoundscapeWithPlayer();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                if (!_isRadioOn)
                {
                    SetRadioPower(true);
                }
                else
                {
                    NextStation();
                }
            }
        }

        public void SetRadioPower(bool powerOn)
        {
            bool changed = _isRadioOn != powerOn;
            _isRadioOn = powerOn;

            EnsureAudioSources();
            if (!_isRadioOn)
            {
                _currentProgram = default;
                _currentBroadcastCaption = string.Empty;
                if (_radioAudioSource != null && _radioAudioSource.isPlaying)
                {
                    _radioAudioSource.Stop();
                }
            }
            else
            {
                RefreshCurrentBroadcast();
            }

            if (changed)
            {
                OnRadioPowerChanged?.Invoke(_isRadioOn);
            }
        }

        public void ToggleRadio()
        {
            SetRadioPower(!_isRadioOn);
        }

        public void NextStation()
        {
            if (_stations.Count == 0) return;

            _currentStationIndex = (_currentStationIndex + 1) % _stations.Count;
            if (!_isRadioOn)
            {
                SetRadioPower(true);
            }
            else
            {
                RefreshCurrentBroadcast();
            }
        }

        public void PreviousStation()
        {
            if (_stations.Count == 0) return;

            _currentStationIndex = (_currentStationIndex - 1 + _stations.Count) % _stations.Count;
            if (!_isRadioOn)
            {
                SetRadioPower(true);
            }
            else
            {
                RefreshCurrentBroadcast();
            }
        }

        public bool TuneToStation(string stationId)
        {
            if (string.IsNullOrWhiteSpace(stationId) || _stations.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                var station = _stations[i];
                if (station != null && string.Equals(station.StationId, stationId, StringComparison.OrdinalIgnoreCase))
                {
                    _currentStationIndex = i;
                    if (!_isRadioOn)
                    {
                        SetRadioPower(true);
                    }
                    else
                    {
                        RefreshCurrentBroadcast();
                    }
                    return true;
                }
            }

            return false;
        }

        public void RefreshCurrentBroadcast()
        {
            EnsureAudioSources();

            if (!_isRadioOn || CurrentStation == null)
            {
                _currentProgram = default;
                _isCurrentProgramUnderstood = true;
                _currentBroadcastCaption = string.Empty;
                return;
            }

            int currentHour = _timeService != null ? _timeService.CurrentTime.Hour : 18;
            RadioStationSO station = CurrentStation;

            if (!station.TryGetProgramForHour(currentHour, out _currentProgram))
            {
                _currentProgram = new RadioProgramEntry(
                    "default_carrier",
                    station.StationName,
                    "...",
                    station.PrimaryLanguageId,
                    0,
                    23);
            }

            string effectiveLanguage = !string.IsNullOrWhiteSpace(_currentProgram.LanguageId)
                ? _currentProgram.LanguageId
                : station.PrimaryLanguageId;

            _isCurrentProgramUnderstood = EvaluateLanguageComprehension(effectiveLanguage);
            _currentBroadcastCaption = BuildFormattedCaption(station, _currentProgram, effectiveLanguage, _isCurrentProgramUnderstood);

            if (_radioAudioSource != null && _currentProgram.AudioClip != null)
            {
                if (_radioAudioSource.clip != _currentProgram.AudioClip)
                {
                    _radioAudioSource.clip = _currentProgram.AudioClip;
                }
            }

            OnBroadcastChanged?.Invoke(station, _currentProgram);
        }

        public void UpdateSoundscapeForContext(DriverMode driverMode, WalkableZone currentZone)
        {
            AmbientSoundscapeMode targetMode;

            if (driverMode == DriverMode.InTaxi)
            {
                targetMode = AmbientSoundscapeMode.CityStreetAndTraffic;
            }
            else if (currentZone != null && currentZone.ZoneType == WalkableZoneType.HomeInterior)
            {
                targetMode = AmbientSoundscapeMode.CozyHomeInterior;
            }
            else
            {
                targetMode = AmbientSoundscapeMode.NeighborhoodCurbside;
            }

            bool changed = _currentSoundscape != targetMode;
            _currentSoundscape = targetMode;

            ApplyAmbientClipAndDucking();

            if (changed)
            {
                OnSoundscapeChanged?.Invoke(_currentSoundscape);
            }
        }

        private bool EvaluateLanguageComprehension(string languageId)
        {
            if (string.IsNullOrWhiteSpace(languageId))
            {
                return true; // Instrumental or non-verbal broadcast
            }

            if (_playerState == null)
            {
                return string.Equals(languageId, PlayerProfileData.DefaultCommonLanguageId, StringComparison.OrdinalIgnoreCase);
            }

            return _playerState.CanUnderstandLanguage(languageId);
        }

        private string BuildFormattedCaption(
            RadioStationSO station,
            RadioProgramEntry program,
            string languageId,
            bool isUnderstood)
        {
            string rawTranscript = program.TranscriptText;
            if (string.IsNullOrEmpty(rawTranscript))
            {
                return string.Empty;
            }

            var line = new DialogueLine(
                rawTranscript,
                station.StationName,
                languageId,
                "broadcast",
                isUnderstood,
                Array.Empty<string>());

            string formatted = DialoguePresenter.FormatLineForComprehension(line);
            if (isUnderstood && _localizationService != null)
            {
                formatted = _localizationService.FormatTextForDisplay(formatted);
            }

            return formatted;
        }

        private void EnsureAudioSources()
        {
            if (_radioAudioSource == null)
            {
                var radioTransform = transform.Find("RadioAudioSource");
                if (radioTransform != null)
                {
                    _radioAudioSource = radioTransform.GetComponent<AudioSource>();
                }

                if (_radioAudioSource == null)
                {
                    var go = new GameObject("RadioAudioSource");
                    go.transform.SetParent(transform, false);
                    _radioAudioSource = go.AddComponent<AudioSource>();
                    _radioAudioSource.playOnAwake = false;
                    _radioAudioSource.loop = true;
                    _radioAudioSource.spatialBlend = 0f;
                }
            }

            if (_ambientAudioSource == null)
            {
                var ambientTransform = transform.Find("AmbientAudioSource");
                if (ambientTransform != null)
                {
                    _ambientAudioSource = ambientTransform.GetComponent<AudioSource>();
                }

                if (_ambientAudioSource == null)
                {
                    var go = new GameObject("AmbientAudioSource");
                    go.transform.SetParent(transform, false);
                    _ambientAudioSource = go.AddComponent<AudioSource>();
                    _ambientAudioSource.playOnAwake = false;
                    _ambientAudioSource.loop = true;
                    _ambientAudioSource.spatialBlend = 0f;
                }
            }
        }

        private void ApplyAudioSourceVolumes()
        {
            if (_radioAudioSource != null)
            {
                _radioAudioSource.volume = _radioVolume;
            }

            if (_ambientAudioSource != null)
            {
                float interiorScale = _currentSoundscape == AmbientSoundscapeMode.CozyHomeInterior ? 0.65f : 1.0f;
                _ambientAudioSource.volume = _ambientVolume * interiorScale;
            }
        }

        private void ApplyAmbientClipAndDucking()
        {
            EnsureAudioSources();
            ApplyAudioSourceVolumes();

            if (_ambientAudioSource == null) return;

            AudioClip targetClip = _currentSoundscape switch
            {
                AmbientSoundscapeMode.CozyHomeInterior => _cozyHomeInteriorClip,
                AmbientSoundscapeMode.NeighborhoodCurbside => _neighborhoodCurbsideClip,
                _ => _cityTrafficAmbienceClip
            };

            if (targetClip != null && _ambientAudioSource.clip != targetClip)
            {
                _ambientAudioSource.clip = targetClip;
            }
        }

        private void SubscribeEvents()
        {
            if (_timeService != null)
            {
                _timeService.OnHourTicked += HandleHourTicked;
            }

            if (_playerState != null)
            {
                _playerState.OnLanguageInstalled += HandleLanguageModuleInstalled;
            }

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged += HandleLocaleChanged;
            }

            SubscribePlayerEvents();
        }

        private void SubscribePlayerEvents()
        {
            if (_playerController != null)
            {
                _playerController.OnModeChanged -= HandlePlayerModeChanged;
                _playerController.OnModeChanged += HandlePlayerModeChanged;

                _playerController.OnInteracted -= HandlePlayerInteracted;
                _playerController.OnInteracted += HandlePlayerInteracted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_timeService != null)
            {
                _timeService.OnHourTicked -= HandleHourTicked;
            }

            if (_playerState != null)
            {
                _playerState.OnLanguageInstalled -= HandleLanguageModuleInstalled;
            }

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            }

            if (_playerController != null)
            {
                _playerController.OnModeChanged -= HandlePlayerModeChanged;
                _playerController.OnInteracted -= HandlePlayerInteracted;
            }
        }

        private void HandleHourTicked(WorldTime newTime)
        {
            if (_isRadioOn)
            {
                RefreshCurrentBroadcast();
            }
        }

        private void HandleLanguageModuleInstalled(string languageId)
        {
            if (_isRadioOn)
            {
                RefreshCurrentBroadcast();
            }
        }

        private void HandleLocaleChanged(string newLocale)
        {
            if (_isRadioOn)
            {
                RefreshCurrentBroadcast();
            }
        }

        private void HandlePlayerModeChanged(DriverMode newMode)
        {
            SyncSoundscapeWithPlayer();
        }

        private void HandlePlayerInteracted(NeighborhoodInteractable interactable)
        {
            if (interactable == null) return;

            if (interactable.Kind == InteractableKind.TranslationWorkbench)
            {
                NextStation();
            }

            SyncSoundscapeWithPlayer();
        }

        private void SyncSoundscapeWithPlayer()
        {
            if (_playerController == null) return;

            UpdateSoundscapeForContext(_playerController.CurrentMode, _playerController.CurrentWalkableZone);
        }
    }
}
