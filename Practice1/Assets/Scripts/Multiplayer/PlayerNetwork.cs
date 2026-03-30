using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    private static readonly HashSet<PlayerNetwork> s_SpawnedPlayers = new();
    private static readonly HashSet<int> s_OccupiedSpawnSlots = new();

    [SerializeField] private int _maxHp = 100;
    [SerializeField] private float _spawnHeight = 1f;
    [SerializeField] private float _spawnSpacing = 4f;

    private int _assignedSpawnSlot = -1;

    public static IReadOnlyCollection<PlayerNetwork> SpawnedPlayers => s_SpawnedPlayers;

    public NetworkVariable<FixedString32Bytes> Nickname = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> HP = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        s_SpawnedPlayers.Add(this);

        if (IsServer)
        {
            _assignedSpawnSlot = AcquireSpawnSlot();
            HP.Value = _maxHp;
            Nickname.Value = GetFallbackNickname();
            transform.position = GetSpawnPosition(_assignedSpawnSlot);
        }

        if (IsOwner)
        {
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
        }
    }

    public override void OnNetworkDespawn()
    {
        s_SpawnedPlayers.Remove(this);
        ReleaseSpawnSlot();
    }

    private void OnDestroy()
    {
        s_SpawnedPlayers.Remove(this);
        ReleaseSpawnSlot();
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

    private int AcquireSpawnSlot()
    {
        if (!IsServer)
        {
            return -1;
        }

        int slotIndex = 0;
        while (s_OccupiedSpawnSlots.Contains(slotIndex))
        {
            slotIndex++;
        }

        s_OccupiedSpawnSlots.Add(slotIndex);
        return slotIndex;
    }

    private void ReleaseSpawnSlot()
    {
        if (_assignedSpawnSlot < 0)
        {
            return;
        }

        s_OccupiedSpawnSlots.Remove(_assignedSpawnSlot);
        _assignedSpawnSlot = -1;
    }

    private Vector3 GetSpawnPosition(int slotIndex)
    {
        return new Vector3((slotIndex * _spawnSpacing) - 2f, _spawnHeight, 0f);
    }
}
