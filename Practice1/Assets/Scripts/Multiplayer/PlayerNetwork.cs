using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    private static readonly HashSet<PlayerNetwork> s_SpawnedPlayers = new HashSet<PlayerNetwork>();
    private static readonly Dictionary<int, int> s_SpawnSlotUsage = new Dictionary<int, int>();

    [SerializeField] private int _maxHp = 100;
    [SerializeField] private float _respawnDelay = 3f;
    [SerializeField] private float _fallbackSpawnHeight = 1f;
    [SerializeField] private float _fallbackSpawnSpacing = 4f;

    private readonly SyncVar<string> _nickname = new SyncVar<string>("Игрок");
    private readonly SyncVar<int> _hp = new SyncVar<int>(100);
    private readonly SyncVar<bool> _isAlive = new SyncVar<bool>(true);
    private readonly SyncVar<int> _ammo = new SyncVar<int>();

    private int _assignedSpawnSlot = -1;
    private Coroutine _respawnRoutine;
    private PlayerMovement _playerMovement;
    private PlayerShooting _playerShooting;

    public static IReadOnlyCollection<PlayerNetwork> SpawnedPlayers => s_SpawnedPlayers;
    public int MaxHp => _maxHp;
    public float RespawnDelay => _respawnDelay;
    public string Nickname => _nickname.Value;
    public int HP => _hp.Value;
    public bool IsAlive => _isAlive.Value;
    public int Ammo => _ammo.Value;

    public event Action<string, string> NicknameChanged;
    public event Action<int, int> HpChanged;
    public event Action<bool, bool> AliveChanged;
    public event Action<int, int> AmmoChanged;

    public override void OnStartNetwork()
    {
        s_SpawnedPlayers.Add(this);
        CacheComponents();

        _nickname.OnChange += OnNicknameSyncChanged;
        _hp.OnChange += OnHpSyncChanged;
        _isAlive.OnChange += OnAliveSyncChanged;
        _ammo.OnChange += OnAmmoSyncChanged;

        NicknameChanged?.Invoke(_nickname.Value, _nickname.Value);
        HpChanged?.Invoke(_hp.Value, _hp.Value);
        AliveChanged?.Invoke(_isAlive.Value, _isAlive.Value);
        AmmoChanged?.Invoke(_ammo.Value, _ammo.Value);

        if (base.IsServerInitialized)
        {
            _nickname.Value = GetFallbackNicknameValue();
            RestoreFullStateServer();
            _assignedSpawnSlot = AcquireSpawnSlot(randomize: false);
            MoveToSpawnSlotServer(_assignedSpawnSlot);
        }

        if (base.Owner.IsLocalClient)
        {
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
        }
    }

    public override void OnStopNetwork()
    {
        _nickname.OnChange -= OnNicknameSyncChanged;
        _hp.OnChange -= OnHpSyncChanged;
        _isAlive.OnChange -= OnAliveSyncChanged;
        _ammo.OnChange -= OnAmmoSyncChanged;
        s_SpawnedPlayers.Remove(this);
        StopRespawnRoutine();
        ReleaseSpawnSlot();
    }

    private void OnDestroy()
    {
        s_SpawnedPlayers.Remove(this);
        StopRespawnRoutine();
        ReleaseSpawnSlot();
    }

    public bool ApplyDamage(int damage)
    {
        if (!base.IsServerInitialized || !base.IsSpawned || !_isAlive.Value)
        {
            return false;
        }

        _hp.Value = Mathf.Max(0, _hp.Value - Mathf.Max(1, damage));
        HandleServerDeathIfNeeded();
        return true;
    }

    public bool TryHeal(int amount)
    {
        if (!base.IsServerInitialized || !base.IsSpawned || !_isAlive.Value || _hp.Value >= _maxHp)
        {
            return false;
        }

        _hp.Value = Mathf.Min(_maxHp, _hp.Value + Mathf.Max(1, amount));
        return true;
    }

    public void SetAmmoServer(int ammo)
    {
        if (!base.IsServerInitialized)
        {
            return;
        }

        _ammo.Value = Mathf.Max(0, ammo);
    }

    [ServerRpc]
    private void SubmitNicknameServerRpc(string nickname)
    {
        _nickname.Value = SanitizeNickname(nickname);
    }

    private string SanitizeNickname(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname)
            ? GetFallbackNicknameValue()
            : nickname.Trim();

        return safeValue.Length > 32
            ? safeValue[..32]
            : safeValue;
    }

    private string GetFallbackNicknameValue()
    {
        return $"Игрок_{base.OwnerId}";
    }

    private void OnNicknameSyncChanged(string previous, string next, bool asServer)
    {
        NicknameChanged?.Invoke(previous, next);
    }

    private void OnHpSyncChanged(int previous, int next, bool asServer)
    {
        HpChanged?.Invoke(previous, next);

        if (asServer)
        {
            HandleServerDeathIfNeeded();
        }
    }

    private void OnAliveSyncChanged(bool previous, bool next, bool asServer)
    {
        AliveChanged?.Invoke(previous, next);
    }

    private void OnAmmoSyncChanged(int previous, int next, bool asServer)
    {
        AmmoChanged?.Invoke(previous, next);
    }

    private void HandleServerDeathIfNeeded()
    {
        if (!base.IsServerInitialized || _hp.Value > 0 || !_isAlive.Value)
        {
            return;
        }

        _isAlive.Value = false;
        ReleaseSpawnSlot();
        StopRespawnRoutine();
        _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        _assignedSpawnSlot = AcquireSpawnSlot(randomize: true);
        MoveToSpawnSlotServer(_assignedSpawnSlot);
        RestoreFullStateServer();
        _respawnRoutine = null;
    }

    private void RestoreFullStateServer()
    {
        if (!base.IsServerInitialized)
        {
            return;
        }

        _hp.Value = _maxHp;
        _isAlive.Value = true;
        _playerShooting ??= GetComponent<PlayerShooting>();
        _playerShooting?.ResetForSpawnServer();
    }

    private int AcquireSpawnSlot(bool randomize)
    {
        if (!base.IsServerInitialized)
        {
            return -1;
        }

        int availableSpawnPoints = PlayerSpawnPoint.Count;
        if (availableSpawnPoints <= 0)
        {
            int fallbackSlot = 0;
            while (s_SpawnSlotUsage.ContainsKey(fallbackSlot))
            {
                fallbackSlot++;
            }

            RegisterSlotUsage(fallbackSlot);
            return fallbackSlot;
        }

        List<int> freeSlots = new List<int>();
        for (int i = 0; i < availableSpawnPoints; i++)
        {
            if (!s_SpawnSlotUsage.ContainsKey(i))
            {
                freeSlots.Add(i);
            }
        }

        int selectedSlot;
        if (freeSlots.Count > 0)
        {
            selectedSlot = randomize
                ? freeSlots[UnityEngine.Random.Range(0, freeSlots.Count)]
                : freeSlots[0];
        }
        else
        {
            selectedSlot = randomize
                ? UnityEngine.Random.Range(0, availableSpawnPoints)
                : 0;
        }

        RegisterSlotUsage(selectedSlot);
        return selectedSlot;
    }

    private void ReleaseSpawnSlot()
    {
        if (_assignedSpawnSlot < 0)
        {
            return;
        }

        if (s_SpawnSlotUsage.TryGetValue(_assignedSpawnSlot, out int usageCount))
        {
            if (usageCount <= 1)
            {
                s_SpawnSlotUsage.Remove(_assignedSpawnSlot);
            }
            else
            {
                s_SpawnSlotUsage[_assignedSpawnSlot] = usageCount - 1;
            }
        }

        _assignedSpawnSlot = -1;
    }

    private void RegisterSlotUsage(int slotIndex)
    {
        if (s_SpawnSlotUsage.TryGetValue(slotIndex, out int usageCount))
        {
            s_SpawnSlotUsage[slotIndex] = usageCount + 1;
        }
        else
        {
            s_SpawnSlotUsage[slotIndex] = 1;
        }
    }

    private void MoveToSpawnSlotServer(int slotIndex)
    {
        if (!base.IsServerInitialized)
        {
            return;
        }

        Vector3 position = GetSpawnPosition(slotIndex);
        Quaternion rotation = GetSpawnRotation(slotIndex);
        TeleportServer(position, rotation);
    }

    private Vector3 GetSpawnPosition(int slotIndex)
    {
        PlayerSpawnPoint spawnPoint = PlayerSpawnPoint.GetByIndex(slotIndex);
        if (spawnPoint != null)
        {
            return spawnPoint.transform.position;
        }

        return new Vector3((slotIndex * _fallbackSpawnSpacing) - 2f, _fallbackSpawnHeight, 0f);
    }

    private Quaternion GetSpawnRotation(int slotIndex)
    {
        PlayerSpawnPoint spawnPoint = PlayerSpawnPoint.GetByIndex(slotIndex);
        return spawnPoint != null
            ? spawnPoint.transform.rotation
            : Quaternion.identity;
    }

    private void TeleportServer(Vector3 position, Quaternion rotation)
    {
        CacheComponents();
        _playerMovement?.TeleportServer(position, rotation);
    }

    private void CacheComponents()
    {
        _playerMovement ??= GetComponent<PlayerMovement>();
        _playerShooting ??= GetComponent<PlayerShooting>();
    }

    private void StopRespawnRoutine()
    {
        if (_respawnRoutine == null)
        {
            return;
        }

        StopCoroutine(_respawnRoutine);
        _respawnRoutine = null;
    }
}
