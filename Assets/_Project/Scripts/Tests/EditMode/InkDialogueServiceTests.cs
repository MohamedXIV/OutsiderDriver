using NUnit.Framework;
using Ink;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Dialogue;

namespace OutsiderDriver.Tests.EditMode
{
    public class InkDialogueServiceTests
    {
        [Test]
        public void StartDialogue_ParsesTagsAndEvaluatesLanguageComprehension()
        {
            const string inkSource = @"
=== ride_start ===
Greetings, driver. Keep to the upper ring. #speaker:Lyra #lang:Vesperian #mood:curious
+ [Yes, right away.]
    Appreciated. #speaker:Lyra #lang:Common #mood:warm
    -> END
+ [(Silence) Say nothing]
    Quiet type, huh? #speaker:Lyra #lang:Common #mood:neutral
    -> END
";
            string compiledJson = CompileInkToJson(inkSource);

            var playerState = new PlayerStateService();
            var dialogueService = new InkDialogueService(playerState);

            // Initially player does NOT have "Vesperian" installed, only "Common"
            bool started = dialogueService.StartDialogueFromJson(
                inkJson: compiledJson,
                residentId: "res_lyra",
                defaultLanguageId: "Vesperian",
                entryKnot: "ride_start");

            Assert.IsTrue(started);
            Assert.IsNotNull(dialogueService.CurrentLine);
            Assert.AreEqual("Lyra", dialogueService.CurrentLine.SpeakerId);
            Assert.AreEqual("Vesperian", dialogueService.CurrentLine.LanguageId);
            Assert.AreEqual("curious", dialogueService.CurrentLine.Mood);
            Assert.IsFalse(dialogueService.CurrentLine.IsUnderstoodByPlayer, "Line in Vesperian should not be understood before installing module.");
            Assert.AreEqual(2, dialogueService.CurrentChoices.Count);

            // Install Vesperian and test StaySilent() selecting the [Silence] choice
            playerState.InstallLanguageModule("Vesperian");
            dialogueService.StaySilent();

            // Since the story reached -> END after the choice response line, verify the line was published
            Assert.IsFalse(dialogueService.IsConversationActive, "Story should end after exhausting the chosen branch.");
        }

        [Test]
        public void ExternalFunctions_TwoWayStateBinding_ReadsAndMutatesPlayerAndRelationshipState()
        {
            const string inkSource = @"
EXTERNAL CanUnderstandLanguage(langId)
EXTERNAL GetCredits()
EXTERNAL AddCredits(amount)
EXTERNAL AdjustCoverIntegrity(delta)
EXTERNAL RecordInteraction(residentId, affinityDelta, trustDelta, suspicionDelta)
EXTERNAL GetTrust(residentId)

=== ride_start ===
~ AddCredits(25)
~ RecordInteraction(""res_krell"", 10, 15, -5)
You speak Krellish! Here is a bonus tip.
+ [Thank you]
    ~ AdjustCoverIntegrity(-10)
    Current trust is {GetTrust(""res_krell"")} and credits are {GetCredits()}.
    -> END
";
            string compiledJson = CompileInkToJson(inkSource);

            var timeService = new TimeService(null);
            timeService.SetTime(new WorldTime(day: 3, hour: 19, minute: 15));

            var playerState = new PlayerStateService(timeService);
            playerState.InstallLanguageModule("Krellish");

            var dialogueService = new InkDialogueService(playerState, timeService);

            DialogueLine lastPublishedLine = null;
            dialogueService.OnLinePublished += line => lastPublishedLine = line;

            bool started = dialogueService.StartDialogueFromJson(
                inkJson: compiledJson,
                residentId: "res_krell",
                defaultLanguageId: "Krellish",
                entryKnot: "ride_start");

            Assert.IsTrue(started);
            Assert.AreEqual(125, playerState.Credits, "Ink external function AddCredits(25) should increase player credits from 100 to 125.");

            var resident = playerState.GetOrAddResident("res_krell");
            Assert.AreEqual(10, resident.Affinity);
            Assert.AreEqual(15, resident.PersonalTrust);
            Assert.AreEqual(0, resident.SuspicionOfHumanity);

            // Select the [Thank you] choice
            Assert.AreEqual(1, dialogueService.CurrentChoices.Count);
            Assert.IsTrue(dialogueService.SelectChoice(0));

            Assert.AreEqual(90, playerState.CoverIntegrity, "AdjustCoverIntegrity(-10) should reduce cover integrity to 90.");
            Assert.IsNotNull(lastPublishedLine);
            StringAssert.Contains("Current trust is 15 and credits are 125.", lastPublishedLine.Text);
        }

        private static string CompileInkToJson(string inkSource)
        {
            var compiler = new Compiler(inkSource, new Compiler.Options
            {
                countAllVisits = true
            });
            var story = compiler.Compile();
            Assert.IsNotNull(story, "Ink source failed to compile in test.");
            return story.ToJson();
        }
    }
}
