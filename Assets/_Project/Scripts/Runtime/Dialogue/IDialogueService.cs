using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Data;

namespace OutsiderDriver.Dialogue
{
    /// <summary>
    /// Authoritative domain service hosting the Ink narrative engine (Decision P-020).
    /// Provides two-way state binding with PlayerStateService and TimeService, and emits
    /// events for UI Toolkit Presenters without blocking vehicle travel.
    /// </summary>
    public interface IDialogueService
    {
        bool IsConversationActive { get; }
        bool CanContinue { get; }
        string CurrentResidentId { get; }
        DialogueLine CurrentLine { get; }
        IReadOnlyList<DialogueChoice> CurrentChoices { get; }

        event Action<string> OnConversationStarted;
        event Action<DialogueLine> OnLinePublished;
        event Action<IReadOnlyList<DialogueChoice>> OnChoicesPresented;
        event Action<string> OnConversationEnded;

        /// <summary>
        /// Starts a conversation using the passenger's compiled Ink story asset and default entry knot.
        /// </summary>
        bool StartPassengerDialogue(PassengerDefinitionSO passenger, string overrideKnot = null);

        /// <summary>
        /// Starts a conversation directly from a compiled Ink JSON string.
        /// </summary>
        bool StartDialogueFromJson(
            string inkJson,
            string residentId = null,
            string defaultLanguageId = null,
            string entryKnot = null);

        /// <summary>
        /// Advances the active Ink story by one line and publishes the resulting DialogueLine and choices.
        /// </summary>
        DialogueLine ContinueDialogue();

        /// <summary>
        /// Selects a branching choice by index and automatically advances to the next line.
        /// </summary>
        bool SelectChoice(int choiceIndex);

        /// <summary>
        /// Chooses to remain silent when supported by the conversation (GAME_DESIGN §1 & §3.B).
        /// </summary>
        void StaySilent();

        /// <summary>
        /// Ends the active conversation cleanly.
        /// </summary>
        void EndDialogue();
    }
}
