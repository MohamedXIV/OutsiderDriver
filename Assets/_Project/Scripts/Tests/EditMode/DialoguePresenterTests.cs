using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ink;
using OutsiderDriver.Core.State;
using OutsiderDriver.Data;
using OutsiderDriver.Dialogue;
using OutsiderDriver.Navigation;
using OutsiderDriver.Rides;
using OutsiderDriver.UI.Dialogue;
using OutsiderDriver.Vehicles;

namespace OutsiderDriver.Tests.EditMode
{
    public class DialoguePresenterTests
    {
        private readonly List<UnityEngine.Object> _spawnedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                if (_spawnedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_spawnedObjects[i]);
                }
            }
            _spawnedObjects.Clear();
            VehicleController.ClearActiveVehicles();
        }

        [Test]
        public void Presenter_DisplaysUnderstoodDialogueChoicesAndForwardsSelection()
        {
            const string inkSource = @"
=== ride_start ===
Take the coastal bridge, please. #speaker:Lyra #lang:Common #mood:calm
+ [Sure thing, heading there now.]
    Many thanks! #speaker:Lyra #lang:Common #mood:happy
    -> END
";
            string json = CompileInkToJson(inkSource);

            var playerState = new PlayerStateService();
            var dialogueService = new InkDialogueService(playerState);
            var mockView = new MockDialogueView();

            using var presenter = new DialoguePresenter(dialogueService, mockView);
            presenter.Start();

            Assert.IsFalse(mockView.IsVisible);

            dialogueService.StartDialogueFromJson(json, "res_lyra", "Common", "ride_start");

            Assert.IsTrue(mockView.IsVisible);
            Assert.AreEqual("Take the coastal bridge, please.", mockView.LastDialogueText);
            Assert.IsTrue(mockView.LastIsUnderstood);
            Assert.AreEqual("calm", mockView.LastMood);
            Assert.AreEqual(1, mockView.LastChoices.Count);

            // Simulate user clicking Choice 0 in the UI View
            mockView.SimulateChoiceSelected(0);

            // Story advances through "Many thanks!" and hits -> END, closing the view
            Assert.IsFalse(dialogueService.IsConversationActive);
            Assert.IsFalse(mockView.IsVisible);
        }

        [Test]
        public void Presenter_ObfuscatesUntranslatedAlienLanguageUntilModuleInstalled()
        {
            const string inkSource = @"
=== ride_start ===
Zorath vek talos! #speaker:Vori #lang:Vesperian #mood:urgent
+ [Continue]
    -> END
";
            string json = CompileInkToJson(inkSource);

            var playerState = new PlayerStateService();
            var dialogueService = new InkDialogueService(playerState);
            var mockView = new MockDialogueView();

            using var presenter = new DialoguePresenter(dialogueService, mockView);
            presenter.Start();

            dialogueService.StartDialogueFromJson(json, "res_vori", "Vesperian", "ride_start");

            Assert.IsTrue(mockView.IsVisible);
            Assert.IsFalse(mockView.LastIsUnderstood);
            Assert.AreNotEqual("Zorath vek talos!", mockView.LastDialogueText, "Untranslated alien speech should be obfuscated with glyphs.");
            StringAssert.EndsWith("!", mockView.LastDialogueText, "Punctuation should be preserved in obfuscated alien speech.");
        }

        [Test]
        public void Presenter_NonBlockingArrival_ShowsGentleArrivalBannerWithoutCuttingOffActiveDialogue()
        {
            const string inkSource = @"
=== ride_start ===
We are almost there, let me tell you one more thing. #speaker:Lyra #lang:Common #mood:warm
+ [I'm listening.]
    Safe travels, friend.
    -> END
";
            string json = CompileInkToJson(inkSource);
            var inkAsset = new TextAsset(json);
            _spawnedObjects.Add(inkAsset);

            var passenger = ScriptableObject.CreateInstance<PassengerDefinitionSO>();
            passenger.Initialize("res_lyra", "Lyra", "Vesperian", "Common", inkStoryJson: inkAsset);
            _spawnedObjects.Add(passenger);

            var networkGo = new GameObject("RoadNetwork");
            _spawnedObjects.Add(networkGo);
            var network = networkGo.AddComponent<RoadNetwork>();

            var nodePickup = CreateNode(networkGo.transform, "Pickup", Vector3.zero);
            var nodeDropoff = CreateNode(networkGo.transform, "Dropoff", new Vector3(2f, 0f, 0f));
            nodePickup.AddConnection(nodeDropoff);
            network.RebuildNetwork();

            var taxiGo = new GameObject("PlayerTaxi");
            _spawnedObjects.Add(taxiGo);
            var taxi = taxiGo.AddComponent<VehicleController>();
            taxi.Construct(network);
            VehicleController.RegisterActiveVehicle(taxi);

            var playerState = new PlayerStateService();
            using var rideService = new RideLifecycleService(network, taxi, playerState);
            var dialogueService = new InkDialogueService(playerState);
            var mockView = new MockDialogueView();

            using var presenter = new DialoguePresenter(dialogueService, mockView, rideService);
            presenter.Start();

            // Offer, accept at pickup, and board passenger (which automatically starts passenger Ink dialogue)
            Assert.IsTrue(rideService.TryOfferHail(passenger, nodePickup, nodeDropoff));
            Assert.IsTrue(rideService.AcceptHail());
            Assert.IsTrue(rideService.BoardPassenger());

            Assert.IsTrue(dialogueService.IsConversationActive, "Boarding passenger with InkStoryJson should start dialogue automatically.");
            Assert.IsTrue(mockView.IsVisible);
            Assert.IsFalse(mockView.ArrivalBannerVisible);

            // Drive taxi to destination while conversation is still waiting on player choice
            for (int i = 0; i < 60 && taxi.HasActiveRoute; i++)
            {
                taxi.TickMovement(0.05f);
            }

            Assert.AreEqual(RidePhase.ArrivedAtDestination, rideService.CurrentPhase);
            Assert.IsTrue(dialogueService.IsConversationActive, "Arriving at destination must NOT cut off active passenger conversation.");
            Assert.IsTrue(mockView.IsVisible);
            Assert.IsTrue(mockView.ArrivalBannerVisible, "Presenter should display the gentle arrival notice while conversation continues.");
        }

        private RoadNode CreateNode(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent);
            _spawnedObjects.Add(go);
            return go.AddComponent<RoadNode>();
        }

        private static string CompileInkToJson(string inkSource)
        {
            var compiler = new Compiler(inkSource, new Compiler.Options { countAllVisits = true });
            return compiler.Compile().ToJson();
        }

        private sealed class MockDialogueView : IDialogueView
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

            public void SetButtonLabels(string continueLabel, string silentLabel) { }

            public void ApplyTextDirection(bool isRtl) { }

            public void ClearView()
            {
                LastDialogueText = string.Empty;
                LastChoices = Array.Empty<DialogueChoice>();
                ContinueButtonVisible = false;
                ArrivalBannerVisible = false;
            }

            public void SimulateContinueClicked() => OnContinueClicked?.Invoke();
            public void SimulateStaySilentClicked() => OnStaySilentClicked?.Invoke();
            public void SimulateChoiceSelected(int index) => OnChoiceSelected?.Invoke(index);
        }
    }
}
