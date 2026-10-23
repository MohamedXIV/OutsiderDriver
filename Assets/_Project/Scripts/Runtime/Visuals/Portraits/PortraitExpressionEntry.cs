using System;
using UnityEngine;

namespace OutsiderDriver.Visuals.Portraits
{
    /// <summary>
    /// Maps a dialogue mood tag (e.g., 'neutral', 'calm', 'happy', 'curious', 'tense')
    /// to SpriteResolver labels, optional direct sprites, and subtle 2D bone head-tilt angle
    /// (Systems Architecture §8, Visual Direction §4).
    /// </summary>
    [Serializable]
    public struct PortraitExpressionEntry
    {
        [Tooltip("Mood identifier matching Ink #mood tags (e.g., 'neutral', 'calm', 'happy', 'curious', 'tense').")]
        [SerializeField] private string _moodId;

        [Tooltip("SpriteResolver label in the 'Eyes' category.")]
        [SerializeField] private string _eyesLabel;

        [Tooltip("SpriteResolver label in the 'Mouth' category.")]
        [SerializeField] private string _mouthLabel;

        [Tooltip("SpriteResolver label in the 'Emotion' category.")]
        [SerializeField] private string _emotionOverlayLabel;

        [Tooltip("Target head-tilt angle in degrees applied to the NeckHeadBone for this mood.")]
        [Range(-25f, 25f)]
        [SerializeField] private float _headTiltDegrees;

        [Header("Sprites for SpriteLibrary / Direct Fallback")]
        [SerializeField] private Sprite _eyesSprite;
        [SerializeField] private Sprite _mouthSprite;
        [SerializeField] private Sprite _emotionOverlaySprite;

        public string MoodId => _moodId;
        public string EyesLabel => _eyesLabel;
        public string MouthLabel => _mouthLabel;
        public string EmotionOverlayLabel => _emotionOverlayLabel;
        public float HeadTiltDegrees => _headTiltDegrees;
        public Sprite EyesSprite => _eyesSprite;
        public Sprite MouthSprite => _mouthSprite;
        public Sprite EmotionOverlaySprite => _emotionOverlaySprite;

        public PortraitExpressionEntry(
            string moodId,
            string eyesLabel,
            string mouthLabel,
            string emotionOverlayLabel = "none",
            float headTiltDegrees = 0f,
            Sprite eyesSprite = null,
            Sprite mouthSprite = null,
            Sprite emotionOverlaySprite = null)
        {
            _moodId = moodId ?? "neutral";
            _eyesLabel = string.IsNullOrWhiteSpace(eyesLabel) ? _moodId : eyesLabel;
            _mouthLabel = string.IsNullOrWhiteSpace(mouthLabel) ? _moodId : mouthLabel;
            _emotionOverlayLabel = string.IsNullOrWhiteSpace(emotionOverlayLabel) ? "none" : emotionOverlayLabel;
            _headTiltDegrees = Mathf.Clamp(headTiltDegrees, -25f, 25f);
            _eyesSprite = eyesSprite;
            _mouthSprite = mouthSprite;
            _emotionOverlaySprite = emotionOverlaySprite;
        }
    }
}
