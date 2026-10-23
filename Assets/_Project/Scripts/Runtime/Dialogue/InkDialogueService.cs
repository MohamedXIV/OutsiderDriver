using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Ink.Runtime;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Data;

namespace OutsiderDriver.Dialogue
{
    /// <summary>
    /// Concrete Ink narrative service implementing two-way state binding (Decision P-020).
    /// Exposes external Ink functions for querying/mutating player credits, language modules,
    /// cover integrity, world clock, and separated resident relationship dimensions (Decision X-011).
    /// </summary>
    public sealed class InkDialogueService : IDialogueService
    {
        private readonly IPlayerStateService _playerStateService;
        private readonly ITimeService _timeService;

        private Story _activeStory;
        private string _currentResidentId;
        private string _defaultLanguageId = PlayerProfileData.DefaultCommonLanguageId;
        private DialogueLine _currentLine;
        private readonly List<DialogueChoice> _currentChoices = new List<DialogueChoice>();

        public bool IsConversationActive => _activeStory != null;
        public bool CanContinue => _activeStory != null && _activeStory.canContinue;
        public string CurrentResidentId => _currentResidentId;
        public DialogueLine CurrentLine => _currentLine;
        public IReadOnlyList<DialogueChoice> CurrentChoices => _currentChoices;

        public event Action<string> OnConversationStarted;
        public event Action<DialogueLine> OnLinePublished;
        public event Action<IReadOnlyList<DialogueChoice>> OnChoicesPresented;
        public event Action<string> OnConversationEnded;

        [Inject]
        public InkDialogueService(IPlayerStateService playerStateService, ITimeService timeService = null)
        {
            _playerStateService = playerStateService;
            _timeService = timeService;
        }

        public bool StartPassengerDialogue(PassengerDefinitionSO passenger, string overrideKnot = null)
        {
            if (passenger == null || passenger.InkStoryJson == null)
            {
                return false;
            }

            string knot = !string.IsNullOrWhiteSpace(overrideKnot)
                ? overrideKnot
                : passenger.DefaultEntryKnot;

            return StartDialogueFromJson(
                inkJson: passenger.InkStoryJson.text,
                residentId: passenger.PassengerId,
                defaultLanguageId: passenger.NativeLanguageId,
                entryKnot: knot);
        }

        public bool StartDialogueFromJson(
            string inkJson,
            string residentId = null,
            string defaultLanguageId = null,
            string entryKnot = null)
        {
            if (string.IsNullOrWhiteSpace(inkJson))
            {
                return false;
            }

            EndDialogue();

            try
            {
                _activeStory = new Story(inkJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InkDialogueService] Failed to parse Ink JSON: {ex.Message}");
                _activeStory = null;
                return false;
            }

            _currentResidentId = residentId ?? string.Empty;
            _defaultLanguageId = string.IsNullOrWhiteSpace(defaultLanguageId)
                ? PlayerProfileData.DefaultCommonLanguageId
                : defaultLanguageId;

            BindExternalFunctions(_activeStory);

            if (!string.IsNullOrWhiteSpace(entryKnot))
            {
                try
                {
                    _activeStory.ChoosePathString(entryKnot);
                }
                catch
                {
                    // If the specified knot does not exist in this story, start from root content
                }
            }

            OnConversationStarted?.Invoke(_currentResidentId);

            if (_activeStory.canContinue)
            {
                ContinueDialogue();
            }
            else
            {
                RefreshChoices();
            }

            return true;
        }

        public DialogueLine ContinueDialogue()
        {
            if (_activeStory == null)
            {
                return null;
            }

            if (!_activeStory.canContinue)
            {
                RefreshChoices();
                if (_currentChoices.Count == 0)
                {
                    EndDialogue();
                }
                return _currentLine;
            }

            string rawText = _activeStory.Continue()?.Trim() ?? string.Empty;
            List<string> tags = _activeStory.currentTags != null
                ? new List<string>(_activeStory.currentTags)
                : new List<string>();

            ParseLineTags(tags, out string speakerId, out string languageId, out string mood);

            bool understood = _playerStateService == null || _playerStateService.CanUnderstandLanguage(languageId);

            _currentLine = new DialogueLine(
                text: rawText,
                speakerId: speakerId,
                languageId: languageId,
                mood: mood,
                isUnderstoodByPlayer: understood,
                rawTags: tags);

            OnLinePublished?.Invoke(_currentLine);
            RefreshChoices();

            if (!_activeStory.canContinue && _currentChoices.Count == 0)
            {
                EndDialogue();
            }

            return _currentLine;
        }

        public bool SelectChoice(int choiceIndex)
        {
            if (_activeStory == null || _activeStory.currentChoices == null)
            {
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= _activeStory.currentChoices.Count)
            {
                return false;
            }

            _activeStory.ChooseChoiceIndex(choiceIndex);
            _currentChoices.Clear();

            if (_activeStory.canContinue)
            {
                ContinueDialogue();
            }
            else
            {
                EndDialogue();
            }

            return true;
        }

