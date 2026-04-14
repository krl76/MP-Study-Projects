using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    private static readonly HashSet<PlayerNetwork> s_SpawnedPlayers = new HashSet<PlayerNetwork>();
    private static readonly Dictionary<int, int> s_SpawnSlotUsage = new Dictionary<int, int>();

    [SerializeField] private int _maxHp = 100;
    [SerializeField] private float _respawnDelay = 3f;
    [SerializeField] private float _fallbackSpawnHeight = 1f;
    [SerializeField] private float _fallbackSpawnSpacing = 4f;

    private int _assignedSpawnSlot = -1;
    private Coroutine _respawnRoutine;
    private CharacterController _characterController;
    private NetworkTransform _networkTransform;
    private PlayerShooting _playerShooting;

    public static IReadOnlyCollection<PlayerNetwork> SpawnedPlayers => s_SpawnedPlayers;
    public int MaxHp => _maxHp;
    public float RespawnDelay => _respawnDelay;

    public NetworkVariable<FixedString32Bytes> Nickname = new NetworkVariable<FixedString32Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> HP = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> Ammo = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        s_SpawnedPlayers.Add(this);
        CacheComponents();
        HP.OnValueChanged += OnHpChanged;

        if (IsServer)
        {
            Nickname.Value = GetFallbackNickname();
            RestoreFullStateServer();
            _assignedSpawnSlot = AcquireSpawnSlot(randomize: false);
            MoveToSpawnSlotServer(_assignedSpawnSlot);
        }

        if (IsOwner)
        {
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
        }
    }

    public override void OnNetworkDespawn()
    {
        s_SpawnedPlayers.Remove(this);
        HP.OnValueChanged -= OnHpChanged;
        StopRespawnRoutine();
        ReleaseSpawnSlot();
    }

    public override void OnDestroy()
    {
        s_SpawnedPlayers.Remove(this);
        HP.OnValueChanged -= OnHpChanged;
        StopRespawnRoutine();
        ReleaseSpawnSlot();
        base.OnDestroy();
    }

    public bool ApplyDamage(int damage)
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value)
        {
            return false;
        }

        HP.Value = Mathf.Max(0, HP.Value - Mathf.Max(1, damage));
        return true;
    }

    public bool TryHeal(int amount)
    {
        if (!IsServer || !IsSpawned || !IsAlive.Value || HP.Value >= _maxHp)
        {
            return false;
        }

        HP.Value = Mathf.Min(_maxHp, HP.Value + Mathf.Max(1, amount));
        return true;
    }

    public void SetAmmoServer(int ammo)
    {
        if (!IsServer)
        {
            return;
        }

        Ammo.Value = Mathf.Max(0, ammo);
    }

    [ServerRpc]
    private void SubmitNicknameServerRpc(string nickname)
    {
        Nickname.Value = SanitizeNickname(nickname);
    }

    private FixedString32Bytes SanitizeNickname(string nickname)
    {
        string safeValue = string.IsNullOrWhiteSpace(nickname)
            ? GetFallbackNicknameValue()
            : nickname.Trim();

        FixedString32Bytes fixedNickname = default;
        fixedNickname.CopyFromTruncated(safeValue);

        if (fixedNickname.Length == 0)
        {
            fixedNickname.CopyFromTruncated(GetFallbackNicknameValue());
        }

        return fixedNickname;
    }

    private FixedString32Bytes GetFallbackNickname()
    {
        FixedString32Bytes fallback = default;
        fallback.CopyFromTruncated(GetFallbackNicknameValue());
        return fallback;
    }

    private string GetFallbackNicknameValue()
    {
        return $"\u0418\u0433\u0440\u043e\u043a_{OwnerClientId}";
    }

    private void OnHpChanged(int _, int nextHp)
    {
        if (!IsServer || nextHp > 0 || !IsAlive.Value)
        {
            return;
        }

        IsAlive.Value = false;
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
        if (!IsServer)
        {
            return;
        }

        HP.Value = _maxHp;
        IsAlive.Value = true;
        _playerShooting ??= GetComponent<PlayerShooting>();
        _playerShooting?.ResetForSpawnServer();
    }

    private int AcquireSpawnSlot(bool randomize)
    {
        if (!IsServer)
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
                ? freeSlots[Random.Range(0, freeSlots.Count)]
                : freeSlots[0];
        }
        else
        {
            selectedSlot = randomize
                ? Random.Range(0, availableSpawnPoints)
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
        if (!IsServer)
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

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (controllerWasEnabled)
        {
            _characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (controllerWasEnabled)
        {
            _characterController.enabled = true;
        }

        if (_networkTransform != null && IsSpawned)
        {
            _networkTransform.SetState(position, rotation, transform.localScale, teleportDisabled: false);
        }
    }

    private void CacheComponents()
    {
        _characterController ??= GetComponent<CharacterController>();
        _networkTransform ??= GetComponent<NetworkTransform>();
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
