using UnityEngine;

namespace OutsiderDriver.Data
{
    /// <summary>
    /// Authored baseline street class. One planning cell is one metre; widths are nominal
    /// standards for authoring and validation, not universal dimension requirements
    /// (owner scale decision, 2026-10-25).
    /// </summary>
    [CreateAssetMenu(fileName = "StreetClass", menuName = "Outsider Driver/Data/Street Class")]
    public class StreetProfileSO : ScriptableObject
    {
        [Header("Stable Identity")]
        [SerializeField] private string _streetClassId = "street_class_local_two_way";

        [Tooltip("Development label only; not an in-world alien street name.")]
        [SerializeField] private string _workingName = "Local two-way street";

        [Header("Nominal Cross-Section (metres)")]
        [Min(1)]
        [SerializeField] private int _lanesPerDirection = 1;
        [Min(0.5f)]
        [SerializeField] private float _laneWidthMeters = 3f;
        [Min(0.5f)]
        [SerializeField] private float _sidewalkWidthMeters = 2f;

        public string StreetClassId => _streetClassId;
        public string WorkingName => _workingName;
        public int LanesPerDirection => _lanesPerDirection;
        public int TotalLanes => _lanesPerDirection * 2;
        public float LaneWidthMeters => _laneWidthMeters;
        public float SidewalkWidthMeters => _sidewalkWidthMeters;
        public float CarriagewayWidthMeters => TotalLanes * _laneWidthMeters;
        public float TotalWidthMeters => CarriagewayWidthMeters + (2f * _sidewalkWidthMeters);

        /// <summary>
        /// Centre offset of the innermost travel lane from the street centerline.
        /// </summary>
        public float InnermostLaneCenterOffsetMeters => _laneWidthMeters * 0.5f;

        public float OutermostLaneCenterOffsetMeters =>
            InnermostLaneCenterOffsetMeters + ((_lanesPerDirection - 1) * _laneWidthMeters);

        public void Initialize(
            string streetClassId,
            string workingName,
            int lanesPerDirection,
            float laneWidthMeters,
            float sidewalkWidthMeters)
        {
            _streetClassId = streetClassId;
            _workingName = workingName;
            _lanesPerDirection = Mathf.Max(1, lanesPerDirection);
            _laneWidthMeters = Mathf.Max(0.5f, laneWidthMeters);
            _sidewalkWidthMeters = Mathf.Max(0.5f, sidewalkWidthMeters);
        }
    }
}
