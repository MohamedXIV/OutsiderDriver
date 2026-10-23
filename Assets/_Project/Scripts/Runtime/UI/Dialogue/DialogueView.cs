using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using OutsiderDriver.Dialogue;

namespace OutsiderDriver.UI.Dialogue
{
    /// <summary>
    /// UI Toolkit implementation of IDialogueView for non-blocking conversational taxi rides.
    /// Uses PickMode.Ignore on background containers so the player can observe and interact
    /// with the city while talking to a passenger (GAME_DESIGN §1 & §3.B, Decision P-019).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class DialogueView : MonoBehaviour, IDialogueView
    {
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _rootContainer;
        private VisualElement _portraitImage;
        private Label _speakerLabel;
        private Label _moodBadgeLabel;
        private Label _languageBadgeLabel;
        private Label _dialogueBodyLabel;
        private Label _arrivalBannerLabel;
        private VisualElement _choicesContainer;
        private Button _continueButton;
        private Button _staySilentButton;

        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public event Action OnContinueClicked;
        public event Action OnStaySilentClicked;
        public event Action<int> OnChoiceSelected;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            BuildOrBindVisualTree();
            SetVisible(false);
        }

        /// <summary>
        /// Binds to existing UXML elements if assigned, or constructs a clean, responsive
        /// UI Toolkit hierarchy programmatically when no UXML template is set.
        /// </summary>
        public void BuildOrBindVisualTree()
        {
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
            {
                return;
            }

            VisualElement docRoot = _uiDocument.rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;

            _rootContainer = docRoot.Q<VisualElement>("dialogue-root");
            if (_rootContainer == null)
            {
                _rootContainer = CreateDefaultLayout(docRoot);
            }
            else
            {
                _portraitImage = _rootContainer.Q<VisualElement>("passenger-portrait");
                _speakerLabel = _rootContainer.Q<Label>("speaker-name");
                _moodBadgeLabel = _rootContainer.Q<Label>("mood-badge");
                _languageBadgeLabel = _rootContainer.Q<Label>("language-badge");
                _dialogueBodyLabel = _rootContainer.Q<Label>("dialogue-body");
                _arrivalBannerLabel = _rootContainer.Q<Label>("arrival-banner");
                _choicesContainer = _rootContainer.Q<VisualElement>("choices-container");
                _continueButton = _rootContainer.Q<Button>("continue-button");
                _staySilentButton = _rootContainer.Q<Button>("silent-button");
            }

            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueClicked;
                _continueButton.clicked += HandleContinueClicked;
            }

