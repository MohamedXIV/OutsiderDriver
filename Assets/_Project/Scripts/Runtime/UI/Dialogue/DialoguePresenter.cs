using System;
using System.Collections.Generic;
using System.Text;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Data;
using OutsiderDriver.Dialogue;
using OutsiderDriver.Localization;
using OutsiderDriver.Rides;

namespace OutsiderDriver.UI.Dialogue
{
    /// <summary>
    /// Event-driven MVP Presenter coordinating IDialogueService, IRideLifecycleService, and IDialogueView.
    /// Ensures non-blocking conversational rides: reaching the destination displays a gentle arrival
    /// notice without abruptly cutting off an active sentence (GAME_DESIGN §3.B, Decision P-019).
    /// </summary>
    public sealed class DialoguePresenter : IStartable, IDisposable
    {
        private const string AlienGlyphAlphabet = "◊∆¤§‡†¶•ªº«»¿¡";

        private readonly IDialogueService _dialogueService;
        private readonly IDialogueView _view;
        private readonly IRideLifecycleService _rideService;
        private readonly ILocalizationService _localizationService;

        private PassengerDefinitionSO _activePassenger;

        [Inject]
        public DialoguePresenter(
            IDialogueService dialogueService,
            IDialogueView view,
            IRideLifecycleService rideService = null,
            ILocalizationService localizationService = null)
        {
            _dialogueService = dialogueService ?? throw new ArgumentNullException(nameof(dialogueService));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _rideService = rideService;
            _localizationService = localizationService;
        }

        public void Start()
        {
            _dialogueService.OnConversationStarted += HandleConversationStarted;
            _dialogueService.OnLinePublished += HandleLinePublished;
            _dialogueService.OnChoicesPresented += HandleChoicesPresented;
            _dialogueService.OnConversationEnded += HandleConversationEnded;

            _view.OnContinueClicked += HandleContinueClicked;
            _view.OnStaySilentClicked += HandleStaySilentClicked;
            _view.OnChoiceSelected += HandleChoiceSelected;

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged += HandleLocaleChanged;
                UpdateViewLocalization();
            }

            if (_rideService != null)
            {
                _rideService.OnPassengerBoarded += HandlePassengerBoarded;
                _rideService.OnArrivedAtDestination += HandleArrivedAtDestination;
                _rideService.OnRideCompleted += HandleRideCompleted;
                _rideService.OnRideCancelled += HandleRideCancelled;
            }
        }

        public void Dispose()
        {
            _dialogueService.OnConversationStarted -= HandleConversationStarted;
            _dialogueService.OnLinePublished -= HandleLinePublished;
            _dialogueService.OnChoicesPresented -= HandleChoicesPresented;
            _dialogueService.OnConversationEnded -= HandleConversationEnded;

            _view.OnContinueClicked -= HandleContinueClicked;
            _view.OnStaySilentClicked -= HandleStaySilentClicked;
            _view.OnChoiceSelected -= HandleChoiceSelected;

            if (_localizationService != null)
            {
                _localizationService.OnLocaleChanged -= HandleLocaleChanged;
            }

            if (_rideService != null)
            {
                _rideService.OnPassengerBoarded -= HandlePassengerBoarded;
                _rideService.OnArrivedAtDestination -= HandleArrivedAtDestination;
                _rideService.OnRideCompleted -= HandleRideCompleted;
                _rideService.OnRideCancelled -= HandleRideCancelled;
            }
        }

