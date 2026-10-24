using UnityEngine;

namespace OutsiderDriver.Navigation
{
    /// <summary>
    /// Converts between Unity world space and the authored isometric ground plane.
    /// One ground unit is one logical planning cell, approved as one metre.
    /// The basis is sampled from the live Grid, so it follows the native IsometricZAsY projection
    /// instead of assuming that world units are metres.
    /// </summary>
    public readonly struct IsometricGroundProjection
    {
        private readonly Vector2 _origin;
        private readonly Vector2 _axisX;
        private readonly Vector2 _axisY;
        private readonly float _determinant;

        public IsometricGroundProjection(Vector2 origin, Vector2 axisX, Vector2 axisY)
        {
            _origin = origin;
            _axisX = axisX;
            _axisY = axisY;
            _determinant = axisX.x * axisY.y - axisY.x * axisX.y;
        }

        /// <summary>
        /// Fallback matching the production IsometricZAsY grid when no Grid is available (tests, isolated scenes).
        /// </summary>
        public static IsometricGroundProjection Default => new IsometricGroundProjection(
            Vector2.zero,
            new Vector2(0.5f, 0.25f),
            new Vector2(-0.5f, 0.25f));

        public bool IsValid => Mathf.Abs(_determinant) > 0.000001f;
        public Vector2 Origin => _origin;
        public Vector2 AxisX => _axisX;
        public Vector2 AxisY => _axisY;

        public static IsometricGroundProjection FromGrid(Grid grid)
        {
            if (grid == null)
            {
                return Default;
            }

            Vector3 origin = grid.CellToWorld(Vector3Int.zero);
            Vector3 axisX = grid.CellToWorld(new Vector3Int(1, 0, 0)) - origin;
            Vector3 axisY = grid.CellToWorld(new Vector3Int(0, 1, 0)) - origin;

            return new IsometricGroundProjection(
                new Vector2(origin.x, origin.y),
                new Vector2(axisX.x, axisX.y),
                new Vector2(axisY.x, axisY.y));
        }

        public Vector2 WorldToGround(Vector2 world) => WorldDeltaToGround(world - _origin);

        public Vector2 WorldDeltaToGround(Vector2 worldDelta)
        {
            if (!IsValid)
            {
                return Vector2.zero;
            }

            float inverseDeterminant = 1f / _determinant;
            return new Vector2(
                (worldDelta.x * _axisY.y - worldDelta.y * _axisY.x) * inverseDeterminant,
                (_axisX.x * worldDelta.y - _axisX.y * worldDelta.x) * inverseDeterminant);
        }

        public Vector2 GroundToWorld(Vector2 ground) => _origin + GroundDeltaToWorld(ground);

        public Vector2 GroundDeltaToWorld(Vector2 groundDelta) => _axisX * groundDelta.x + _axisY * groundDelta.y;

        public float Distance(Vector2 worldA, Vector2 worldB)
        {
            return (WorldToGround(worldA) - WorldToGround(worldB)).magnitude;
        }

        public float SqrDistance(Vector2 worldA, Vector2 worldB)
        {
            return (WorldToGround(worldA) - WorldToGround(worldB)).sqrMagnitude;
        }
    }
}
