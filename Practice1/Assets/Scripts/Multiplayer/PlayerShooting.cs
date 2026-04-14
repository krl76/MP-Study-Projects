using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerNetwork))]
public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _cooldown = 0.4f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private Vector3 _firePointOffset = new Vector3(0f, 0.85f, 0.75f);

    private float _nextShotTime;
    private PlayerNetwork _playerNetwork;
    private Transform _firePoint;

    public int MaxAmmo => _maxAmmo;
    public GameObject ProjectilePrefab => _projectilePrefab;

    private void Awake()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();
        EnsureFirePoint();
    }

    public override void OnNetworkSpawn()
    {
        _playerNetwork ??= GetComponent<PlayerNetwork>();
        EnsureFirePoint();

        if (IsServer && _playerNetwork != null && _playerNetwork.Ammo.Value <= 0)
        {
            ResetForSpawnServer();
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || _playerNetwork == null || !_playerNetwork.IsAlive.Value)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        Vector3 shotDirection = transform.forward;
        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude <= 0.001f)
        {
            shotDirection = Vector3.forward;
        }

        ShootServerRpc(_firePoint.position, shotDirection.normalized);
    }

    public void ResetForSpawnServer()
    {
        if (!IsServer || _playerNetwork == null)
        {
            return;
        }

        _nextShotTime = 0f;
        _playerNetwork.SetAmmoServer(_maxAmmo);
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 position, Vector3 direction, ServerRpcParams rpcParams = default)
    {
        _playerNetwork ??= GetComponent<PlayerNetwork>();
        if (_playerNetwork == null || _projectilePrefab == null)
        {
            return;
        }

        if (!_playerNetwork.IsAlive.Value || _playerNetwork.HP.Value <= 0)
        {
            return;
        }

        if (_playerNetwork.Ammo.Value <= 0)
        {
            return;
        }

        if (Time.time < _nextShotTime)
        {
            return;
        }

        Vector3 shotDirection = new Vector3(direction.x, 0f, direction.z);
        if (shotDirection.sqrMagnitude <= 0.001f)
        {
            shotDirection = transform.forward;
        }

        shotDirection.Normalize();
        _nextShotTime = Time.time + _cooldown;
        _playerNetwork.SetAmmoServer(_playerNetwork.Ammo.Value - 1);

        GameObject projectileObject = Instantiate(
            _projectilePrefab,
            position + (shotDirection * 1.1f),
            Quaternion.LookRotation(shotDirection, Vector3.up)
        );

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        projectile?.SetInitialDirection(shotDirection);

        NetworkObject networkObject = projectileObject.GetComponent<NetworkObject>();
        networkObject.SpawnWithOwnership(rpcParams.Receive.SenderClientId);
    }

    private void EnsureFirePoint()
    {
        if (_firePoint != null)
        {
            _firePoint.localPosition = _firePointOffset;
            _firePoint.localRotation = Quaternion.identity;
            return;
        }

        Transform existing = transform.Find("FirePoint");
        if (existing != null)
        {
            _firePoint = existing;
        }
        else
        {
            GameObject firePoint = new GameObject("FirePoint");
            _firePoint = firePoint.transform;
            _firePoint.SetParent(transform, false);
        }

        _firePoint.localPosition = _firePointOffset;
        _firePoint.localRotation = Quaternion.identity;
    }
}
