using UnityEngine;

namespace OutsiderDriver.Vehicles
{
    /// <summary>
    /// Swaps the authored isometric vehicle sprite for the current cardinal ground direction.
    /// Isometric 90-degree turns are not screen rotations, so each direction needs its own sprite.
    /// </summary>
    [DisallowMultipleComponent]
    public class VehicleDirectionalVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite _northEast;
        [SerializeField] private Sprite _southEast;
        [SerializeField] private Sprite _southWest;
        [SerializeField] private Sprite _northWest;

        private VehicleController _vehicle;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
                if (_renderer == null)
                {
                    _renderer = GetComponentInChildren<SpriteRenderer>();
                }
            }

            _vehicle = GetComponentInParent<VehicleController>();
        }

        private void OnEnable()
        {
            if (_vehicle == null)
            {
                _vehicle = GetComponentInParent<VehicleController>();
            }

            if (_vehicle != null)
            {
                _vehicle.OnFacingChanged += ApplyFacing;
                ApplyFacing(_vehicle.CurrentFacing);
            }
        }

        private void OnDisable()
        {
            if (_vehicle != null)
            {
                _vehicle.OnFacingChanged -= ApplyFacing;
            }
        }

        private void ApplyFacing(IsometricDirection facing)
        {
            Sprite sprite = facing switch
            {
                IsometricDirection.NorthEast => _northEast,
                IsometricDirection.SouthEast => _southEast,
                IsometricDirection.SouthWest => _southWest,
                IsometricDirection.NorthWest => _northWest,
                _ => _northEast
            };

            if (sprite != null && _renderer != null)
            {
                _renderer.sprite = sprite;
            }
        }
    }
}
