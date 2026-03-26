using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace MazeEscape
{
    /// <summary>
    /// Creates a prison-bar door visual at the spawn room entrance and slides it
    /// upward when the countdown ends. Door state is synced to all clients via
    /// a NetworkVariable so late-joiners also see the correct state.
    /// </summary>
    public class PrisonBarDoor : NetworkBehaviour
    {
        [Header("Bar Geometry")]
        [SerializeField] private int barCount = 6;
        [SerializeField] private float barDiameter = 0.1f;   // world-space diameter
        [SerializeField] private float barWorldHeight = 5f;  // matches maze wall height

        [Header("Door Placement")]
        [Tooltip("World-space centre of the entrance gap (matches maze entrance).")]
        [SerializeField] private Vector3 doorWorldPosition = new Vector3(2.7f, 0f, -16.3f);
        [Tooltip("Width of the gap in the Z direction (matches maze cellSize = 2).")]
        [SerializeField] private float gapWidth = 2f;

        [Header("Animation")]
        [SerializeField] private float slideDistance = 7f;
        [SerializeField] private float slideDuration = 1.4f;

        // ── Network state ─────────────────────────────────────────────────────
        private readonly NetworkVariable<bool> _isDoorOpen = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ── Internals ─────────────────────────────────────────────────────────
        private Transform _barRoot;
        private bool _animationStarted;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            BuildBars();
        }

        public override void OnNetworkSpawn()
        {
            _isDoorOpen.OnValueChanged += HandleDoorStateChanged;

            // Late-joiner: door may already be open — hide bars immediately.
            if (_isDoorOpen.Value)
                HideBarsImmediate();
        }

        public override void OnNetworkDespawn()
        {
            _isDoorOpen.OnValueChanged -= HandleDoorStateChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Server-side: sets the door open, triggering animation on all clients.</summary>
        public void OpenDoor()
        {
            if (!IsServer) return;
            _isDoorOpen.Value = true;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void BuildBars()
        {
            var barRootGo = new GameObject("BarRoot");
            barRootGo.transform.SetParent(transform);
            barRootGo.transform.position = Vector3.zero; // bars use world positions directly
            _barRoot = barRootGo.transform;

            // Distribute bars evenly across the Z gap, with equal margins on both ends.
            // gapStart = centre - half width (e.g. -16.3 - 1 = -17.3)
            float gapStart = doorWorldPosition.z - gapWidth / 2f;
            float spacing  = gapWidth / (barCount + 1);

            for (int i = 0; i < barCount; i++)
            {
                float z   = gapStart + spacing * (i + 1);
                var   bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name  = "Bar_" + i;
                bar.transform.SetParent(_barRoot);

                // Unity Cylinder: local height = 2 units → world height = scale.y * 2
                bar.transform.position   = new Vector3(doorWorldPosition.x, 0f, z);
                bar.transform.localScale = new Vector3(barDiameter, barWorldHeight * 0.5f, barDiameter);

                // Dark metallic colour
                var rend = bar.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Create an instance so we don't modify the shared default material.
                    rend.material       = new Material(rend.sharedMaterial);
                    rend.material.color = new Color(0.18f, 0.18f, 0.18f);
                }
            }
        }

        private void HandleDoorStateChanged(bool prev, bool next)
        {
            if (next && !_animationStarted)
                StartCoroutine(SlideUpCoroutine());
        }

        private IEnumerator SlideUpCoroutine()
        {
            _animationStarted = true;

            Vector3 startPos  = _barRoot.position;
            Vector3 targetPos = startPos + Vector3.up * slideDistance;
            float   elapsed   = 0f;

            while (elapsed < slideDuration)
            {
                elapsed      += Time.deltaTime;
                float t       = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration));
                _barRoot.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            _barRoot.position = targetPos;
            _barRoot.gameObject.SetActive(false);
        }

        private void HideBarsImmediate()
        {
            _animationStarted = true;
            if (_barRoot != null)
                _barRoot.gameObject.SetActive(false);
        }
    }
}
