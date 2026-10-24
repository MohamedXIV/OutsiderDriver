using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using VContainer;
using OutsiderDriver.Core.Persistence;
using OutsiderDriver.Core.State;
using OutsiderDriver.Core.Time;
using OutsiderDriver.Navigation;
using OutsiderDriver.Vehicles;
using OutsiderDriver.World;

namespace OutsiderDriver.Player
{
    /// <summary>
    /// Controls the human protagonist's bounded 2D isometric on-foot walking, domestic/neighborhood
    /// interactions, boarding/exiting the taxi, and Cinemachine 3 virtual camera priority switching
    /// (Decisions P-009, P-014, P-025).
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        public const int ActiveCameraPriority = 20;
        public const int InactiveCameraPriority = 5;

        [Header("Scene References")]
        [SerializeField] private HomeNeighborhoodLayout _neighborhoodLayout;
        [SerializeField] private VehicleController _playerTaxi;
        [SerializeField] private TrafficDirector _trafficDirector;
        [SerializeField] private GameObject _visualsRoot;

        [Header("Cinemachine 3 Virtual Cameras")]
        [SerializeField] private CinemachineCamera _taxiCamera;
        [SerializeField] private CinemachineCamera _onFootCamera;

        [Header("Movement & Interaction Settings")]
        [Min(0.5f)]
        [SerializeField] private float _walkSpeed = 3.2f;
        [Min(0.5f)]
        [SerializeField] private float _taxiBoardingRadius = 1.75f;
        [Min(15)]
        [SerializeField] private int _restDurationMinutes = 360;
        [SerializeField] private DriverMode _initialMode = DriverMode.InTaxi;

        private ITimeService _timeService;
        private ISaveService _saveService;
        private IPlayerStateService _playerStateService;
        private DriverMode _currentMode = DriverMode.InTaxi;
        private IsometricDirection _currentFacing = IsometricDirection.SouthEast;
        private WalkableZone _currentWalkableZone;
        private NeighborhoodInteractable _nearestInteractable;

