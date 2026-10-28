using System;
using System.Collections.Generic;

namespace OutsiderDriver.Rides
{
    /// <summary>
    /// Systemic ordinary-work source over authored passengers, time windows, locations and the road graph.
    /// It owns generated offers only; RideLifecycleService remains the single active-ride state machine.
    /// </summary>
    public interface IRideDemandService
    {
        RideOffer CurrentOffer { get; }
        event Action<IReadOnlyList<RideOffer>> OnOffersChanged;

        /// <summary>Returns the current valid offer, generating one when none exists.</summary>
        bool TryEnsureOffer(out RideOffer offer);

        /// <summary>Forces a fresh stable offer board for explicit player refresh/testing.</summary>
        bool TryGenerateOffer(out RideOffer offer);

        /// <summary>Returns the stable current offer board. Repeated calls do not mint new IDs.</summary>
        IReadOnlyList<RideOffer> GetEligibleOffers();

        /// <summary>Prunes expired offers and generates a board only when none remains.</summary>
        void RefreshOffers();

        /// <summary>Reserves one exact displayed offer before ride activation.</summary>
        bool TryReserveOffer(RideOffer offer);

        /// <summary>Returns a reserved offer to the board after failed ride activation.</summary>
        bool ReleaseReservedOffer(RideOffer offer);

        /// <summary>Consumes the exact reserved offer after a ride starts successfully.</summary>
        bool ConsumeReservedOffer(RideOffer offer);

        /// <summary>Consumes one exact board offer directly (legacy/test helper).</summary>
        bool AcceptOffer(RideOffer offer);

        /// <summary>Consumes one exact board offer and suppresses that job for the cooldown.</summary>
        bool DeclineOffer(RideOffer offer);

        /// <summary>Expires ignored/stale offers without generating replacements.</summary>
        bool ExpireIfNeeded();
    }
}
