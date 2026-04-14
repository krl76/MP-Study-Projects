using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : NetworkBehaviour
{
    [SerializeField] private float _speed = 18f;
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _lifeTime = 4f;

    private Rigidbody _rigidbody;
    private Vector3 _initialDirection = Vector3.forward;
    private float _despawnAt;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        sphereCollider.isTrigger = true;

        _rigidbody.useGravity = false;
    }

    public override void OnNetworkSpawn()
    {
        _despawnAt = Time.time + _lifeTime;

        if (IsServer)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = _initialDirection.normalized * _speed;
        }
        else
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (Time.time >= _despawnAt)
        {
            DespawnSelf();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        PlayerNetwork target = other.GetComponentInParent<PlayerNetwork>();
        if (target != null)
        {
            if (target.OwnerClientId == OwnerClientId || !target.IsAlive.Value)
            {
                return;
            }

            if (target.ApplyDamage(_damage))
            {
                DespawnSelf();
            }

            return;
        }

        if (other.GetComponentInParent<HealthPickup>() != null || other.isTrigger)
        {
            return;
        }

        DespawnSelf();
    }

    public void SetInitialDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            _initialDirection = direction.normalized;
        }
    }

    private void DespawnSelf()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(destroy: true);
        }
    }
}
