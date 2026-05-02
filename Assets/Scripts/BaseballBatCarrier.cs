using UnityEngine;
using Unity.Netcode;
using Blocks.Gameplay.Core;

/// <summary>
/// Added to a player GameObject (server-side via AddComponent) when they pick
/// up a baseball bat. Implements IPlayerAddon so CorePlayerManager automatically
/// calls OnLifeStateChanged — when the player is eliminated the bat is dropped.
///
/// Handles:
///   - Spawning a carried visual parented to Spine_Attach
///   - Dropping the bat (re-spawning the pickup NetworkObject) on death
///   - Cleaning itself up on player despawn
///
/// No new prefab fields are needed on the player — it finds Spine_Attach by name,
/// matching the existing AttachableNode the weapon system already uses.
/// </summary>
public class BaseballBatCarrier : MonoBehaviour, IPlayerAddon
{
    // ── Config ────────────────────────────────────────────────────────────────

    [Tooltip("Prefab to spawn when the bat is dropped. Assign in the BatSpawner " +
             "or leave null — Initialize() receives it at runtime.")]
    public GameObject batPickupPrefab;

    [Tooltip("Optional prefab for the carried bat visual. If null, a primitive bat is built at runtime.")]
    public GameObject batVisualPrefab;

    // Name of the existing attachment node on the player rig.
    private const string CarryNodeName = "Spine_Attach";

    // Height above ground for the dropped bat.
    private const float DropHeightOffset = 0.5f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private CorePlayerManager _playerManager;
    private GameObject        _carriedVisual;   // bat mesh shown while carrying
    private bool              _initialized;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by BaseballBat.OnPickup() immediately after AddComponent.
    /// Stores the prefab reference and builds the carried visual.
    /// </summary>
    public void Initialize(GameObject pickupPrefab)
    {
        batPickupPrefab = pickupPrefab;
        _initialized    = true;
        BuildCarriedVisual();
    }

    // ── IPlayerAddon ──────────────────────────────────────────────────────────

    public void Initialize(CorePlayerManager playerManager)
    {
        _playerManager = playerManager;
    }

    public void OnPlayerSpawn()
    {
        // Nothing needed — this component is added after spawn.
    }

    public void OnPlayerDespawn()
    {
        // Player is leaving — just clean up visuals locally.
        // Drop logic is handled in OnLifeStateChanged before despawn.
        RemoveCarriedVisual();
    }

    public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
    {
        if (newState == PlayerLifeState.Eliminated)
        {
            DropBat();
        }
    }

    // ── Private: carried visual ───────────────────────────────────────────────

    private void BuildCarriedVisual()
    {
        // Find the existing Spine_Attach node on the player rig.
        Transform attachPoint = FindDeepChild(transform, CarryNodeName);

        if (attachPoint == null)
        {
            Debug.LogWarning("[BaseballBatCarrier] Could not find Spine_Attach on player. " +
                             "Bat will be carried at root position.");
            attachPoint = transform;
        }

        // Use a real model prefab if one has been assigned, otherwise fall back to primitives.
        if (batVisualPrefab != null)
        {
            _carriedVisual = Object.Instantiate(batVisualPrefab, attachPoint);
            _carriedVisual.transform.localPosition = new Vector3(0.05f, -0.3f, 0.1f);
            _carriedVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            _carriedVisual.transform.localScale    = Vector3.one;
        }
        else
        {
            _carriedVisual = BuildPrimitiveBat(attachPoint);
        }
    }

    private void RemoveCarriedVisual()
    {
        if (_carriedVisual != null)
            Destroy(_carriedVisual);
        _carriedVisual = null;
    }

    // ── Private: drop ─────────────────────────────────────────────────────────

    private void DropBat()
    {
        RemoveCarriedVisual();

        // Only the server spawns the new pickup NetworkObject.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Destroy(this);
            return;
        }

        if (batPickupPrefab != null)
        {
            Vector3 dropPos = transform.position + Vector3.up * DropHeightOffset;
            GameObject dropped = Object.Instantiate(batPickupPrefab, dropPos, Quaternion.identity);

            NetworkObject no = dropped.GetComponent<NetworkObject>();
            if (no != null)
                no.Spawn();
            else
                Debug.LogWarning("[BaseballBatCarrier] Bat pickup prefab has no NetworkObject — " +
                                 "dropped bat will not be visible to other clients.");
        }

        Destroy(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Recursive depth-first search for a named child transform.</summary>
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Builds a baseball bat shape from Unity primitives parented to attachPoint.
    /// Swap this out for a real model by replacing the body with:
    ///     GameObject visual = Instantiate(yourBatModelPrefab, attachPoint);
    /// </summary>
    private static GameObject BuildPrimitiveBat(Transform attachPoint)
    {
        var root = new GameObject("CarriedBat");
        root.transform.SetParent(attachPoint);
        root.transform.localPosition = new Vector3(0.05f, -0.3f, 0.1f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
        root.transform.localScale    = Vector3.one;

        // Handle (thin cylinder)
        AddBatPart(root.transform, "Handle",
            localPos:   new Vector3(0f, 0f,    0f),
            localScale: new Vector3(0.04f, 0.22f, 0.04f),
            color:      new Color(0.55f, 0.27f, 0.07f));   // brown

        // Barrel (thicker cylinder)
        AddBatPart(root.transform, "Barrel",
            localPos:   new Vector3(0f, 0.28f, 0f),
            localScale: new Vector3(0.09f, 0.18f, 0.09f),
            color:      new Color(0.55f, 0.27f, 0.07f));

        // Knob (small sphere at the base)
        var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "Knob";
        knob.transform.SetParent(root.transform);
        knob.transform.localPosition = new Vector3(0f, -0.22f, 0f);
        knob.transform.localScale    = new Vector3(0.07f, 0.04f, 0.07f);
        Object.DestroyImmediate(knob.GetComponent<Collider>());
        ApplyColor(knob.GetComponent<Renderer>(), new Color(0.4f, 0.2f, 0.05f));

        return root;
    }

    private static void AddBatPart(Transform parent, string partName,
                                   Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = partName;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        ApplyColor(go.GetComponent<Renderer>(), color);
    }

    private static void ApplyColor(Renderer rend, Color color)
    {
        if (rend == null) return;
        rend.material = new Material(rend.sharedMaterial) { color = color };
    }
}
