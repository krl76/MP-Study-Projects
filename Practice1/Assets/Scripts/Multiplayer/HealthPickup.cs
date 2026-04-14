using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SphereCollider))]
public class HealthPickup : NetworkBehaviour
{
    [SerializeField] private int _healAmount = 40;
    [SerializeField] private float _respawnDelay = 10f;
    [SerializeField] private GameObject _visualPrefab;
    [SerializeField] private Vector3 _visualOffset = Vector3.zero;
    [SerializeField] private Vector3 _visualRotationEuler = Vector3.zero;
    [SerializeField] private Vector3 _visualScale = Vector3.one;
    [SerializeField] private float _triggerRadius = 0.75f;
    [SerializeField] private Vector3 _triggerCenter = Vector3.zero;
    [SerializeField] private bool _hideRootRendererWhenUsingVisual = true;

    private PickupManager _pickupManager;
    private Vector3 _spawnPosition;
    private SphereCollider _sphereCollider;
    private MeshRenderer _rootRenderer;
    private GameObject _spawnedVisual;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        _rootRenderer = GetComponent<MeshRenderer>();
        ApplyColliderSettings();
        UpdateRootRendererVisibility();
    }

    public override void OnNetworkSpawn()
    {
        EnsureVisualInstance();
    }

    public void Initialize(PickupManager pickupManager, Vector3 spawnPosition)
    {
        _pickupManager = pickupManager;
        _spawnPosition = spawnPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || _pickupManager == null)
        {
            return;
        }

        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || !player.IsAlive.Value)
        {
            return;
        }

        if (!player.TryHeal(_healAmount))
        {
            return;
        }

        _pickupManager.NotifyPickedUp(_spawnPosition, _respawnDelay);
        NetworkObject.Despawn(destroy: true);
    }

    private void ApplyColliderSettings()
    {
        if (_sphereCollider == null)
        {
            return;
        }

        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = Mathf.Max(0.05f, _triggerRadius);
        _sphereCollider.center = _triggerCenter;
    }

    private void UpdateRootRendererVisibility()
    {
        if (_rootRenderer == null)
        {
            return;
        }

        bool hasCustomVisual = _visualPrefab != null;
        _rootRenderer.enabled = !hasCustomVisual || !_hideRootRendererWhenUsingVisual;
    }

    private void EnsureVisualInstance()
    {
        if (_spawnedVisual != null || _visualPrefab == null)
        {
            return;
        }

        _spawnedVisual = Instantiate(_visualPrefab, transform);
        Transform visualTransform = _spawnedVisual.transform;
        visualTransform.localPosition = _visualOffset;
        visualTransform.localRotation = Quaternion.Euler(_visualRotationEuler);
        visualTransform.localScale = _visualScale;

        Collider[] colliders = _spawnedVisual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }
}
