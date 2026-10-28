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

        /// <summary>Returns the current valid offer, generating one when none exists.</summary>
        bool TryEnsureOffer(out RideOffer offer);

        /// <summary>Generates a fresh offer from currently eligible world state.</summary>
        bool TryGenerateOffer(out RideOffer offer);

        /// <summary>Returns all currently eligible offers (used by the future offer HUD/tests).</summary>
        IReadOnlyList<RideOffer> GetEligibleOffers();

        /// <summary>Consumes the current offer after player acceptance.</summary>
        bool AcceptOffer(RideOffer offer);

        /// <summary>Consumes the current offer and suppresses that exact job for the cooldown.</summary>
        bool DeclineOffer(RideOffer offer);

        /// <summary>Expires an ignored offer and suppresses that exact job for the cooldown.</summary>
        bool ExpireIfNeeded();
    }
}