        public void StaySilent()
        {
            // Look for an explicit silence choice if authored; otherwise end or pause conversation gracefully
            if (_activeStory != null && _activeStory.currentChoices != null)
            {
                for (int i = 0; i < _activeStory.currentChoices.Count; i++)
                {
                    string choiceText = _activeStory.currentChoices[i].text;
                    if (!string.IsNullOrEmpty(choiceText) &&
                        (choiceText.IndexOf("[Silence]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         choiceText.IndexOf("(Silence)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         choiceText.IndexOf("Silence", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        SelectChoice(i);
                        return;
                    }
                }
            }

            EndDialogue();
        }

        public void EndDialogue()
        {
            if (_activeStory == null)
            {
                return;
            }

            string endedResident = _currentResidentId;
            _activeStory = null;
            _currentResidentId = null;
            _currentLine = null;
            _currentChoices.Clear();

            OnConversationEnded?.Invoke(endedResident);
        }

        private void RefreshChoices()
        {
            _currentChoices.Clear();
            if (_activeStory == null || _activeStory.currentChoices == null)
            {
                return;
            }

            for (int i = 0; i < _activeStory.currentChoices.Count; i++)
            {
                var choice = _activeStory.currentChoices[i];
                _currentChoices.Add(new DialogueChoice(choice.index, choice.text?.Trim() ?? string.Empty));
            }

            if (_currentChoices.Count > 0)
            {
                OnChoicesPresented?.Invoke(_currentChoices);
            }
        }

        private void ParseLineTags(
            IReadOnlyList<string> tags,
            out string speakerId,
            out string languageId,
            out string mood)
        {
            speakerId = _currentResidentId ?? string.Empty;
            languageId = _defaultLanguageId;
            mood = "neutral";

            if (tags == null) return;

            for (int i = 0; i < tags.Count; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag)) continue;

                int colonIndex = tag.IndexOf(':');
                if (colonIndex <= 0 || colonIndex >= tag.Length - 1) continue;

                string key = tag.Substring(0, colonIndex).Trim();
                string value = tag.Substring(colonIndex + 1).Trim();

                if (string.Equals(key, "speaker", StringComparison.OrdinalIgnoreCase))
                {
                    speakerId = value;
                }
                else if (string.Equals(key, "lang", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "language", StringComparison.OrdinalIgnoreCase))
                {
                    languageId = value;
                }
                else if (string.Equals(key, "mood", StringComparison.OrdinalIgnoreCase))
                {
                    mood = value;
                }
            }
        }

        private void BindExternalFunctions(Story story)
        {
            // Language comprehension check
            story.BindExternalFunction("CanUnderstandLanguage", (string langId) =>
                _playerStateService == null || _playerStateService.CanUnderstandLanguage(langId),
                lookaheadSafe: true);

            // Player Credits queries & mutations
            story.BindExternalFunction("GetCredits", () =>
                _playerStateService != null ? _playerStateService.Credits : 0,
                lookaheadSafe: true);

            story.BindExternalFunction("AddCredits", (int amount) =>
            {
                _playerStateService?.AddCredits(amount);
            }, lookaheadSafe: false);

            story.BindExternalFunction("TrySpendCredits", (int amount) =>
                _playerStateService != null && _playerStateService.TrySpendCredits(amount),
                lookaheadSafe: false);

            // Cover Integrity queries & mutations
            story.BindExternalFunction("GetCoverIntegrity", () =>
                _playerStateService != null ? _playerStateService.CoverIntegrity : 100,
                lookaheadSafe: true);

            story.BindExternalFunction("AdjustCoverIntegrity", (int delta) =>
            {
                _playerStateService?.AdjustCoverIntegrity(delta);
            }, lookaheadSafe: false);

            // World Clock hour query
            story.BindExternalFunction("GetCurrentHour", () =>
                _timeService != null ? _timeService.CurrentTime.Hour : 18,
                lookaheadSafe: true);

            // Resident Relationship queries & mutations (Decision X-011: Affinity, PersonalTrust, Suspicion kept distinct)
            story.BindExternalFunction("GetAffinity", (string residentId) =>
            {
                string targetId = ResolveResidentId(residentId);
                return _playerStateService != null && !string.IsNullOrWhiteSpace(targetId)
                    ? _playerStateService.GetOrAddResident(targetId).Affinity
                    : 0;
            }, lookaheadSafe: true);

            story.BindExternalFunction("GetTrust", (string residentId) =>
            {
                string targetId = ResolveResidentId(residentId);
                return _playerStateService != null && !string.IsNullOrWhiteSpace(targetId)
                    ? _playerStateService.GetOrAddResident(targetId).PersonalTrust
                    : 0;
            }, lookaheadSafe: true);

            story.BindExternalFunction("GetSuspicion", (string residentId) =>
            {
                string targetId = ResolveResidentId(residentId);
                return _playerStateService != null && !string.IsNullOrWhiteSpace(targetId)
                    ? _playerStateService.GetOrAddResident(targetId).SuspicionOfHumanity
                    : 0;
            }, lookaheadSafe: true);

            story.BindExternalFunction("RecordInteraction", (string residentId, int affinityDelta, int trustDelta, int suspicionDelta) =>
            {
                string targetId = ResolveResidentId(residentId);
                if (_playerStateService != null && !string.IsNullOrWhiteSpace(targetId))
                {
                    int totalMins = _timeService != null ? _timeService.CurrentTime.TotalMinutes : 0;
                    _playerStateService.RecordResidentInteraction(targetId, affinityDelta, trustDelta, suspicionDelta, totalMins);
                }
            }, lookaheadSafe: false);
        }

        private string ResolveResidentId(string providedId)
        {
            return !string.IsNullOrWhiteSpace(providedId) ? providedId : _currentResidentId;
        }
    }
}
