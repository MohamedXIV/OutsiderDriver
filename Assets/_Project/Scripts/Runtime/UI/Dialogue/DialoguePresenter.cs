using System;
using System.Collections.Generic;
using System.Text;
using VContainer;
using VContainer.Unity;
using OutsiderDriver.Data;
using OutsiderDriver.Dialogue;
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

        private PassengerDefinitionSO _activePassenger;

        [Inject]
        public DialoguePresenter(
            IDialogueService dialogueService,
            IDialogueView view,
            IRideLifecycleService rideService = null)
        {
            _dialogueService = dialogueService ?? throw new ArgumentNullException(nameof(dialogueService));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _rideService = rideService;
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
            _view.SetChoices(choices);
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
                _view.SetArrivalBannerVisible(true, "Arrived at destination — finish speaking whenever ready.");
            }
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
