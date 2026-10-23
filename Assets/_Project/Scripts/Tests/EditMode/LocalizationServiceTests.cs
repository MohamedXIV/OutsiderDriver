using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ink;
using OutsiderDriver.Core.State;
using OutsiderDriver.Data;
using OutsiderDriver.Dialogue;
using OutsiderDriver.Localization;
using OutsiderDriver.UI.Dialogue;

namespace OutsiderDriver.Tests.EditMode
{
    [TestFixture]
    public class LocalizationServiceTests
    {
        [Test]
        public void LocalizationService_DefaultLocaleIsEnglish_AndResolvesDefaultStrings()
        {
            var loc = new LocalizationService();

            Assert.AreEqual("en", loc.CurrentLocaleCode);
            Assert.IsFalse(loc.IsRtl);

            string continueText = loc.GetLocalizedString(LocalizationService.TableUI, "ui_continue");
            Assert.AreEqual("Continue", continueText);

            string boardTaxi = loc.GetLocalizedString(LocalizationService.TablePrompts, "prompt_board_taxi");
            Assert.AreEqual("Board / Drive Taxi", boardTaxi);

            string district = loc.GetLocalizedString(LocalizationService.TableDistricts, "district_amber_terrace");
            Assert.AreEqual("Amber Terrace", district);

            string fare = loc.GetLocalizedString(LocalizationService.TableUI, "hud_fare_credits", 120);
            Assert.AreEqual("120 Credits", fare);
        }

        [Test]
        public void LocalizationService_SetLocaleArabic_SwitchesRtlAndReturnsArabicStrings()
        {
            var loc = new LocalizationService();
            string reportedLocale = null;
            loc.OnLocaleChanged += l => reportedLocale = l;

            loc.SetLocale("ar");

            Assert.AreEqual("ar", loc.CurrentLocaleCode);
            Assert.AreEqual("ar", reportedLocale);
            Assert.IsTrue(loc.IsRtl);

            string continueText = loc.GetLocalizedString(LocalizationService.TableUI, "ui_continue");
            Assert.IsTrue(ArabicRtlShaper.ContainsArabic(continueText));

            string fare = loc.GetLocalizedString(LocalizationService.TableUI, "hud_fare_credits", 250);
            Assert.IsTrue(fare.Contains("250"));
            Assert.IsTrue(ArabicRtlShaper.ContainsArabic(fare));
        }

        [Test]
        public void LocalizationService_FallbackBehavior_ReturnsEnglishOrKeyWhenMissing()
        {
            var loc = new LocalizationService();
            loc.RegisterString("CustomTable", "key_only_english", "en", "English Only Value");

            loc.SetLocale("ar");
            string fallbackResult = loc.GetLocalizedString("CustomTable", "key_only_english");
            Assert.AreEqual("English Only Value", fallbackResult);

            string missingResult = loc.GetLocalizedString("CustomTable", "non_existent_key");
            Assert.AreEqual("non_existent_key", missingResult);
        }

        [Test]
        public void ArabicRtlShaper_ShapesCharactersAndLaamAlifLigatures()
        {
            // Raw Arabic text with Laam-Alif: "لا" (U+0644 + U+0627)
            string rawLaamAlif = "لا";
            string shaped = ArabicRtlShaper.ShapeAndReorder(rawLaamAlif);

            Assert.IsNotNull(shaped);
            // Must contain isolated Laam-Alif ligature U+FEFB
            Assert.IsTrue(shaped.Contains("\uFEFB"));

            // Raw word: "باب" (Baa + Alif + Baa)
            // Initial Baa: \uFE91, Final Alif: \uFE8E, Isolated Baa: \uFE8F
            // In reversed BiDi display: Isolated Baa (\uFE8F), Final Alif (\uFE8E), Initial Baa (\uFE91)
            string rawDoor = "باب";
            string shapedDoor = ArabicRtlShaper.ShapeAndReorder(rawDoor);
            Assert.IsTrue(shapedDoor.Contains("\uFE8F"));
            Assert.IsTrue(shapedDoor.Contains("\uFE8E"));
            Assert.IsTrue(shapedDoor.Contains("\uFE91"));
        }