        /// <summary>
        /// Deterministic helper to obfuscate untranslated alien dialogue when the driver lacks
        /// the corresponding language chip (Decision H-009 & Systems Architecture §7).
        /// Preserves punctuation and spacing so speech cadence remains readable.
        /// </summary>
        public static string FormatLineForComprehension(DialogueLine line)
        {
            if (line == null) return string.Empty;
            if (line.IsUnderstoodByPlayer || string.IsNullOrEmpty(line.Text))
            {
                return line.Text;
            }

            var sb = new StringBuilder(line.Text.Length);
            for (int i = 0; i < line.Text.Length; i++)
            {
                char c = line.Text[i];
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c))
                {
                    sb.Append(c);
                }
                else
                {
                    int glyphIndex = Math.Abs(c + i * 7) % AlienGlyphAlphabet.Length;
                    sb.Append(AlienGlyphAlphabet[glyphIndex]);
                }
            }
            return sb.ToString();
        }

        private void HandleConversationStarted(string residentId)
        {
            _view.ClearView();
            _view.SetVisible(true);

            if (_activePassenger != null && _activePassenger.PortraitSprite != null)
            {
                _view.SetPortrait(_activePassenger.PortraitSprite);
            }
        }

        private void HandleLinePublished(DialogueLine line)
        {
            if (line == null) return;

            string displayName = _activePassenger != null && !string.IsNullOrWhiteSpace(_activePassenger.DisplayName)
                ? _activePassenger.DisplayName
                : line.SpeakerId;

            string formattedText = FormatLineForComprehension(line);
            if (line.IsUnderstoodByPlayer && _localizationService != null)
            {
                formattedText = _localizationService.FormatTextForDisplay(formattedText);
                displayName = _localizationService.FormatTextForDisplay(displayName);
            }

            _view.SetSpeakerInfo(displayName, line.Mood, line.LanguageId, line.IsUnderstoodByPlayer);
            _view.SetDialogueText(formattedText, line.IsUnderstoodByPlayer);
            _view.SetContinueButtonVisible(_dialogueService.CanContinue);

            if (_dialogueService.CurrentChoices.Count == 0)
            {
                _view.SetChoices(Array.Empty<DialogueChoice>());
            }
        }

        private void HandleChoicesPresented(IReadOnlyList<DialogueChoice> choices)
        {
            if (_localizationService != null && choices != null && choices.Count > 0)
            {
                var localizedChoices = new List<DialogueChoice>(choices.Count);
                for (int i = 0; i < choices.Count; i++)
                {
                    var c = choices[i];
                    string formatted = _localizationService.FormatTextForDisplay(c.Text);
                    localizedChoices.Add(new DialogueChoice(c.Index, formatted));
                }
                _view.SetChoices(localizedChoices);
            }
            else
            {
                _view.SetChoices(choices);
            }

            _view.SetContinueButtonVisible(false);
        }

        private void HandleConversationEnded(string residentId)
        {
            _view.ClearView();
            _view.SetVisible(false);
        }

        private void HandleContinueClicked()
        {
            if (_dialogueService.IsConversationActive)
            {
                _dialogueService.ContinueDialogue();
            }
        }

        private void HandleStaySilentClicked()
        {
            if (_dialogueService.IsConversationActive)
            {
                _dialogueService.StaySilent();
            }
        }

        private void HandleChoiceSelected(int choiceIndex)
        {
            if (_dialogueService.IsConversationActive)
            {
                _dialogueService.SelectChoice(choiceIndex);
            }
        }

        private void HandlePassengerBoarded(RideRequest request)
        {
            _activePassenger = request?.Passenger;
            if (_activePassenger != null && _activePassenger.InkStoryJson != null)
            {
                _dialogueService.StartPassengerDialogue(_activePassenger);
            }
        }

        private void HandleArrivedAtDestination(RideRequest request)
        {
            // GAME_DESIGN §3.B: Reaching the destination produces a comfortable narrative transition,
            // not silently cutting off an active sentence.
            if (_dialogueService.IsConversationActive && _view.IsVisible)
            {
                string msg = _localizationService != null
                    ? _localizationService.GetLocalizedString(LocalizationService.TableUI, "hud_arrival_banner")
                    : "Arrived at destination — finish speaking whenever ready.";
                _view.SetArrivalBannerVisible(true, msg);
            }
        }

        private void HandleLocaleChanged(string newLocale)
        {
            UpdateViewLocalization();
        }

        private void UpdateViewLocalization()
        {
            if (_localizationService == null) return;
            string continueText = _localizationService.GetLocalizedString(LocalizationService.TableUI, "ui_continue");
            string silentText = $"[{_localizationService.GetLocalizedString(LocalizationService.TableUI, "ui_stay_silent")}]";
            _view.SetButtonLabels(continueText, silentText);
            _view.ApplyTextDirection(_localizationService.IsRtl);
        }

        private void HandleRideCompleted(RideSettlementResult settlement)
        {
            _activePassenger = null;
            if (_dialogueService.IsConversationActive)
            {
                _dialogueService.EndDialogue();
            }
        }

        private void HandleRideCancelled(RideRequest request)
        {
            _activePassenger = null;
            if (_dialogueService.IsConversationActive)
            {
                _dialogueService.EndDialogue();
            }
        }
    }
}
