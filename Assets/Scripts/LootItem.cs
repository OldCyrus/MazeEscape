using UnityEngine;

/// <summary>
/// Base class for all loot items. Attach to a GameObject with a Trigger Collider.
/// Name a child GameObject "Visual" — it will spin and bob automatically.
/// </summary>
public abstract class LootItem : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Destroy loot after this many seconds (0 = never expires)")]
    public float lifetime = 30f;

    [Header("Spin Visual")]
    public float spinSpeed = 90f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 1.5f;

    [Header("Audio / FX")]
    public AudioClip pickupSound;
    public GameObject pickupVFXPrefab;

    protected Transform _visual;
    private Vector3 _startPos;
    private float _age;

    protected virtual void Awake()
    {
        Transform v = transform.Find("Visual");
        _visual = v != null ? v : transform;
        _startPos = transform.position;
    }

    protected virtual void Start()
    {
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    protected virtual void Update()
    {
        _age += Time.deltaTime;
        _visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        float newY = _startPos.y + Mathf.Sin(_age * bobSpeed * Mathf.PI * 2f) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            TryPickup(other.gameObject);
    }

    private void TryPickup(GameObject player)
    {
        if (OnPickup(player))
            PlayFXAndDestroy();
    }

    /// <summary>Override in subclass. Return true if the pickup was successfully applied.</summary>
    protected abstract bool OnPickup(GameObject player);

    private void PlayFXAndDestroy()
    {
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        if (pickupVFXPrefab != null)
            Instantiate(pickupVFXPrefab, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}