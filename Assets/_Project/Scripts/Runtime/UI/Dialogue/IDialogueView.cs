using System;
using System.Collections.Generic;
using UnityEngine;
using OutsiderDriver.Dialogue;

namespace OutsiderDriver.UI.Dialogue
{
    /// <summary>
    /// Passive View contract for the non-blocking passenger dialogue UI (Systems Architecture §6).
    /// Contains zero gameplay logic; only renders visual state pushed by DialoguePresenter
    /// and forwards user interaction events.
    /// </summary>
    public interface IDialogueView
    {
        bool IsVisible { get; }

        event Action OnContinueClicked;
        event Action OnStaySilentClicked;
        event Action<int> OnChoiceSelected;

        void SetVisible(bool visible);
        void SetSpeakerInfo(string speakerName, string mood, string languageLabel, bool isUnderstood);
        void SetPortrait(Sprite portraitSprite);
        void SetDialogueText(string formattedText, bool isUnderstood);
        void SetContinueButtonVisible(bool visible);
        void SetChoices(IReadOnlyList<DialogueChoice> choices);
        void SetArrivalBannerVisible(bool visible, string message = null);
        void ClearView();
    }
}