        public DriverMode CurrentMode => _currentMode;
        public IsometricDirection CurrentFacing => _currentFacing;
        public WalkableZone CurrentWalkableZone => _currentWalkableZone;
        public NeighborhoodInteractable NearestInteractable => _nearestInteractable;
        public Vector2 Position2D => new Vector2(transform.position.x, transform.position.y);
        public ISaveService SaveService => _saveService;
        public IPlayerStateService PlayerStateService => _playerStateService;

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = Mathf.Max(0.5f, value);
        }

        public float TaxiBoardingRadius
        {
            get => _taxiBoardingRadius;
            set => _taxiBoardingRadius = Mathf.Max(0.5f, value);
        }

        public int RestDurationMinutes
        {
            get => _restDurationMinutes;
            set => _restDurationMinutes = Mathf.Max(15, value);
        }

        public CinemachineCamera TaxiCamera => _taxiCamera;
        public CinemachineCamera OnFootCamera => _onFootCamera;

        public event Action<DriverMode> OnModeChanged;
        public event Action<IsometricDirection> OnFacingChanged;
        public event Action<NeighborhoodInteractable> OnInteracted;

        [Inject]
        public void Construct(
            HomeNeighborhoodLayout neighborhoodLayout = null,
            VehicleController playerTaxi = null,
            TrafficDirector trafficDirector = null,
            ITimeService timeService = null,
            ISaveService saveService = null,
            IPlayerStateService playerStateService = null)
        {
            if (neighborhoodLayout != null)
            {
                _neighborhoodLayout = neighborhoodLayout;
            }

            if (playerTaxi != null)
            {
                _playerTaxi = playerTaxi;
            }

            if (trafficDirector != null)
            {
                _trafficDirector = trafficDirector;
            }

            if (timeService != null)
            {
                _timeService = timeService;
            }

            if (saveService != null)
            {
                SetSaveService(saveService);
            }

            if (playerStateService != null)
            {
                _playerStateService = playerStateService;
            }
        }

        public void SetSaveService(ISaveService saveService)
        {
            if (_saveService != null)
            {
                _saveService.OnLoadCompleted -= HandleSaveLoaded;
            }

            _saveService = saveService;

            if (_saveService != null)
            {
                _saveService.OnLoadCompleted += HandleSaveLoaded;
            }
        }

        private void OnDestroy()
        {
            if (_saveService != null)
            {
                _saveService.OnLoadCompleted -= HandleSaveLoaded;
            }
        }

        public void AssignCameras(CinemachineCamera taxiCamera, CinemachineCamera onFootCamera)
        {
            _taxiCamera = taxiCamera;
            _onFootCamera = onFootCamera;
            ApplyCameraPrioritiesAndVisibility();
        }

        public void AssignVisualsRoot(GameObject visualsRoot)
        {
            _visualsRoot = visualsRoot;
            ApplyCameraPrioritiesAndVisibility();
        }

        private void Awake()
        {
            _currentMode = _initialMode;
            ApplyCameraPrioritiesAndVisibility();
        }

        private void Start()
        {
            if (_neighborhoodLayout == null)
            {
                _neighborhoodLayout = FindAnyObjectByType<HomeNeighborhoodLayout>();
            }

            if (_playerTaxi == null)
            {
                _playerTaxi = FindAnyObjectByType<VehicleController>();
            }

            if (_trafficDirector == null)
            {
                _trafficDirector = FindAnyObjectByType<TrafficDirector>();
            }

            ApplyCameraPrioritiesAndVisibility();
            RefreshCurrentZoneAndInteractable();
            SyncWorldStateToPlayerState();
        }

        private void Update()
        {
            Vector2 moveInput = ReadMoveInput();
            if (_currentMode == DriverMode.OnFoot)
            {
                TickMovement(moveInput, Time.deltaTime);
            }

            if (WasInteractPressedThisFrame())
            {
                if (_currentMode == DriverMode.InTaxi)
                {
                    TryExitTaxi();
                }
                else
                {
                    if (!TryInteractWithNearest())
                    {
                        TryBoardTaxi();
                    }
                }
            }
        }

        /// <summary>
        /// Forces the driver into the specified mode and updates camera priorities, visibility, and traffic bubble center.
        /// </summary>
        public void SetModeImmediate(DriverMode mode, Vector2? onFootPositionOverride = null)
        {
            if (onFootPositionOverride.HasValue)
            {
                Vector2 target = onFootPositionOverride.Value;
                transform.position = new Vector3(target.x, target.y, 0f);
            }

            bool changed = _currentMode != mode;
            _currentMode = mode;

            ApplyCameraPrioritiesAndVisibility();
            RefreshCurrentZoneAndInteractable();

            if (changed)
            {
                SyncWorldStateToPlayerState();
                OnModeChanged?.Invoke(_currentMode);
            }
        }

        /// <summary>
        /// Advances 2D isometric on-foot movement constrained strictly to authorized WalkableZones (Decision P-009).
        /// Publicly callable for deterministic EditMode/PlayMode verification.
        /// </summary>
        public void TickMovement(Vector2 moveInput, float deltaTime)
        {
            if (_currentMode != DriverMode.OnFoot || deltaTime <= 0f)
            {
                return;
            }

            if (moveInput.sqrMagnitude > 0.0001f)
            {
                Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
                UpdateIsometricFacing(clampedInput);

                Vector2 currentPos = Position2D;
                Vector2 delta = clampedInput * (_walkSpeed * deltaTime);
                Vector2 candidatePos = currentPos + delta;

                if (CanMoveToPosition(candidatePos, out WalkableZone validZone))
                {
                    transform.position = new Vector3(candidatePos.x, candidatePos.y, 0f);
                    _currentWalkableZone = validZone;
                }
                else
                {
                    // Per-axis sliding along walkable boundaries and walls
                    Vector2 slideX = new Vector2(currentPos.x + delta.x, currentPos.y);
                    Vector2 slideY = new Vector2(currentPos.x, currentPos.y + delta.y);

                    if (Mathf.Abs(delta.x) > 0.0001f && CanMoveToPosition(slideX, out WalkableZone zoneX))
                    {
                        transform.position = new Vector3(slideX.x, slideX.y, 0f);
                        _currentWalkableZone = zoneX;
                    }
                    else if (Mathf.Abs(delta.y) > 0.0001f && CanMoveToPosition(slideY, out WalkableZone zoneY))
                    {
                        transform.position = new Vector3(slideY.x, slideY.y, 0f);
                        _currentWalkableZone = zoneY;
                    }
                }
            }

            RefreshCurrentZoneAndInteractable();
        }

        /// <summary>
        /// Attempts to exit the taxi onto the starter neighborhood sidewalk/garage when parked within the walkable home neighborhood.
        /// </summary>
        public bool TryExitTaxi()
        {
            if (_currentMode != DriverMode.InTaxi || _neighborhoodLayout == null)
            {
                return false;
            }

            Vector2 exitPosition = ResolveValidTaxiExitPosition(out WalkableZone targetZone);
            if (targetZone == null)
            {
                return false;
            }

            if (_playerTaxi != null)
            {
                _playerTaxi.Stop();
            }

            transform.position = new Vector3(exitPosition.x, exitPosition.y, 0f);
            _currentWalkableZone = targetZone;
            _currentMode = DriverMode.OnFoot;

            ApplyCameraPrioritiesAndVisibility();
            RefreshCurrentZoneAndInteractable();
            SyncWorldStateToPlayerState();
            OnModeChanged?.Invoke(_currentMode);
            return true;
        }

        /// <summary>
        /// Attempts to board the parked taxi from on-foot mode when within boarding distance.
        /// </summary>
        public bool TryBoardTaxi()
        {
            if (_currentMode != DriverMode.OnFoot)
            {
                return false;
            }

            if (!IsWithinTaxiBoardingRange())
            {
                return false;
            }

            _currentMode = DriverMode.InTaxi;
            _nearestInteractable = null;

            if (_playerTaxi != null)
            {
                transform.position = _playerTaxi.transform.position;
                _playerTaxi.Resume();
            }

            ApplyCameraPrioritiesAndVisibility();
            SyncWorldStateToPlayerState();
            OnModeChanged?.Invoke(_currentMode);
            return true;
        }

        /// <summary>
        /// Interacts with the closest NeighborhoodInteractable in range (bed, doorway threshold, workbench, or taxi bay).
        /// </summary>
        public bool TryInteractWithNearest()
        {
            if (_currentMode != DriverMode.OnFoot || _neighborhoodLayout == null)
            {
                return false;
            }

            RefreshCurrentZoneAndInteractable();
            if (_nearestInteractable == null)
            {
                return false;
            }

            NeighborhoodInteractable target = _nearestInteractable;
            if (!target.TryInteract(Position2D))
            {
                return false;
            }

            ExecuteInteractableBehavior(target);
            OnInteracted?.Invoke(target);
            RefreshCurrentZoneAndInteractable();
            return true;
        }

        private void ExecuteInteractableBehavior(NeighborhoodInteractable interactable)
        {
            switch (interactable.Kind)
            {
                case InteractableKind.ParkedTaxiBay:
                    TryBoardTaxi();
                    break;

                case InteractableKind.DoorwayThreshold:
                    TransitionThroughDoorway();
                    break;

                case InteractableKind.HomeBed:
                    RestAtHomeBed();
                    break;
            }
        }

        private void TransitionThroughDoorway()
        {
            if (_neighborhoodLayout == null) return;

            Transform homeSpawn = _neighborhoodLayout.HomeInteriorSpawnPoint;
            Transform sidewalkSpawn = _neighborhoodLayout.StreetSidewalkSpawnPoint;
            if (homeSpawn == null && sidewalkSpawn == null) return;

            bool currentlyInsideHome = _currentWalkableZone != null &&
                                       _currentWalkableZone.ZoneType == WalkableZoneType.HomeInterior;

            Transform destinationAnchor = currentlyInsideHome
                ? (sidewalkSpawn != null ? sidewalkSpawn : homeSpawn)
                : (homeSpawn != null ? homeSpawn : sidewalkSpawn);

            if (destinationAnchor == null) return;

            Vector2 dest2D = new Vector2(destinationAnchor.position.x, destinationAnchor.position.y);
            if (_neighborhoodLayout.CanWalkAt(dest2D, out WalkableZone destZone))
            {
                transform.position = new Vector3(dest2D.x, dest2D.y, 0f);
                _currentWalkableZone = destZone;
            }
        }

        private void RestAtHomeBed()
        {
            if (_timeService != null)
            {
                float realSecondsToAdvance = _restDurationMinutes * _timeService.RealSecondsPerInGameMinute;
                _timeService.AdvanceTime(realSecondsToAdvance);
            }

            SyncWorldStateToPlayerState();

            _saveService?.Save();
        }

        /// <summary>
        /// Synchronizes current player position, mode, and facing to the IPlayerStateService.
        /// </summary>
        public void SyncWorldStateToPlayerState()
        {
            if (_playerStateService != null)
            {
                Vector2 currentPos = _currentMode == DriverMode.InTaxi && _playerTaxi != null
                    ? new Vector2(_playerTaxi.transform.position.x, _playerTaxi.transform.position.y)
                    : Position2D;

                _playerStateService.UpdateWorldPosition(currentPos, _currentMode.ToString(), _currentFacing.ToString());
            }
        }

        private void HandleSaveLoaded(SaveStateData loadedData)
        {
            if (loadedData != null && loadedData.World != null)
            {
                RestoreWorldState(loadedData.World);
            }
        }

        /// <summary>
        /// Restores driver mode, position, and facing from persistent player world state.
        /// </summary>
        public void RestoreWorldState(PlayerWorldStateData worldState)
        {
            if (worldState == null) return;

            DriverMode targetMode = string.Equals(worldState.DriverMode, nameof(DriverMode.OnFoot), StringComparison.OrdinalIgnoreCase)
                ? DriverMode.OnFoot
                : DriverMode.InTaxi;

            Vector2 targetPos = new Vector2(worldState.PositionX, worldState.PositionY);

            if (targetMode == DriverMode.OnFoot)
            {
                SetModeImmediate(DriverMode.OnFoot, targetPos);
            }
            else
            {
                if (_playerTaxi != null && targetPos.sqrMagnitude > 0.0001f)
                {
                    _playerTaxi.transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
                }
                SetModeImmediate(DriverMode.InTaxi);
            }

            if (Enum.TryParse<IsometricDirection>(worldState.Facing, true, out var facing))
            {
                _currentFacing = facing;
                OnFacingChanged?.Invoke(_currentFacing);
            }
        }

        private Vector2 ResolveValidTaxiExitPosition(out WalkableZone validZone)
        {
            validZone = null;

            // 1. Check if the taxi itself is currently inside or adjacent to a WalkableZone
            if (_playerTaxi != null)
            {
                Vector2 taxiPos = new Vector2(_playerTaxi.transform.position.x, _playerTaxi.transform.position.y);
                if (_neighborhoodLayout.CanWalkAt(taxiPos, out validZone))
                {
                    return taxiPos;
                }

                // Check if taxi is near the ParkedTaxiBayPoint
                if (_neighborhoodLayout.ParkedTaxiBayPoint != null)
                {
                    Vector2 bayPos = new Vector2(
                        _neighborhoodLayout.ParkedTaxiBayPoint.position.x,
                        _neighborhoodLayout.ParkedTaxiBayPoint.position.y);

                    if ((taxiPos - bayPos).sqrMagnitude <= (_taxiBoardingRadius * 2f) * (_taxiBoardingRadius * 2f) &&
                        _neighborhoodLayout.CanWalkAt(bayPos, out validZone))
                    {
                        return bayPos;
                    }
                }

                // Taxi is out in the city away from the bounded walkable neighborhood
                return Vector2.zero;
            }

            // 2. Fallback when no taxi reference is assigned: use ParkedTaxiBayPoint or StreetSidewalkSpawnPoint
            if (_neighborhoodLayout.ParkedTaxiBayPoint != null)
            {
                Vector2 bayPos = new Vector2(
                    _neighborhoodLayout.ParkedTaxiBayPoint.position.x,
                    _neighborhoodLayout.ParkedTaxiBayPoint.position.y);

                if (_neighborhoodLayout.CanWalkAt(bayPos, out validZone))
                {
                    return bayPos;
                }
            }

            if (_neighborhoodLayout.StreetSidewalkSpawnPoint != null)
            {
                Vector2 sidewalkPos = new Vector2(
                    _neighborhoodLayout.StreetSidewalkSpawnPoint.position.x,
                    _neighborhoodLayout.StreetSidewalkSpawnPoint.position.y);

                if (_neighborhoodLayout.CanWalkAt(sidewalkPos, out validZone))
                {
                    return sidewalkPos;
                }
            }

            return Vector2.zero;
        }

        private bool IsWithinTaxiBoardingRange()
        {
            float maxDistSqr = _taxiBoardingRadius * _taxiBoardingRadius;
            Vector2 currentPos = Position2D;

            if (_playerTaxi != null)
            {
                Vector2 taxiPos = new Vector2(_playerTaxi.transform.position.x, _playerTaxi.transform.position.y);
                if ((taxiPos - currentPos).sqrMagnitude <= maxDistSqr)
                {
                    return true;
                }
            }

            if (_neighborhoodLayout != null && _neighborhoodLayout.ParkedTaxiBayPoint != null)
            {
                Vector2 bayPos = new Vector2(
                    _neighborhoodLayout.ParkedTaxiBayPoint.position.x,
                    _neighborhoodLayout.ParkedTaxiBayPoint.position.y);

                if ((bayPos - currentPos).sqrMagnitude <= maxDistSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanMoveToPosition(Vector2 targetPosition, out WalkableZone zone)
        {
            zone = null;
            if (_neighborhoodLayout == null)
            {
                return false;
            }

            return _neighborhoodLayout.CanWalkAt(targetPosition, out zone);
        }

        private void RefreshCurrentZoneAndInteractable()
        {
            if (_neighborhoodLayout == null)
            {
                _currentWalkableZone = null;
                _nearestInteractable = null;
                return;
            }

            if (_currentMode == DriverMode.OnFoot)
            {
                _neighborhoodLayout.CanWalkAt(Position2D, out _currentWalkableZone);
                _nearestInteractable = _neighborhoodLayout.FindNearestInteractable(Position2D);
            }
            else
            {
                _nearestInteractable = null;
            }
        }

        private void ApplyCameraPrioritiesAndVisibility()
        {
            bool isOnFoot = _currentMode == DriverMode.OnFoot;

            if (_visualsRoot != null)
            {
                _visualsRoot.SetActive(isOnFoot);
            }

            if (_taxiCamera != null)
            {
                _taxiCamera.Priority.Value = isOnFoot ? InactiveCameraPriority : ActiveCameraPriority;
            }

            if (_onFootCamera != null)
            {
                _onFootCamera.Priority.Value = isOnFoot ? ActiveCameraPriority : InactiveCameraPriority;
            }

            if (_trafficDirector != null)
            {
                if (isOnFoot)
                {
                    _trafficDirector.BubbleCenter = transform;
                }
                else if (_playerTaxi != null)
                {
                    _trafficDirector.BubbleCenter = _playerTaxi.transform;
                }
            }
        }

        private void UpdateIsometricFacing(Vector2 moveVector)
        {
            if (moveVector.sqrMagnitude < 0.0001f) return;

            IsometricDirection newFacing = DetermineIsometricDirection(moveVector.x, moveVector.y);
            if (newFacing != _currentFacing)
            {
                _currentFacing = newFacing;
                OnFacingChanged?.Invoke(_currentFacing);
            }
        }

        private static IsometricDirection DetermineIsometricDirection(float x, float y)
        {
            if (x >= 0f && y >= 0f) return IsometricDirection.NorthEast;
            if (x >= 0f && y < 0f) return IsometricDirection.SouthEast;
            if (x < 0f && y < 0f) return IsometricDirection.SouthWest;
            return IsometricDirection.NorthWest;
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && input.sqrMagnitude < 0.001f)
            {
                input = gamepad.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private static bool WasInteractPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }
    }
}