        [Test]
        public void ArabicRtlShaper_PreservesLatinTokensAndNumbersInBidiText()
        {
            string mixed = "تاكسي 404";
            string formatted = ArabicRtlShaper.ShapeAndReorder(mixed);

            // Number 404 must remain in left-to-right order "404"
            Assert.IsTrue(formatted.Contains("404"));
        }

        [Test]
        public void DialoguePresenter_WithLocalizationService_UpdatesViewLabelsAndShapesRtlDialogue()
        {
            const string inkSource = @"
=== ride_start ===
مرحبا بك في المدينة! #speaker:عمر #lang:Common #mood:friendly
+ [شكرا جزيلا]
    -> END
";
            var compiler = new Compiler(inkSource, new Compiler.Options { countAllVisits = true });
            string json = compiler.Compile().ToJson();

            var playerState = new PlayerStateService();
            var dialogueService = new InkDialogueService(playerState);
            var mockView = new MockLocalizedDialogueView();
            var locService = new LocalizationService("ar");

            using var presenter = new DialoguePresenter(dialogueService, mockView, null, locService);
            presenter.Start();

            // View buttons should have localized Arabic labels and RTL alignment applied
            Assert.IsTrue(mockView.TextDirectionIsRtl);
            Assert.IsTrue(ArabicRtlShaper.ContainsArabic(mockView.ContinueButtonLabel));

            dialogueService.StartDialogueFromJson(json, "res_omar", "Common", "ride_start");

            Assert.IsTrue(mockView.IsVisible);
            Assert.IsTrue(ArabicRtlShaper.ContainsArabic(mockView.LastDialogueText));
            Assert.AreEqual(1, mockView.LastChoices.Count);
            Assert.IsTrue(ArabicRtlShaper.ContainsArabic(mockView.LastChoices[0].Text));
        }

        private sealed class MockLocalizedDialogueView : IDialogueView
        {
            public bool IsVisible { get; private set; }
            public string LastSpeakerName { get; private set; }
            public string LastMood { get; private set; }
            public string LastLanguageLabel { get; private set; }
            public string LastDialogueText { get; private set; }
            public bool LastIsUnderstood { get; private set; }
            public bool ContinueButtonVisible { get; private set; }
            public bool ArrivalBannerVisible { get; private set; }
            public string ArrivalBannerMessage { get; private set; }
            public string ContinueButtonLabel { get; private set; }
            public string SilentButtonLabel { get; private set; }
            public bool TextDirectionIsRtl { get; private set; }
            public IReadOnlyList<DialogueChoice> LastChoices { get; private set; } = Array.Empty<DialogueChoice>();

            public event Action OnContinueClicked;
            public event Action OnStaySilentClicked;
            public event Action<int> OnChoiceSelected;

            public void SetVisible(bool visible) => IsVisible = visible;

            public void SetSpeakerInfo(string speakerName, string mood, string languageLabel, bool isUnderstood)
            {
                LastSpeakerName = speakerName;
                LastMood = mood;
                LastLanguageLabel = languageLabel;
                LastIsUnderstood = isUnderstood;
            }

            public void SetPortrait(Sprite portraitSprite) { }

            public void SetDialogueText(string formattedText, bool isUnderstood)
            {
                LastDialogueText = formattedText;
                LastIsUnderstood = isUnderstood;
            }

            public void SetContinueButtonVisible(bool visible) => ContinueButtonVisible = visible;

            public void SetChoices(IReadOnlyList<DialogueChoice> choices)
            {
                LastChoices = choices ?? Array.Empty<DialogueChoice>();
            }

            public void SetArrivalBannerVisible(bool visible, string message = null)
            {
                ArrivalBannerVisible = visible;
                ArrivalBannerMessage = message;
            }

            public void SetButtonLabels(string continueLabel, string silentLabel)
            {
                ContinueButtonLabel = continueLabel;
                SilentButtonLabel = silentLabel;
            }

            public void ApplyTextDirection(bool isRtl)
            {
                TextDirectionIsRtl = isRtl;
            }

            public void ClearView()
            {
                LastDialogueText = string.Empty;
                LastChoices = Array.Empty<DialogueChoice>();
                ContinueButtonVisible = false;
                ArrivalBannerVisible = false;
            }
        }
    }
}
