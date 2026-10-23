using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace OutsiderDriver.Visuals.Portraits
{
    /// <summary>
    /// Authored configuration asset for a character's layered 2D portrait rig
    /// using Unity 2D Animation bones and SpriteLibraryAsset / SpriteResolver
    /// (Systems Architecture §8, Visual Direction §4).
    /// </summary>
    [CreateAssetMenu(fileName = "PortraitRigConfig_", menuName = "Outsider Driver/Visuals/Character Portrait Rig Config")]
    public class CharacterPortraitRigConfigSO : ScriptableObject
    {
        public const string CategoryBaseBody = "BaseBody";
        public const string CategoryEyes = "Eyes";
        public const string CategoryMouth = "Mouth";
        public const string CategoryEmotion = "Emotion";

        public const string LabelTorso = "torso";
        public const string LabelHead = "head";
        public const string LabelTalkingMouth = "talking_open";

        [Header("Character Identity")]
        [SerializeField] private string _characterId = "res_default";
        [SerializeField] private SpriteLibraryAsset _spriteLibraryAsset;

        [Header("Base Layered Sprites")]
        [SerializeField] private Sprite _baseTorsoSprite;
        [SerializeField] private Sprite _baseHeadSprite;
        [SerializeField] private Sprite _talkingOpenMouthSprite;

        [Header("2D Bone Idle & Phoneme Dynamics")]
        [Min(0f)]
        [SerializeField] private float _breathingAmplitude = 0.035f;
        [Min(0.1f)]
        [SerializeField] private float _breathingFrequency = 1.6f;
        [Min(0.5f)]
        [SerializeField] private float _headTiltSmoothingSpeed = 8f;
        [Min(1f)]
        [SerializeField] private float _talkingPhonemeFrequency = 8f;

        [Header("Mood Expressions")]
        [SerializeField] private List<PortraitExpressionEntry> _expressions = new List<PortraitExpressionEntry>();

        public string CharacterId => _characterId;
        public SpriteLibraryAsset SpriteLibraryAsset => _spriteLibraryAsset;
        public Sprite BaseTorsoSprite => _baseTorsoSprite;
        public Sprite BaseHeadSprite => _baseHeadSprite;
        public Sprite TalkingOpenMouthSprite => _talkingOpenMouthSprite;
        public float BreathingAmplitude => _breathingAmplitude;
        public float BreathingFrequency => _breathingFrequency;
        public float HeadTiltSmoothingSpeed => _headTiltSmoothingSpeed;
        public float TalkingPhonemeFrequency => _talkingPhonemeFrequency;
        public IReadOnlyList<PortraitExpressionEntry> Expressions => _expressions;

        public void Configure(
            string characterId,
            Sprite baseTorsoSprite,
            Sprite baseHeadSprite,
            Sprite talkingOpenMouthSprite = null,
            IEnumerable<PortraitExpressionEntry> expressions = null,
            float breathingAmplitude = 0.035f,
            float breathingFrequency = 1.6f,
            float headTiltSmoothingSpeed = 8f,
            float talkingPhonemeFrequency = 8f)
        {
            _characterId = characterId ?? "res_default";
            _baseTorsoSprite = baseTorsoSprite;
            _baseHeadSprite = baseHeadSprite;
            _talkingOpenMouthSprite = talkingOpenMouthSprite;
            _breathingAmplitude = Mathf.Max(0f, breathingAmplitude);
            _breathingFrequency = Mathf.Max(0.1f, breathingFrequency);
            _headTiltSmoothingSpeed = Mathf.Max(0.5f, headTiltSmoothingSpeed);
            _talkingPhonemeFrequency = Mathf.Max(1f, talkingPhonemeFrequency);

            _expressions.Clear();
            if (expressions != null)
            {
                _expressions.AddRange(expressions);
            }

            RebuildSpriteLibraryAsset();
        }

        /// <summary>
        /// Builds or updates the native Unity 2D Animation SpriteLibraryAsset with categories
        /// ('BaseBody', 'Eyes', 'Mouth', 'Emotion') populated from this config's layered sprites.
        /// </summary>
        public SpriteLibraryAsset RebuildSpriteLibraryAsset()
        {
            if (_spriteLibraryAsset == null)
            {
                _spriteLibraryAsset = CreateInstance<SpriteLibraryAsset>();
                _spriteLibraryAsset.name = $"SpriteLib_{_characterId}";
            }

            if (_baseTorsoSprite != null)
            {
                _spriteLibraryAsset.AddCategoryLabel(_baseTorsoSprite, CategoryBaseBody, LabelTorso);
            }

            if (_baseHeadSprite != null)
            {
                _spriteLibraryAsset.AddCategoryLabel(_baseHeadSprite, CategoryBaseBody, LabelHead);
            }

            if (_talkingOpenMouthSprite != null)
            {
                _spriteLibraryAsset.AddCategoryLabel(_talkingOpenMouthSprite, CategoryMouth, LabelTalkingMouth);
            }

            for (int i = 0; i < _expressions.Count; i++)
            {
                var exp = _expressions[i];
                if (exp.EyesSprite != null && !string.IsNullOrWhiteSpace(exp.EyesLabel))
                {
                    _spriteLibraryAsset.AddCategoryLabel(exp.EyesSprite, CategoryEyes, exp.EyesLabel);
                }

                if (exp.MouthSprite != null && !string.IsNullOrWhiteSpace(exp.MouthLabel))
                {
                    _spriteLibraryAsset.AddCategoryLabel(exp.MouthSprite, CategoryMouth, exp.MouthLabel);
                }

                if (exp.EmotionOverlaySprite != null && !string.IsNullOrWhiteSpace(exp.EmotionOverlayLabel))
                {
                    _spriteLibraryAsset.AddCategoryLabel(exp.EmotionOverlaySprite, CategoryEmotion, exp.EmotionOverlayLabel);
                }
            }

            return _spriteLibraryAsset;
        }

        /// <summary>
        /// Resolves the expression entry matching the requested mood ID, falling back to 'neutral' or the first entry.
        /// </summary>
        public bool TryGetExpression(string moodId, out PortraitExpressionEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(moodId))
            {
                for (int i = 0; i < _expressions.Count; i++)
                {
                    if (string.Equals(_expressions[i].MoodId, moodId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        entry = _expressions[i];
                        return true;
                    }
                }
            }

            for (int i = 0; i < _expressions.Count; i++)
            {
                if (string.Equals(_expressions[i].MoodId, "neutral", StringComparison.OrdinalIgnoreCase))
                {
                    entry = _expressions[i];
                    return true;
                }
            }

            if (_expressions.Count > 0)
            {
                entry = _expressions[0];
                return true;
            }

            entry = new PortraitExpressionEntry("neutral", "neutral", "neutral", "none", 0f);
            return false;
        }
    }
}
