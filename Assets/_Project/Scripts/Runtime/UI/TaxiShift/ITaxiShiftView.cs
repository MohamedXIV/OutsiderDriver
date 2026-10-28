using System;
using System.Collections.Generic;
using OutsiderDriver.Rides;

namespace OutsiderDriver.UI.TaxiShift
{
    /// <summary>
    /// Passive view contract for the player-operated taxi shift HUD (Issue #20).
    /// It only renders presentation data pushed by TaxiShiftPresenter and forwards clicks.
    /// </summary>
    public interface ITaxiShiftView
    {
        event Action<RideOffer> OnAcceptClicked;
        event Action<RideOffer> OnDeclineClicked;
        event Action OnBoardClicked;
        event Action OnCollectClicked;

        void EnsureBuilt();
        void SetTitle(string title);
        void SetCredits(string formattedCredits);
        void SetStatus(string status, bool attention);
        void SetButtonLabels(string accept, string decline, string board, string collect);
        void SetActiveTitle(string title);
        void SetNoWorkText(string title, string hint);
        void SetOffers(IReadOnlyList<TaxiOfferCard> offers);
        void SetNoWorkVisible(bool visible);
        void SetActiveRide(TaxiActiveRideCard active);
        void ClearActiveRide();
        void SetFeedback(string message);
        void ApplyTextDirection(bool isRtl);
    }

    /// <summary>One generated offer rendered as a compact, selectable HUD card.</summary>
    public readonly struct TaxiOfferCard
    {
        public readonly RideOffer Offer;
        public readonly string Passenger;
        public readonly string Fare;
        public readonly string Pickup;
        public readonly string Destination;
        public readonly string Distance;

        public TaxiOfferCard(
            RideOffer offer,
            string passenger,
            string fare,
            string pickup,
            string destination,
            string distance)
        {
            Offer = offer;
            Passenger = passenger;
            Fare = fare;
            Pickup = pickup;
            Destination = destination;
            Distance = distance;
        }
    }

    /// <summary>Authoritative active-ride state rendered by the HUD, including the two player actions.</summary>
    public readonly struct TaxiActiveRideCard
    {
        public readonly string Passenger;
        public readonly string Fare;
        public readonly string Pickup;
        public readonly string Destination;
        public readonly string Phase;
        public readonly bool ShowBoardButton;
        public readonly bool ShowCollectButton;

        public TaxiActiveRideCard(
            string passenger,
            string fare,
            string pickup,
            string destination,
            string phase,
            bool showBoardButton,
            bool showCollectButton)
        {
            Passenger = passenger;
            Fare = fare;
            Pickup = pickup;
            Destination = destination;
            Phase = phase;
            ShowBoardButton = showBoardButton;
            ShowCollectButton = showCollectButton;
        }
    }
}
