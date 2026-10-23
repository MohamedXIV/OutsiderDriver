using System;
using System.Collections.Generic;
using OutsiderDriver.Core.State;

namespace OutsiderDriver.Dialogue
{
    /// <summary>
    /// Represents a selectable dialogue choice presented to the driver during a conversation.
    /// </summary>
    [Serializable]
    public readonly struct DialogueChoice
    {
        public readonly int Index;
        public readonly string Text;

        public DialogueChoice(int index, string text)
        {
            Index = index;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// Snapshot of a single delivered dialogue line, including parsed Ink metadata tags
    /// (e.g., #lang:Vesperian, #speaker:Lyra, #mood:warm) and whether the driver's installed
    /// translator modules can understand the spoken language.
    /// </summary>
    [Serializable]
    public sealed class DialogueLine
    {
        public string Text { get; }
        public string SpeakerId { get; }
        public string LanguageId { get; }
        public string Mood { get; }
        public bool IsUnderstoodByPlayer { get; }
        public IReadOnlyList<string> RawTags { get; }

        public DialogueLine(
            string text,
            string speakerId,
            string languageId,
            string mood,
            bool isUnderstoodByPlayer,
            IReadOnlyList<string> rawTags)
        {
            Text = text ?? string.Empty;
            SpeakerId = speakerId ?? string.Empty;
            LanguageId = string.IsNullOrWhiteSpace(languageId)
                ? PlayerProfileData.DefaultCommonLanguageId
                : languageId;
            Mood = mood ?? "neutral";
            IsUnderstoodByPlayer = isUnderstoodByPlayer;
            RawTags = rawTags ?? Array.Empty<string>();
        }
    }
}
