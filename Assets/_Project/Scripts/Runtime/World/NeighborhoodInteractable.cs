using System;
using UnityEngine;

namespace OutsiderDriver.World
{
    /// <summary>
    /// Types of everyday domestic and neighborhood interaction points (GAME_DESIGN §2 & §3.D).
    /// </summary>
    public enum InteractableKind
    {
        /// <summary>
        /// Bed or resting spot inside the home to pass time or save.
        /// </summary>
        HomeBed = 0,

        /// <summary>
        /// Personal desk/workbench for translator modules or local broadcasts.
        /// </summary>
        TranslationWorkbench = 1,

        /// <summary>
        /// Threshold doorway connecting the apartment interior and the starter street/garage.
        /// </summary>
        DoorwayThreshold = 2,

        /// <summary>
        /// Parked taxi bay where the driver switches between on-foot walking and taxi driving.
        /// </summary>
        ParkedTaxiBay = 3,

        /// <summary>
        /// Local neighborhood kiosk, bench, or resident meeting spot.
        /// </summary>
        NeighborhoodSpot = 4
    }

    /// <summary>
    /// Authored interactive fixture in the driver's home or starter street.
    /// </summary>
    [DisallowMultipleComponent]
    public class NeighborhoodInteractable : MonoBehaviour
    {
        [SerializeField] private string _interactableId = "interactable_default";
        [SerializeField] private string _promptLabel = "Interact";
        [SerializeField] private InteractableKind _kind = InteractableKind.NeighborhoodSpot;
        [Min(0.25f)]
        [SerializeField] private float _interactionRadius = 1.25f;

        public string InteractableId => _interactableId;
        public string PromptLabel => _promptLabel;
        public InteractableKind Kind => _kind;
        public float InteractionRadius => _interactionRadius;
        public Vector2 Position2D => new Vector2(transform.position.x, transform.position.y);

        public event Action<NeighborhoodInteractable> OnInteracted;

        public void Configure(string interactableId, string promptLabel, InteractableKind kind, float interactionRadius = 1.25f)
        {
            _interactableId = interactableId;
            _promptLabel = promptLabel;
            _kind = kind;
            _interactionRadius = Mathf.Max(0.25f, interactionRadius);
        }

        public bool IsWithinRange(Vector2 actorWorldPosition)
        {
            return (Position2D - actorWorldPosition).sqrMagnitude <= _interactionRadius * _interactionRadius;
        }

        public bool TryInteract(Vector2 actorWorldPosition)
        {
            if (!IsWithinRange(actorWorldPosition))
            {
                return false;
            }

            OnInteracted?.Invoke(this);
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);
        }
#endif
    }
}
