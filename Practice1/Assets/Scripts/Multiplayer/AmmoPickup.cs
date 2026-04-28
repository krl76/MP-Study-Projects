using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SphereCollider))]
public class AmmoPickup : NetworkBehaviour
{
    [SerializeField] private int _ammoAmount = 5;
    [SerializeField] private float _respawnDelay = 8f;
    [SerializeField] private float _triggerRadius = 0.75f;

    private PickupManager _pickupManager;
    private Vector3 _spawnPosition;
    private SphereCollider _sphereCollider;
    private readonly Collider[] _overlapResults = new Collider[16];

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        ApplyColliderSettings();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        _sphereCollider = GetComponent<SphereCollider>();
        ApplyColliderSettings();
    }

    public void Initialize(PickupManager pickupManager, Vector3 spawnPosition)
    {
        _pickupManager = pickupManager;
        _spawnPosition = spawnPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPickUp(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPickUp(other);
    }

    private void FixedUpdate()
    {
        if (!base.IsServerInitialized || !base.IsSpawned || _pickupManager == null)
        {
            return;
        }

        int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, Mathf.Max(0.05f, _triggerRadius), _overlapResults, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = _overlapResults[i];
            if (hit == null)
            {
                continue;
            }

            if (TryPickUp(hit))
            {
                break;
            }
        }
    }

    private bool TryPickUp(Collider other)
    {
        if (!base.IsServerInitialized || !base.IsSpawned || _pickupManager == null)
        {
            return false;
        }

        PlayerNetwork player = other.GetComponentInParent<PlayerNetwork>();
        if (player == null || !player.IsAlive)
        {
            return false;
        }

        PlayerShooting shooting = player.GetComponent<PlayerShooting>();
        if (shooting == null || !shooting.TryAddAmmoServer(_ammoAmount))
        {
            return false;
        }

        _pickupManager.NotifyAmmoPickedUp(_spawnPosition, _respawnDelay);
        base.ServerManager.Despawn(base.NetworkObject, DespawnType.Destroy);
        return true;
    }

    private void ApplyColliderSettings()
    {
        if (_sphereCollider == null)
        {
            return;
        }

        _sphereCollider.isTrigger = true;
        _sphereCollider.radius = Mathf.Max(0.05f, _triggerRadius);
    }
}
