using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ink;
using OutsiderDriver.Core.State;
using OutsiderDriver.Dialogue;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.Visuals.Portraits;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class CharacterPortraitRigTests
    {
        private readonly List<UnityEngine.Object> _createdAssets = new List<UnityEngine.Object>();
        private GameObject _rigGo;
        private CharacterPortraitRig _rig;
        private CharacterPortraitRigConfigSO _config;

        private Sprite _torsoSprite;
        private Sprite _headSprite;
        private Sprite _neutralEyesSprite;
        private Sprite _neutralMouthSprite;
        private Sprite _curiousEyesSprite;
        private Sprite _curiousMouthSprite;
        private Sprite _curiousEmotionSprite;
        private Sprite _talkingOpenMouthSprite;

        [SetUp]
        public void SetUp()
        {
            _torsoSprite = CreateTestSprite("Torso");
            _headSprite = CreateTestSprite("Head");
            _neutralEyesSprite = CreateTestSprite("Eyes_Neutral");
            _neutralMouthSprite = CreateTestSprite("Mouth_Neutral");
            _curiousEyesSprite = CreateTestSprite("Eyes_Curious");
            _curiousMouthSprite = CreateTestSprite("Mouth_Curious");
            _curiousEmotionSprite = CreateTestSprite("Emotion_Spark");
            _talkingOpenMouthSprite = CreateTestSprite("Mouth_TalkingOpen");

            _config = ScriptableObject.CreateInstance<CharacterPortraitRigConfigSO>();
            _createdAssets.Add(_config);

            _config.Configure(
                "res_lyra",
                _torsoSprite,
                _headSprite,
                _talkingOpenMouthSprite,
                new[]
                {
                    new PortraitExpressionEntry(
                        "neutral",
                        "neutral",
                        "neutral",
                        "none",
                        0f,
                        _neutralEyesSprite,
                        _neutralMouthSprite,
                        null),
                    new PortraitExpressionEntry(
                        "curious",
                        "curious",
                        "curious",
                        "spark",
                        6.5f,
                        _curiousEyesSprite,
                        _curiousMouthSprite,
                        _curiousEmotionSprite)
                },
                breathingAmplitude: 0.04f,
                breathingFrequency: 2f,
                headTiltSmoothingSpeed: 10f,
                talkingPhonemeFrequency: 5f);

            if (_config.SpriteLibraryAsset != null)
            {
                _createdAssets.Add(_config.SpriteLibraryAsset);
            }

            _rigGo = new GameObject("TestPortraitRig");
            _rig = _rigGo.AddComponent<CharacterPortraitRig>();
            _rig.EnsureRigHierarchy();
            _rig.ApplyRigConfig(_config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_rigGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_rigGo);
            }

            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_createdAssets[i]);
                }
            }
            _createdAssets.Clear();
        }

        [Test]
        public void EnsureRigHierarchy_CreatesArticulatedBonesSpriteLibrarySkinAndLayeredResolvers()
        {
            Assert.IsNotNull(_rig.RootBone);
            Assert.IsNotNull(_rig.TorsoBone);
            Assert.IsNotNull(_rig.NeckHeadBone);
            Assert.AreEqual(_rig.RootBone, _rig.TorsoBone.parent);
            Assert.AreEqual(_rig.TorsoBone, _rig.NeckHeadBone.parent);

            Assert.IsNotNull(_rig.SpriteLibrary);
            Assert.IsNotNull(_rig.SpriteLibrary.spriteLibraryAsset);
            Assert.IsNotNull(_rig.TorsoSkin);
            Assert.IsNotNull(_rig.HeadSkin);

            // Verify layered sorting order from back to front
            Assert.Less(_rig.TorsoRenderer.sortingOrder, _rig.HeadRenderer.sortingOrder);
            Assert.Less(_rig.HeadRenderer.sortingOrder, _rig.EyesRenderer.sortingOrder);
            Assert.Less(_rig.EyesRenderer.sortingOrder, _rig.MouthRenderer.sortingOrder);
            Assert.Less(_rig.MouthRenderer.sortingOrder, _rig.EmotionOverlayRenderer.sortingOrder);
        }

        [Test]
        public void ApplyRigConfigAndSetMood_ResolvesSpritesViaSpriteLibraryAndUpdatesTargetHeadTilt()
        {
            Assert.AreEqual(_torsoSprite, _rig.TorsoRenderer.sprite);
            Assert.AreEqual(_headSprite, _rig.HeadRenderer.sprite);
            Assert.AreEqual("neutral", _rig.CurrentMood);
            Assert.AreEqual(_neutralEyesSprite, _rig.EyesRenderer.sprite);
            Assert.AreEqual(_neutralMouthSprite, _rig.MouthRenderer.sprite);
            Assert.IsFalse(_rig.EmotionOverlayRenderer.enabled);

            // Switch to 'curious' mood
            _rig.SetMood("curious");

            Assert.AreEqual("curious", _rig.CurrentMood);
            Assert.AreEqual(6.5f, _rig.TargetHeadTiltDegrees, 0.001f);
            Assert.AreEqual(_curiousEyesSprite, _rig.EyesRenderer.sprite);
            Assert.AreEqual(_curiousMouthSprite, _rig.MouthRenderer.sprite);
            Assert.AreEqual(_curiousEmotionSprite, _rig.EmotionOverlayRenderer.sprite);
            Assert.IsTrue(_rig.EmotionOverlayRenderer.enabled);
        }

        [Test]
        public void TickAnimation_DrivesIdleBreathingBoneHeadTiltAndTalkingMouthPhonemes()
        {
            _rig.SetMood("curious");
            _rig.SetTalking(true);

            Assert.IsTrue(_rig.IsTalking);
            Assert.AreEqual(_talkingOpenMouthSprite, _rig.MouthRenderer.sprite);

            // Advance animation so breathing and head-tilt progress and phoneme flips
            _rig.TickAnimation(0.15f);

            Assert.AreNotEqual(0f, _rig.TorsoBone.localPosition.y, "TorsoBone should move vertically for idle breathing.");
            Assert.Greater(_rig.CurrentHeadTiltDegrees, 0f, "NeckHeadBone should rotate toward TargetHeadTiltDegrees.");
            Assert.AreEqual(_curiousMouthSprite, _rig.MouthRenderer.sprite, "Mouth phoneme should alternate during speech.");

            // Stop talking -> returns to closed mood mouth
            _rig.SetTalking(false);
            Assert.IsFalse(_rig.IsTalking);
            Assert.AreEqual(_curiousMouthSprite, _rig.MouthRenderer.sprite);
        }

        [Test]
        public void DialoguePresenter_WithPortraitRig_UpdatesMoodAndTalkingStateDuringConversation()
        {
            const string inkSource = @"
=== ride_start ===
Have you ever seen the upper aqueducts at dusk? #speaker:Lyra #lang:Common #mood:curious
+ [Tell me about them.]
    -> END
";
            var compiler = new Compiler(inkSource, new Compiler.Options { countAllVisits = true });
            string json = compiler.Compile().ToJson();

            var playerState = new PlayerStateService();
            var dialogueService = new InkDialogueService(playerState);
            var mockView = new StubDialogueView();

            using var presenter = new DialoguePresenter(dialogueService, mockView, null, null, _rig);
            presenter.Start();

            dialogueService.StartDialogueFromJson(json, "res_lyra", "Common", "ride_start");

            // Line had #mood:curious, followed immediately by choices -> mood is 'curious'
            Assert.AreEqual("curious", _rig.CurrentMood);
            Assert.IsFalse(_rig.IsTalking, "Once choices are presented, passenger pauses speaking.");

            dialogueService.EndDialogue();
            Assert.AreEqual("neutral", _rig.CurrentMood);
            Assert.IsFalse(_rig.IsTalking);
        }

        private Sprite CreateTestSprite(string name)
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            _createdAssets.Add(sprite);
            return sprite;
        }

        private sealed class StubDialogueView : IDialogueView
        {
            public bool IsVisible { get; private set; }
            public event Action OnContinueClicked;
            public event Action OnStaySilentClicked;
            public event Action<int> OnChoiceSelected;

            public void SetVisible(bool visible) => IsVisible = visible;
            public void SetSpeakerInfo(string speakerName, string mood, string languageLabel, bool isUnderstood) { }
            public void SetPortrait(Sprite portraitSprite) { }
            public void SetDialogueText(string formattedText, bool isUnderstood) { }
            public void SetContinueButtonVisible(bool visible) { }
            public void SetChoices(IReadOnlyList<DialogueChoice> choices) { }
            public void SetArrivalBannerVisible(bool visible, string message = null) { }
            public void SetButtonLabels(string continueLabel, string silentLabel) { }
            public void ApplyTextDirection(bool isRtl) { }
            public void ClearView() { }
        }
    }
}
