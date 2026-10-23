using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace OutsiderDriver.Visuals.Portraits
{
    /// <summary>
    /// Layered 2D character portrait rig driven by Unity 2D Animation bones (TorsoBone -> NeckHeadBone),
    /// SpriteLibrary, SpriteResolver, and SpriteSkin components (Systems Architecture §8, Visual Direction §4).
    /// Supports mood expression swapping, subtle idle breathing, bone head-tilt, and talking mouth phonemes.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteLibrary))]
    public class CharacterPortraitRig : MonoBehaviour
    {
        public const int TorsoSortingOrder = 10;
        public const int HeadSortingOrder = 11;
        public const int EyesSortingOrder = 12;
        public const int MouthSortingOrder = 13;
        public const int EmotionSortingOrder = 14;

        [Header("Rig Configuration")]
        [SerializeField] private CharacterPortraitRigConfigSO _rigConfig;
        [SerializeField] private string _initialMood = "neutral";

        [Header("2D Animation Bones")]
        [SerializeField] private Transform _rootBone;
        [SerializeField] private Transform _torsoBone;
        [SerializeField] private Transform _neckHeadBone;

        [Header("Layered Sprite Renderers & Resolvers")]
        [SerializeField] private SpriteLibrary _spriteLibrary;
        [SerializeField] private SpriteRenderer _torsoRenderer;
        [SerializeField] private SpriteResolver _torsoResolver;
        [SerializeField] private SpriteSkin _torsoSkin;

        [SerializeField] private SpriteRenderer _headRenderer;
        [SerializeField] private SpriteResolver _headResolver;
        [SerializeField] private SpriteSkin _headSkin;

        [SerializeField] private SpriteRenderer _eyesRenderer;
        [SerializeField] private SpriteResolver _eyesResolver;

        [SerializeField] private SpriteRenderer _mouthRenderer;
        [SerializeField] private SpriteResolver _mouthResolver;

        [SerializeField] private SpriteRenderer _emotionOverlayRenderer;
        [SerializeField] private SpriteResolver _emotionOverlayResolver;

        private string _currentMood = "neutral";
        private PortraitExpressionEntry _activeExpression;
        private bool _isTalking;
        private bool _isMouthOpenFrame;
        private float _breathingTime;
        private float _talkingTime;
        private float _currentHeadTiltDegrees;
        private float _targetHeadTiltDegrees;
        private Vector3 _torsoRestLocalPosition = Vector3.zero;
        private Vector3 _headRestLocalPosition = new Vector3(0f, 0.45f, 0f);

        public CharacterPortraitRigConfigSO RigConfig => _rigConfig;
        public string CurrentMood => _currentMood;
        public bool IsTalking => _isTalking;
        public bool IsMouthOpenFrame => _isMouthOpenFrame;
        public float CurrentHeadTiltDegrees => _currentHeadTiltDegrees;
        public float TargetHeadTiltDegrees => _targetHeadTiltDegrees;

        public Transform RootBone => _rootBone;
        public Transform TorsoBone => _torsoBone;
        public Transform NeckHeadBone => _neckHeadBone;

        public SpriteLibrary SpriteLibrary => _spriteLibrary;
        public SpriteRenderer TorsoRenderer => _torsoRenderer;
        public SpriteResolver TorsoResolver => _torsoResolver;
        public SpriteSkin TorsoSkin => _torsoSkin;

        public SpriteRenderer HeadRenderer => _headRenderer;
        public SpriteResolver HeadResolver => _headResolver;
        public SpriteSkin HeadSkin => _headSkin;

        public SpriteRenderer EyesRenderer => _eyesRenderer;
        public SpriteResolver EyesResolver => _eyesResolver;

        public SpriteRenderer MouthRenderer => _mouthRenderer;
        public SpriteResolver MouthResolver => _mouthResolver;

        public SpriteRenderer EmotionOverlayRenderer => _emotionOverlayRenderer;
        public SpriteResolver EmotionOverlayResolver => _emotionOverlayResolver;

        public event Action<string> OnMoodChanged;
        public event Action<bool> OnTalkingStateChanged;

        private void Awake()
        {
            EnsureRigHierarchy();
            if (_rigConfig != null)
            {
                ApplyRigConfig(_rigConfig);
            }
            SetMood(_initialMood);
        }

        private void Update()
        {
            TickAnimation(Time.deltaTime);
        }

        /// <summary>
        /// Ensures the 2D bone hierarchy (RootBone -> TorsoBone -> NeckHeadBone), SpriteLibrary,
        /// SpriteSkin, and layered SpriteRenderer + SpriteResolver slots are constructed and wired.
        /// </summary>
        public void EnsureRigHierarchy()
        {
            _rootBone = transform;

            if (_spriteLibrary == null)
            {
                _spriteLibrary = GetComponent<SpriteLibrary>();
                if (_spriteLibrary == null)
                {
                    _spriteLibrary = gameObject.AddComponent<SpriteLibrary>();
                }
            }

            if (_torsoBone == null)
            {
                var existingTorso = transform.Find("TorsoBone");
                if (existingTorso != null)
                {
                    _torsoBone = existingTorso;
                }
                else
                {
                    var torsoGo = new GameObject("TorsoBone");
                    torsoGo.transform.SetParent(transform, false);
                    torsoGo.transform.localPosition = _torsoRestLocalPosition;
                    _torsoBone = torsoGo.transform;
                }
            }

            if (_neckHeadBone == null)
            {
                var existingHead = _torsoBone.Find("NeckHeadBone");
                if (existingHead != null)
                {
                    _neckHeadBone = existingHead;
                }
                else
                {
                    var headGo = new GameObject("NeckHeadBone");
                    headGo.transform.SetParent(_torsoBone, false);
                    headGo.transform.localPosition = _headRestLocalPosition;
                    _neckHeadBone = headGo.transform;
                }
            }

            EnsureLayerSlot(
                _torsoBone,
                "Layer_BaseTorso",
                TorsoSortingOrder,
                Vector3.zero,
                ref _torsoRenderer,
                ref _torsoResolver);

            if (_torsoSkin == null && _torsoRenderer != null)
            {
                _torsoSkin = _torsoRenderer.GetComponent<SpriteSkin>();
                if (_torsoSkin == null)
                {
                    _torsoSkin = _torsoRenderer.gameObject.AddComponent<SpriteSkin>();
                }
            }

            EnsureLayerSlot(
                _neckHeadBone,
                "Layer_BaseHead",
                HeadSortingOrder,
                Vector3.zero,
                ref _headRenderer,
                ref _headResolver);

            if (_headSkin == null && _headRenderer != null)
            {
                _headSkin = _headRenderer.GetComponent<SpriteSkin>();
                if (_headSkin == null)
                {
                    _headSkin = _headRenderer.gameObject.AddComponent<SpriteSkin>();
                }
            }

            EnsureLayerSlot(
                _neckHeadBone,
                "Layer_Eyes",
                EyesSortingOrder,
                new Vector3(0f, 0.06f, 0f),
                ref _eyesRenderer,
                ref _eyesResolver);

            EnsureLayerSlot(
                _neckHeadBone,
                "Layer_Mouth",
                MouthSortingOrder,
                new Vector3(0f, -0.1f, 0f),
                ref _mouthRenderer,
                ref _mouthResolver);

            EnsureLayerSlot(
                _neckHeadBone,
                "Layer_EmotionOverlay",
                EmotionSortingOrder,
                new Vector3(0.22f, 0.2f, 0f),
                ref _emotionOverlayRenderer,
                ref _emotionOverlayResolver);
        }

        /// <summary>
        /// Binds a CharacterPortraitRigConfigSO asset, assigns its SpriteLibraryAsset,
        /// resolves base torso/head sprites, and applies the initial mood expression.
        /// </summary>
        public void ApplyRigConfig(CharacterPortraitRigConfigSO config)
        {
            EnsureRigHierarchy();
            _rigConfig = config;

            if (_rigConfig == null) return;

            var libAsset = _rigConfig.SpriteLibraryAsset != null
                ? _rigConfig.SpriteLibraryAsset
                : _rigConfig.RebuildSpriteLibraryAsset();

            _spriteLibrary.spriteLibraryAsset = libAsset;

            ResolveSlot(
                _torsoResolver,
                _torsoRenderer,
                CharacterPortraitRigConfigSO.CategoryBaseBody,
                CharacterPortraitRigConfigSO.LabelTorso,
                _rigConfig.BaseTorsoSprite);

            ResolveSlot(
                _headResolver,
                _headRenderer,
                CharacterPortraitRigConfigSO.CategoryBaseBody,
                CharacterPortraitRigConfigSO.LabelHead,
                _rigConfig.BaseHeadSprite);

            SetMood(_currentMood);
        }

        /// <summary>
        /// Switches the portrait's expression layers (eyes, mouth, emotion indicator) and target bone head-tilt angle.
        /// </summary>
        public void SetMood(string moodId)
        {
            EnsureRigHierarchy();

            string normalizedMood = string.IsNullOrWhiteSpace(moodId) ? "neutral" : moodId.Trim();
            bool moodChanged = !string.Equals(_currentMood, normalizedMood, StringComparison.OrdinalIgnoreCase);
            _currentMood = normalizedMood;

            if (_rigConfig != null && _rigConfig.TryGetExpression(_currentMood, out _activeExpression))
            {
                _targetHeadTiltDegrees = _activeExpression.HeadTiltDegrees;

                ResolveSlot(
                    _eyesResolver,
                    _eyesRenderer,
                    CharacterPortraitRigConfigSO.CategoryEyes,
                    _activeExpression.EyesLabel,
                    _activeExpression.EyesSprite);

                ApplyMouthState(_isTalking && _isMouthOpenFrame);

                ResolveSlot(
                    _emotionOverlayResolver,
                    _emotionOverlayRenderer,
                    CharacterPortraitRigConfigSO.CategoryEmotion,
                    _activeExpression.EmotionOverlayLabel,
                    _activeExpression.EmotionOverlaySprite);

                if (_emotionOverlayRenderer != null)
                {
                    bool hasEmotionSprite = _emotionOverlayRenderer.sprite != null &&
                                            !string.Equals(_activeExpression.EmotionOverlayLabel, "none", StringComparison.OrdinalIgnoreCase);
                    _emotionOverlayRenderer.enabled = hasEmotionSprite;
                }
            }
            else
            {
                _targetHeadTiltDegrees = 0f;
            }

            if (moodChanged)
            {
                OnMoodChanged?.Invoke(_currentMood);
            }
        }

        /// <summary>
        /// Enables or disables mouth phoneme oscillation while a line is being spoken.
        /// </summary>
        public void SetTalking(bool isTalking)
        {
            EnsureRigHierarchy();

            bool changed = _isTalking != isTalking;
            _isTalking = isTalking;
            _talkingTime = 0f;
            _isMouthOpenFrame = isTalking;

            ApplyMouthState(_isTalking && _isMouthOpenFrame);

            if (changed)
            {
                OnTalkingStateChanged?.Invoke(_isTalking);
            }
        }

        /// <summary>
        /// Advances 2D bone idle breathing, smooth head-tilt rotation, and mouth phoneme alternation by deltaTime.
        /// Publicly callable for deterministic EditMode/PlayMode testing.
        /// </summary>
        public void TickAnimation(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            EnsureRigHierarchy();

            float breathAmp = _rigConfig != null ? _rigConfig.BreathingAmplitude : 0.035f;
            float breathFreq = _rigConfig != null ? _rigConfig.BreathingFrequency : 1.6f;
            float tiltSpeed = _rigConfig != null ? _rigConfig.HeadTiltSmoothingSpeed : 8f;
            float phonemeFreq = _rigConfig != null ? _rigConfig.TalkingPhonemeFrequency : 8f;

            // 1. Subtle idle breathing on TorsoBone
            _breathingTime += deltaTime;
            float verticalOffset = Mathf.Sin(_breathingTime * breathFreq * Mathf.PI * 2f) * breathAmp;
            _torsoBone.localPosition = _torsoRestLocalPosition + new Vector3(0f, verticalOffset, 0f);

            // 2. Smooth bone head-tilt on NeckHeadBone
            _currentHeadTiltDegrees = Mathf.MoveTowards(
                _currentHeadTiltDegrees,
                _targetHeadTiltDegrees,
                tiltSpeed * 15f * deltaTime);
            _neckHeadBone.localRotation = Quaternion.Euler(0f, 0f, _currentHeadTiltDegrees);

            // 3. Mouth phoneme oscillation when talking
            if (_isTalking)
            {
                _talkingTime += deltaTime;
                bool openFrame = Mathf.Sin(_talkingTime * phonemeFreq * Mathf.PI * 2f) >= 0f;
                if (openFrame != _isMouthOpenFrame)
                {
                    _isMouthOpenFrame = openFrame;
                    ApplyMouthState(_isMouthOpenFrame);
                }
            }
            else if (_isMouthOpenFrame)
            {
                _isMouthOpenFrame = false;
                ApplyMouthState(false);
            }
        }

        private void ApplyMouthState(bool useOpenTalkingMouth)
        {
            if (_mouthResolver == null || _mouthRenderer == null) return;

            if (useOpenTalkingMouth && _rigConfig != null && _rigConfig.TalkingOpenMouthSprite != null)
            {
                ResolveSlot(
                    _mouthResolver,
                    _mouthRenderer,
                    CharacterPortraitRigConfigSO.CategoryMouth,
                    CharacterPortraitRigConfigSO.LabelTalkingMouth,
                    _rigConfig.TalkingOpenMouthSprite);
                return;
            }

            string mouthLabel = !string.IsNullOrWhiteSpace(_activeExpression.MouthLabel)
                ? _activeExpression.MouthLabel
                : _currentMood;

            ResolveSlot(
                _mouthResolver,
                _mouthRenderer,
                CharacterPortraitRigConfigSO.CategoryMouth,
                mouthLabel,
                _activeExpression.MouthSprite);
        }

        private static void ResolveSlot(
            SpriteResolver resolver,
            SpriteRenderer renderer,
            string category,
            string label,
            Sprite directFallback)
        {
            if (resolver != null && !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(label))
            {
                resolver.SetCategoryAndLabel(category, label);
                resolver.ResolveSpriteToSpriteRenderer();
            }

            if (renderer != null && renderer.sprite == null && directFallback != null)
            {
                renderer.sprite = directFallback;
            }
        }

        private static void EnsureLayerSlot(
            Transform parentBone,
            string slotName,
            int sortingOrder,
            Vector3 localOffset,
            ref SpriteRenderer renderer,
            ref SpriteResolver resolver)
        {
            if (renderer == null)
            {
                var existing = parentBone.Find(slotName);
                GameObject slotGo;
                if (existing != null)
                {
                    slotGo = existing.gameObject;
                }
                else
                {
                    slotGo = new GameObject(slotName);
                    slotGo.transform.SetParent(parentBone, false);
                    slotGo.transform.localPosition = localOffset;
                }

                renderer = slotGo.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = slotGo.AddComponent<SpriteRenderer>();
                }
            }

            renderer.sortingOrder = sortingOrder;

            if (resolver == null)
            {
                resolver = renderer.GetComponent<SpriteResolver>();
                if (resolver == null)
                {
                    resolver = renderer.gameObject.AddComponent<SpriteResolver>();
                }
            }
        }
    }
}
