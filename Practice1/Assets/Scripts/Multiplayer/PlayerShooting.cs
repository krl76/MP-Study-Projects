using FishNet.Object;
using FishNet.Connection;
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

    public override void OnStartNetwork()
    {
        _playerNetwork ??= GetComponent<PlayerNetwork>();
        EnsureFirePoint();

        if (base.IsServerInitialized && _playerNetwork != null && _playerNetwork.Ammo <= 0)
        {
            ResetForSpawnServer();
        }
    }

    private void Update()
    {
        if (!base.IsOwner || !base.IsSpawned || _playerNetwork == null || !_playerNetwork.IsAlive)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
        {
            return;
        }

        ShootServerRpc();
    }

    public void ResetForSpawnServer()
    {
        if (!base.IsServerInitialized || _playerNetwork == null)
        {
            return;
        }

        _nextShotTime = 0f;
        _playerNetwork.SetAmmoServer(_maxAmmo);
    }

    [ServerRpc]
    private void ShootServerRpc(NetworkConnection sender = null)
    {
        _playerNetwork ??= GetComponent<PlayerNetwork>();
        if (_playerNetwork == null || _projectilePrefab == null)
        {
            return;
        }

        if (!_playerNetwork.IsAlive || _playerNetwork.HP <= 0)
        {
            return;
        }

        if (_playerNetwork.Ammo <= 0)
        {
            return;
        }

        if (Time.time < _nextShotTime)
        {
            return;
        }

        EnsureFirePoint();
        Vector3 shotDirection = transform.forward;
        shotDirection.y = 0f;
        if (shotDirection.sqrMagnitude <= 0.001f)
        {
            shotDirection = Vector3.forward;
        }

        shotDirection.Normalize();
        _nextShotTime = Time.time + _cooldown;
        _playerNetwork.SetAmmoServer(_playerNetwork.Ammo - 1);

        GameObject projectileObject = Instantiate(
            _projectilePrefab,
            _firePoint.position + (shotDirection * 1.1f),
            Quaternion.LookRotation(shotDirection, Vector3.up)
        );

        Projectile projectile = projectileObject.GetComponent<Projectile>();
        projectile?.SetInitialDirection(shotDirection);

        base.ServerManager.Spawn(projectileObject, sender);
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
