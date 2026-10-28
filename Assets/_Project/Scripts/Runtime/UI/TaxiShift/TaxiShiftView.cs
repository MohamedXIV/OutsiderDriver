using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using OutsiderDriver.Rides;

namespace OutsiderDriver.UI.TaxiShift
{
    /// <summary>
    /// UI Toolkit implementation of the compact taxi-shift HUD (Issue #20).
    /// Cards are created once and updated in place on demand; there is no per-frame polling and
    /// no full-tree rebuild. The left-side panel leaves the city and camera visible, and
    /// PickMode.Ignore on the shell lets clicks outside the panel reach the world.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class TaxiShiftView : MonoBehaviour, ITaxiShiftView
    {
        private const int MaxOfferCards = 3;
        private const float PanelWidth = 360f;

        private static readonly Color PanelBackground = new Color(0.055f, 0.105f, 0.115f, 0.93f);
        private static readonly Color CardBackground = new Color(0.085f, 0.145f, 0.155f, 0.96f);
        private static readonly Color ActiveBackground = new Color(0.12f, 0.15f, 0.14f, 0.97f);
        private static readonly Color AccentAmber = new Color(0.98f, 0.73f, 0.34f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.93f, 0.84f, 1f);
        private static readonly Color Muted = new Color(0.67f, 0.76f, 0.73f, 1f);
        private static readonly Color Faint = new Color(0.52f, 0.62f, 0.60f, 1f);
        private static readonly Color Warning = new Color(1f, 0.58f, 0.40f, 1f);
        private static readonly Color Divider = new Color(0.98f, 0.73f, 0.34f, 0.28f);
        private static readonly Color CardBorder = new Color(0.98f, 0.73f, 0.34f, 0.22f);
        private static readonly Color ButtonTextDark = new Color(0.07f, 0.09f, 0.10f, 1f);

        [SerializeField] private UIDocument _uiDocument;

        private readonly OfferCardElements[] _offerCards = new OfferCardElements[MaxOfferCards];
        private readonly RideOffer[] _displayedOffers = new RideOffer[MaxOfferCards];
        private readonly List<VisualElement> _actionRows = new List<VisualElement>();
        private readonly List<Label> _bodyLabels = new List<Label>();

        private VisualElement _panel;
        private Label _titleLabel;
        private Label _creditsLabel;
        private Label _statusLabel;
        private Label _feedbackLabel;
        private VisualElement _offersContainer;
        private VisualElement _noWorkCard;
        private Label _noWorkTitleLabel;
        private Label _noWorkHintLabel;
        private VisualElement _activeCard;
        private Label _activeTitleLabel;
        private Label _activePassengerLabel;
        private Label _activeFareLabel;
        private Label _activePickupLabel;
        private Label _activeDestinationLabel;
        private Label _activePhaseLabel;
        private Button _boardButton;
        private Button _collectButton;

        private bool _built;
        private bool _activeVisible;
        private bool _rtl;

        public event Action<RideOffer> OnAcceptClicked;
        public event Action<RideOffer> OnDeclineClicked;
        public event Action OnBoardClicked;
        public event Action OnCollectClicked;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }
        }

        private void OnEnable()
        {
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
            {
                return;
            }

            BuildTree(_uiDocument.rootVisualElement);
            _built = true;
            ApplyTextDirection(_rtl);
        }

        public void SetTitle(string title)
        {
            EnsureBuilt();
            if (_titleLabel != null)
            {
                _titleLabel.text = title ?? string.Empty;
            }
        }

        public void SetCredits(string formattedCredits)
        {
            EnsureBuilt();
            if (_creditsLabel != null)
            {
                _creditsLabel.text = formattedCredits ?? string.Empty;
            }
        }

        public void SetStatus(string status, bool attention)
        {
            EnsureBuilt();
            if (_statusLabel == null)
            {
                return;
            }

            _statusLabel.text = status ?? string.Empty;
            _statusLabel.style.color = attention ? Warning : AccentAmber;
        }

        public void SetButtonLabels(string accept, string decline, string board, string collect)
        {
            EnsureBuilt();
            for (int i = 0; i < _offerCards.Length; i++)
            {
                if (_offerCards[i] == null) continue;
                _offerCards[i].AcceptButton.text = accept ?? string.Empty;
                _offerCards[i].DeclineButton.text = decline ?? string.Empty;
            }

            if (_boardButton != null)
            {
                _boardButton.text = board ?? string.Empty;
            }

            if (_collectButton != null)
            {
                _collectButton.text = collect ?? string.Empty;
            }
        }

        public void SetActiveTitle(string title)
        {
            EnsureBuilt();
            if (_activeTitleLabel != null)
            {
                _activeTitleLabel.text = title ?? string.Empty;
            }
        }

        public void SetNoWorkText(string title, string hint)
        {
            EnsureBuilt();
            if (_noWorkTitleLabel != null)
            {
                _noWorkTitleLabel.text = title ?? string.Empty;
            }

            if (_noWorkHintLabel != null)
            {
                _noWorkHintLabel.text = hint ?? string.Empty;
            }
        }

        public void SetOffers(IReadOnlyList<TaxiOfferCard> offers)
        {
            EnsureBuilt();
            if (_offersContainer == null)
            {
                return;
            }

            int count = offers != null ? Mathf.Min(offers.Count, MaxOfferCards) : 0;
            for (int i = 0; i < MaxOfferCards; i++)
            {
                var card = _offerCards[i];
                if (card == null) continue;

                bool visible = i < count;
                card.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible)
                {
                    _displayedOffers[i] = null;
                    continue;
                }

                var data = offers[i];
                _displayedOffers[i] = data.Offer;
                card.PassengerLabel.text = data.Passenger ?? string.Empty;
                card.FareLabel.text = data.Fare ?? string.Empty;
                card.PickupLabel.text = data.Pickup ?? string.Empty;
                card.DestinationLabel.text = data.Destination ?? string.Empty;
                card.DistanceLabel.text = data.Distance ?? string.Empty;
            }

            _offersContainer.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetNoWorkVisible(bool visible)
        {
            EnsureBuilt();
            if (_noWorkCard == null)
            {
                return;
            }

            _noWorkCard.style.display = visible && !_activeVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetActiveRide(TaxiActiveRideCard active)
        {
            EnsureBuilt();
            if (_activeCard == null)
            {
                return;
            }

            _activeVisible = true;
            _activeCard.style.display = DisplayStyle.Flex;
            _offersContainer.style.display = DisplayStyle.None;
            _noWorkCard.style.display = DisplayStyle.None;

            _activePassengerLabel.text = active.Passenger ?? string.Empty;
            _activeFareLabel.text = active.Fare ?? string.Empty;
            _activePickupLabel.text = active.Pickup ?? string.Empty;
            _activeDestinationLabel.text = active.Destination ?? string.Empty;
            _activePhaseLabel.text = active.Phase ?? string.Empty;
            _boardButton.style.display = active.ShowBoardButton ? DisplayStyle.Flex : DisplayStyle.None;
            _collectButton.style.display = active.ShowCollectButton ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ClearActiveRide()
        {
            EnsureBuilt();
            _activeVisible = false;
            if (_activeCard != null)
            {
                _activeCard.style.display = DisplayStyle.None;
            }
        }

        public void SetFeedback(string message)
        {
            EnsureBuilt();
            if (_feedbackLabel == null)
            {
                return;
            }

            bool visible = !string.IsNullOrWhiteSpace(message);
            _feedbackLabel.text = visible ? message : string.Empty;
            _feedbackLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void ApplyTextDirection(bool isRtl)
        {
            _rtl = isRtl;
            if (!_built || _panel == null)
            {
                return;
            }

            _panel.style.left = isRtl ? StyleKeyword.Auto : 18f;
            _panel.style.right = isRtl ? 18f : StyleKeyword.Auto;

            var align = isRtl ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            for (int i = 0; i < _bodyLabels.Count; i++)
            {
                _bodyLabels[i].style.unityTextAlign = align;
            }

            if (_titleLabel != null)
            {
                _titleLabel.style.unityTextAlign = align;
            }

            if (_creditsLabel != null)
            {
                _creditsLabel.style.unityTextAlign = isRtl ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            }

            for (int i = 0; i < _actionRows.Count; i++)
            {
                _actionRows[i].style.flexDirection = isRtl ? FlexDirection.RowReverse : FlexDirection.Row;
            }
        }

        private void RaiseAccept(int cardIndex)
        {
            var offer = _displayedOffers[cardIndex];
            if (offer != null)
            {
                OnAcceptClicked?.Invoke(offer);
            }
        }

        private void RaiseDecline(int cardIndex)
        {
            var offer = _displayedOffers[cardIndex];
            if (offer != null)
            {
                OnDeclineClicked?.Invoke(offer);
            }
        }

        private void BuildTree(VisualElement docRoot)
        {
            docRoot.pickingMode = PickingMode.Ignore;

            var shell = new VisualElement { name = "taxi-shift-shell" };
            shell.pickingMode = PickingMode.Ignore;
            shell.style.position = Position.Absolute;
            shell.style.left = 0;
            shell.style.right = 0;
            shell.style.top = 0;
            shell.style.bottom = 0;
            docRoot.Add(shell);

            _panel = new VisualElement { name = "taxi-shift-panel" };
            _panel.pickingMode = PickingMode.Position;
            _panel.style.position = Position.Absolute;
            _panel.style.left = 18f;
            _panel.style.top = 18f;
            _panel.style.width = PanelWidth;
            _panel.style.flexShrink = 0;
            _panel.style.backgroundColor = PanelBackground;
            _panel.style.borderTopLeftRadius = 10;
            _panel.style.borderTopRightRadius = 10;
            _panel.style.borderBottomLeftRadius = 10;
            _panel.style.borderBottomRightRadius = 10;
            _panel.style.borderLeftWidth = 1;
            _panel.style.borderRightWidth = 1;
            _panel.style.borderTopWidth = 1;
            _panel.style.borderBottomWidth = 1;
            _panel.style.borderLeftColor = CardBorder;
            _panel.style.borderRightColor = CardBorder;
            _panel.style.borderTopColor = CardBorder;
            _panel.style.borderBottomColor = CardBorder;
            _panel.style.paddingLeft = 14;
            _panel.style.paddingRight = 14;
            _panel.style.paddingTop = 12;
            _panel.style.paddingBottom = 12;
            shell.Add(_panel);

            var header = new VisualElement();
            header.pickingMode = PickingMode.Ignore;
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            _titleLabel = CreateLabel("Taxi Shift", 17, Cream, FontStyle.Bold);
            _titleLabel.name = "taxi-title";
            _titleLabel.style.flexGrow = 1;
            header.Add(_titleLabel);

            _creditsLabel = CreateLabel(string.Empty, 14, AccentAmber, FontStyle.Bold);
            _creditsLabel.name = "taxi-credits";
            _creditsLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            header.Add(_creditsLabel);
            _panel.Add(header);

            _panel.Add(CreateDivider());

            _statusLabel = CreateLabel(string.Empty, 11, AccentAmber, FontStyle.Bold);
            _statusLabel.name = "taxi-status";
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginBottom = 8;
            _panel.Add(_statusLabel);

            _feedbackLabel = CreateLabel(string.Empty, 11, Warning, FontStyle.Normal);
            _feedbackLabel.name = "taxi-feedback";
            _feedbackLabel.style.whiteSpace = WhiteSpace.Normal;
            _feedbackLabel.style.marginBottom = 8;
            _feedbackLabel.style.display = DisplayStyle.None;
            _panel.Add(_feedbackLabel);

            _offersContainer = new VisualElement { name = "taxi-offers" };
            _offersContainer.pickingMode = PickingMode.Ignore;
            _panel.Add(_offersContainer);

            for (int i = 0; i < MaxOfferCards; i++)
            {
                _offerCards[i] = CreateOfferCard(i);
                _offersContainer.Add(_offerCards[i].Root);
            }

            CreateNoWorkCard();
            CreateActiveCard();
        }

        private void CreateNoWorkCard()
        {
            _noWorkCard = new VisualElement { name = "taxi-no-work" };
            _noWorkCard.pickingMode = PickingMode.Ignore;
            _noWorkCard.style.backgroundColor = CardBackground;
            _noWorkCard.style.paddingLeft = 12;
            _noWorkCard.style.paddingRight = 12;
            _noWorkCard.style.paddingTop = 10;
            _noWorkCard.style.paddingBottom = 10;
            _noWorkCard.style.borderTopLeftRadius = 8;
            _noWorkCard.style.borderTopRightRadius = 8;
            _noWorkCard.style.borderBottomLeftRadius = 8;
            _noWorkCard.style.borderBottomRightRadius = 8;
            _noWorkCard.style.display = DisplayStyle.None;

            _noWorkTitleLabel = CreateLabel(string.Empty, 13, Cream, FontStyle.Bold);
            _noWorkTitleLabel.name = "taxi-no-work-title";
            _noWorkTitleLabel.style.marginBottom = 3;
            _noWorkCard.Add(_noWorkTitleLabel);

            _noWorkHintLabel = CreateLabel(string.Empty, 11, Muted, FontStyle.Normal);
            _noWorkHintLabel.name = "taxi-no-work-hint";
            _noWorkHintLabel.style.whiteSpace = WhiteSpace.Normal;
            _noWorkCard.Add(_noWorkHintLabel);

            _panel.Add(_noWorkCard);
        }

        private void CreateActiveCard()
        {
            _activeCard = new VisualElement { name = "taxi-active-ride" };
            _activeCard.pickingMode = PickingMode.Ignore;
            _activeCard.style.backgroundColor = ActiveBackground;
            _activeCard.style.paddingLeft = 12;
            _activeCard.style.paddingRight = 12;
            _activeCard.style.paddingTop = 10;
            _activeCard.style.paddingBottom = 10;
            _activeCard.style.borderTopLeftRadius = 8;
            _activeCard.style.borderTopRightRadius = 8;
            _activeCard.style.borderBottomLeftRadius = 8;
            _activeCard.style.borderBottomRightRadius = 8;
            _activeCard.style.borderLeftWidth = 1;
            _activeCard.style.borderRightWidth = 1;
            _activeCard.style.borderTopWidth = 1;
            _activeCard.style.borderBottomWidth = 1;
            _activeCard.style.borderLeftColor = AccentAmber;
            _activeCard.style.borderRightColor = AccentAmber;
            _activeCard.style.borderTopColor = AccentAmber;
            _activeCard.style.borderBottomColor = AccentAmber;
            _activeCard.style.display = DisplayStyle.None;

            _activeTitleLabel = CreateLabel(string.Empty, 11, AccentAmber, FontStyle.Bold);
            _activeTitleLabel.name = "taxi-active-title";
            _activeTitleLabel.style.marginBottom = 4;
            _activeCard.Add(_activeTitleLabel);

            var nameRow = new VisualElement();
            nameRow.pickingMode = PickingMode.Ignore;
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.justifyContent = Justify.SpaceBetween;
            nameRow.style.alignItems = Align.Center;

            _activePassengerLabel = CreateLabel(string.Empty, 16, Cream, FontStyle.Bold);
            _activePassengerLabel.name = "taxi-active-passenger";
            _activePassengerLabel.style.flexGrow = 1;
            _activePassengerLabel.style.whiteSpace = WhiteSpace.Normal;
            nameRow.Add(_activePassengerLabel);

            _activeFareLabel = CreateLabel(string.Empty, 13, AccentAmber, FontStyle.Bold);
            _activeFareLabel.name = "taxi-active-fare";
            nameRow.Add(_activeFareLabel);
            _activeCard.Add(nameRow);

            _activePickupLabel = CreateLabel(string.Empty, 11, Muted, FontStyle.Normal);
            _activePickupLabel.name = "taxi-active-pickup";
            _activePickupLabel.style.whiteSpace = WhiteSpace.Normal;
            _activePickupLabel.style.marginTop = 4;
            _activeCard.Add(_activePickupLabel);

            _activeDestinationLabel = CreateLabel(string.Empty, 11, Muted, FontStyle.Normal);
            _activeDestinationLabel.name = "taxi-active-destination";
            _activeDestinationLabel.style.whiteSpace = WhiteSpace.Normal;
            _activeDestinationLabel.style.marginTop = 2;
            _activeCard.Add(_activeDestinationLabel);

            _activePhaseLabel = CreateLabel(string.Empty, 12, AccentAmber, FontStyle.Bold);
            _activePhaseLabel.name = "taxi-active-phase";
            _activePhaseLabel.style.whiteSpace = WhiteSpace.Normal;
            _activePhaseLabel.style.marginTop = 8;
            _activePhaseLabel.style.marginBottom = 8;
            _activeCard.Add(_activePhaseLabel);

            var actionRow = new VisualElement();
            actionRow.pickingMode = PickingMode.Ignore;
            actionRow.style.flexDirection = FlexDirection.Row;
            _actionRows.Add(actionRow);

            _boardButton = CreateActionButton("Board passenger", AccentAmber, ButtonTextDark);
            _boardButton.name = "taxi-board";
            _boardButton.style.marginRight = 6;
            _boardButton.style.display = DisplayStyle.None;
            _boardButton.clicked += HandleBoardClicked;
            actionRow.Add(_boardButton);

            _collectButton = CreateActionButton("Collect fare", AccentAmber, ButtonTextDark);
            _collectButton.name = "taxi-collect";
            _collectButton.style.display = DisplayStyle.None;
            _collectButton.clicked += HandleCollectClicked;
            actionRow.Add(_collectButton);

            _activeCard.Add(actionRow);
            _panel.Add(_activeCard);
        }

        private OfferCardElements CreateOfferCard(int index)
        {
            var root = new VisualElement { name = "taxi-offer-" + index };
            root.pickingMode = PickingMode.Position;
            root.style.backgroundColor = CardBackground;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 9;
            root.style.paddingBottom = 9;
            root.style.marginBottom = 8;
            root.style.borderTopLeftRadius = 8;
            root.style.borderTopRightRadius = 8;
            root.style.borderBottomLeftRadius = 8;
            root.style.borderBottomRightRadius = 8;
            root.style.borderLeftWidth = 1;
            root.style.borderRightWidth = 1;
            root.style.borderTopWidth = 1;
            root.style.borderBottomWidth = 1;
            root.style.borderLeftColor = CardBorder;
            root.style.borderRightColor = CardBorder;
            root.style.borderTopColor = CardBorder;
            root.style.borderBottomColor = CardBorder;

            var topRow = new VisualElement();
            topRow.pickingMode = PickingMode.Ignore;
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.justifyContent = Justify.SpaceBetween;
            topRow.style.alignItems = Align.Center;

            var passengerLabel = CreateLabel(string.Empty, 15, Cream, FontStyle.Bold);
            passengerLabel.style.flexGrow = 1;
            passengerLabel.style.whiteSpace = WhiteSpace.Normal;
            topRow.Add(passengerLabel);

            var fareLabel = CreateLabel(string.Empty, 13, AccentAmber, FontStyle.Bold);
            topRow.Add(fareLabel);
            root.Add(topRow);

            var pickupLabel = CreateLabel(string.Empty, 11, Muted, FontStyle.Normal);
            pickupLabel.style.whiteSpace = WhiteSpace.Normal;
            pickupLabel.style.marginTop = 4;
            root.Add(pickupLabel);

            var destinationLabel = CreateLabel(string.Empty, 11, Muted, FontStyle.Normal);
            destinationLabel.style.whiteSpace = WhiteSpace.Normal;
            destinationLabel.style.marginTop = 2;
            root.Add(destinationLabel);

            var distanceLabel = CreateLabel(string.Empty, 10, Faint, FontStyle.Normal);
            distanceLabel.style.marginTop = 3;
            root.Add(distanceLabel);

            var actionRow = new VisualElement();
            actionRow.pickingMode = PickingMode.Ignore;
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginTop = 8;
            _actionRows.Add(actionRow);

            var acceptButton = CreateActionButton("Accept", AccentAmber, ButtonTextDark);
            acceptButton.name = "taxi-offer-" + index + "-accept";
            acceptButton.style.marginRight = 6;
            acceptButton.clicked += () => RaiseAccept(index);
            actionRow.Add(acceptButton);

            var declineButton = CreateActionButton("Decline", new Color(0f, 0f, 0f, 0f), Cream);
            declineButton.name = "taxi-offer-" + index + "-decline";
            declineButton.style.borderLeftWidth = 1;
            declineButton.style.borderRightWidth = 1;
            declineButton.style.borderTopWidth = 1;
            declineButton.style.borderBottomWidth = 1;
            declineButton.style.borderLeftColor = Muted;
            declineButton.style.borderRightColor = Muted;
            declineButton.style.borderTopColor = Muted;
            declineButton.style.borderBottomColor = Muted;
            declineButton.clicked += () => RaiseDecline(index);
            actionRow.Add(declineButton);

            root.Add(actionRow);

            return new OfferCardElements(
                root,
                passengerLabel,
                fareLabel,
                pickupLabel,
                destinationLabel,
                distanceLabel,
                acceptButton,
                declineButton);
        }

        private void HandleBoardClicked()
        {
            OnBoardClicked?.Invoke();
        }

        private void HandleCollectClicked()
        {
            OnCollectClicked?.Invoke();
        }

        private VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.pickingMode = PickingMode.Ignore;
            divider.style.height = 1;
            divider.style.backgroundColor = Divider;
            divider.style.marginTop = 8;
            divider.style.marginBottom = 9;
            return divider;
        }

        private Label CreateLabel(string text, int fontSize, Color color, FontStyle weight)
        {
            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = weight;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            _bodyLabels.Add(label);
            return label;
        }

        private Button CreateActionButton(string text, Color background, Color textColor)
        {
            var button = new Button { text = text };
            button.pickingMode = PickingMode.Position;
            button.style.flexGrow = 1;
            button.style.height = 28;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.marginTop = 0;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.backgroundColor = background;
            button.style.color = textColor;
            button.style.fontSize = 12;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            return button;
        }

        private sealed class OfferCardElements
        {
            public readonly VisualElement Root;
            public readonly Label PassengerLabel;
            public readonly Label FareLabel;
            public readonly Label PickupLabel;
            public readonly Label DestinationLabel;
            public readonly Label DistanceLabel;
            public readonly Button AcceptButton;
            public readonly Button DeclineButton;

            public OfferCardElements(
                VisualElement root,
                Label passengerLabel,
                Label fareLabel,
                Label pickupLabel,
                Label destinationLabel,
                Label distanceLabel,
                Button acceptButton,
                Button declineButton)
            {
                Root = root;
                PassengerLabel = passengerLabel;
                FareLabel = fareLabel;
                PickupLabel = pickupLabel;
                DestinationLabel = destinationLabel;
                DistanceLabel = distanceLabel;
                AcceptButton = acceptButton;
                DeclineButton = declineButton;
            }
        }
    }
}
