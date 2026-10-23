using System;
using UnityEngine;

namespace OutsiderDriver.Audio
{
    /// <summary>
    /// Defines a scheduled broadcast segment or musical program on an alien radio station
    /// (GAME_DESIGN §5 "Radio and information").
    /// </summary>
    [Serializable]
    public struct RadioProgramEntry
    {
        [Tooltip("Unique identifier for this broadcast segment.")]
        [SerializeField] private string _programId;

        [Tooltip("Display title of the program or music track.")]
        [SerializeField] private string _title;

        [Tooltip("Spoken broadcast snippet or atmospheric description shown in the radio caption.")]
        [TextArea(2, 4)]
        [SerializeField] private string _transcriptText;

        [Tooltip("Language ID spoken during this program (e.g., 'Common', 'Local', 'TradeCant'). Empty means instrumental/universal.")]
        [SerializeField] private string _languageId;

        [Tooltip("Optional AudioClip played when tuned to this program.")]
        [SerializeField] private AudioClip _audioClip;

        [Range(0, 23)]
        [SerializeField] private int _startHour;

        [Range(0, 23)]
        [SerializeField] private int _endHour;

        public string ProgramId => _programId;
        public string Title => _title;
        public string TranscriptText => _transcriptText;
        public string LanguageId => _languageId;
        public AudioClip AudioClip => _audioClip;
        public int StartHour => _startHour;
        public int EndHour => _endHour;

        public RadioProgramEntry(
            string programId,
            string title,
            string transcriptText,
            string languageId,
            int startHour = 0,
            int endHour = 23,
            AudioClip audioClip = null)
        {
            _programId = programId ?? string.Empty;
            _title = title ?? string.Empty;
            _transcriptText = transcriptText ?? string.Empty;
            _languageId = languageId ?? string.Empty;
            _startHour = Mathf.Clamp(startHour, 0, 23);
            _endHour = Mathf.Clamp(endHour, 0, 23);
            _audioClip = audioClip;
        }

        /// <summary>
        /// Evaluates whether this broadcast program is scheduled during the given hour (0..23).
        /// Supports both daytime windows (start <= end) and overnight windows (start > end).
        /// </summary>
        public bool IsActiveAtHour(int hour)
        {
            int normalizedHour = ((hour % 24) + 24) % 24;
            if (_startHour <= _endHour)
            {
                return normalizedHour >= _startHour && normalizedHour <= _endHour;
            }

            return normalizedHour >= _startHour || normalizedHour <= _endHour;
        }
    }
}
