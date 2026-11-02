using UnityEngine;

namespace OutsiderDriver.Vehicles
{
    /// <summary>Stateless longitudinal policy; gap is bumper-to-bumper ground metres.</summary>
    public static class CarFollowing
    {
        public static float TargetSpeed(float cruiseSpeed, float gap, float leadSpeed,
            float leadDeceleration, float deceleration, float minimumGap, float headway)
        {
            float available = Mathf.Max(0f, gap - minimumGap);
            float brake = Mathf.Max(0.1f, deceleration);
            float time = Mathf.Max(0.1f, headway);
            // v*T + v²/(2*b) <= available + lead stopping distance.
            // Treat a paused/idle leader as stationary at the call site.
            float stoppingBudget = available + leadSpeed * leadSpeed / (2f * Mathf.Max(0.1f, leadDeceleration));
            float safeSpeed = Mathf.Sqrt(brake * brake * time * time + 2f * brake * stoppingBudget) - brake * time;
            // Stable equilibrium at minimumGap + speed*headway; no binary stop/release threshold.
            return Mathf.Max(0f, Mathf.Min(cruiseSpeed, Mathf.Min(safeSpeed, available / time)));
        }
    }
}