            if (_staySilentButton != null)
            {
                _staySilentButton.clicked -= HandleStaySilentClicked;
                _staySilentButton.clicked += HandleStaySilentClicked;
            }
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_rootContainer != null)
            {
                _rootContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetSpeakerInfo(string speakerName, string mood, string languageLabel, bool isUnderstood)
        {
            if (_speakerLabel != null)
            {
                _speakerLabel.text = string.IsNullOrWhiteSpace(speakerName) ? "Passenger" : speakerName;
            }

            if (_moodBadgeLabel != null)
            {
                _moodBadgeLabel.text = string.IsNullOrWhiteSpace(mood) ? string.Empty : $"[{mood}]";
            }

            if (_languageBadgeLabel != null)
            {
                _languageBadgeLabel.text = isUnderstood
                    ? languageLabel
                    : $"{languageLabel} (Untranslated)";
            }
        }

        public void SetPortrait(Sprite portraitSprite)
        {
            if (_portraitImage == null) return;

            if (portraitSprite != null)
            {
                _portraitImage.style.backgroundImage = new StyleBackground(portraitSprite);
                _portraitImage.style.display = DisplayStyle.Flex;
            }
            else
            {
                _portraitImage.style.backgroundImage = null;
                _portraitImage.style.display = DisplayStyle.None;
            }
        }

        public void SetDialogueText(string formattedText, bool isUnderstood)
        {
            if (_dialogueBodyLabel == null) return;

            _dialogueBodyLabel.text = formattedText ?? string.Empty;
            _dialogueBodyLabel.style.unityFontStyleAndWeight = isUnderstood
                ? FontStyle.Normal
                : FontStyle.Italic;
        }

        public void SetContinueButtonVisible(bool visible)
        {
            if (_continueButton != null)
            {
                _continueButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetChoices(IReadOnlyList<DialogueChoice> choices)
        {
            if (_choicesContainer == null) return;

            _choicesContainer.Clear();
            if (choices == null || choices.Count == 0)
            {
                _choicesContainer.style.display = DisplayStyle.None;
                return;
            }

            _choicesContainer.style.display = DisplayStyle.Flex;
            for (int i = 0; i < choices.Count; i++)
            {
                int choiceIndex = choices[i].Index;
                var choiceBtn = new Button(() => OnChoiceSelected?.Invoke(choiceIndex))
                {
                    text = choices[i].Text
                };
                choiceBtn.style.marginBottom = 4;
                choiceBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                _choicesContainer.Add(choiceBtn);
            }
        }

        public void SetArrivalBannerVisible(bool visible, string message = null)
        {
            if (_arrivalBannerLabel == null) return;

            _arrivalBannerLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!string.IsNullOrWhiteSpace(message))
            {
                _arrivalBannerLabel.text = message;
            }
        }

        public void ClearView()
        {
            SetDialogueText(string.Empty, true);
            SetChoices(Array.Empty<DialogueChoice>());
            SetContinueButtonVisible(false);
            SetArrivalBannerVisible(false);
        }

        private void HandleContinueClicked() => OnContinueClicked?.Invoke();
        private void HandleStaySilentClicked() => OnStaySilentClicked?.Invoke();

        private VisualElement CreateDefaultLayout(VisualElement docRoot)
        {
            var root = new VisualElement { name = "dialogue-root" };
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.paddingLeft = 24;
            root.style.paddingRight = 24;
            root.style.paddingTop = 24;
            root.style.paddingBottom = 24;

            // Upper-right passenger portrait & header panel
            var topRow = new VisualElement();
            topRow.pickingMode = PickingMode.Ignore;
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.FlexEnd;

            var portraitCard = new VisualElement();
            portraitCard.style.backgroundColor = new Color(0.14f, 0.16f, 0.22f, 0.92f);
            portraitCard.style.paddingLeft = 12;
            portraitCard.style.paddingRight = 12;
            portraitCard.style.paddingTop = 10;
            portraitCard.style.paddingBottom = 10;
            portraitCard.style.borderTopLeftRadius = 8;
            portraitCard.style.borderTopRightRadius = 8;
            portraitCard.style.borderBottomLeftRadius = 8;
            portraitCard.style.borderBottomRightRadius = 8;

            _portraitImage = new VisualElement { name = "passenger-portrait" };
            _portraitImage.style.width = 96;
            _portraitImage.style.height = 96;
            _portraitImage.style.marginBottom = 6;
            portraitCard.Add(_portraitImage);

            _speakerLabel = new Label("Passenger") { name = "speaker-name" };
            _speakerLabel.style.color = new Color(1f, 0.85f, 0.45f);
            _speakerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            portraitCard.Add(_speakerLabel);

            _moodBadgeLabel = new Label(string.Empty) { name = "mood-badge" };
            _moodBadgeLabel.style.color = new Color(0.75f, 0.85f, 0.95f);
            portraitCard.Add(_moodBadgeLabel);

            _languageBadgeLabel = new Label("Common") { name = "language-badge" };
            _languageBadgeLabel.style.color = new Color(0.65f, 0.9f, 0.75f);
            portraitCard.Add(_languageBadgeLabel);

            topRow.Add(portraitCard);
            root.Add(topRow);

            // Lower-left conversation & response panel (non-blocking middle screen for road view)
            var bottomPanel = new VisualElement();
            bottomPanel.style.maxWidth = 680;
            bottomPanel.style.backgroundColor = new Color(0.11f, 0.13f, 0.18f, 0.94f);
            bottomPanel.style.paddingLeft = 16;
            bottomPanel.style.paddingRight = 16;
            bottomPanel.style.paddingTop = 14;
            bottomPanel.style.paddingBottom = 14;
            bottomPanel.style.borderTopLeftRadius = 8;
            bottomPanel.style.borderTopRightRadius = 8;
            bottomPanel.style.borderBottomLeftRadius = 8;
            bottomPanel.style.borderBottomRightRadius = 8;

            _arrivalBannerLabel = new Label("Arrived at destination — take your time finishing the conversation.")
            {
                name = "arrival-banner"
            };
            _arrivalBannerLabel.style.color = new Color(1f, 0.78f, 0.28f);
            _arrivalBannerLabel.style.marginBottom = 6;
            _arrivalBannerLabel.style.display = DisplayStyle.None;
            bottomPanel.Add(_arrivalBannerLabel);

            _dialogueBodyLabel = new Label(string.Empty) { name = "dialogue-body" };
            _dialogueBodyLabel.style.color = new Color(0.95f, 0.94f, 0.9f);
            _dialogueBodyLabel.style.whiteSpace = WhiteSpace.Normal;
            _dialogueBodyLabel.style.marginBottom = 10;
            bottomPanel.Add(_dialogueBodyLabel);

            _choicesContainer = new VisualElement { name = "choices-container" };
            _choicesContainer.style.marginBottom = 8;
            bottomPanel.Add(_choicesContainer);

            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;

            _continueButton = new Button { name = "continue-button", text = "Continue" };
            _continueButton.style.marginRight = 8;
            actionRow.Add(_continueButton);

            _staySilentButton = new Button { name = "silent-button", text = "[Stay Quiet]" };
            actionRow.Add(_staySilentButton);

            bottomPanel.Add(actionRow);
            root.Add(bottomPanel);

            docRoot.Add(root);
            return root;
        }
    }
}
