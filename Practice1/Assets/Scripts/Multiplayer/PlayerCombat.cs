using FishNet.Object;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private int _damage = 10;

    public bool TryAttackNearest()
    {
        if (!base.IsOwner || !base.IsSpawned || _playerNetwork == null)
        {
            return false;
        }

        AttackNearestTargetServerRpc();
        return true;
    }

    [ServerRpc]
    private void AttackNearestTargetServerRpc()
    {
        if (_playerNetwork == null || _playerNetwork.HP <= 0)
        {
            return;
        }

        PlayerNetwork target = FindNearestValidTarget();
        target?.ApplyDamage(_damage);
    }

    private PlayerNetwork FindNearestValidTarget()
    {
        PlayerNetwork bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (PlayerNetwork candidate in PlayerNetwork.SpawnedPlayers)
        {
            if (candidate == null ||
                candidate == _playerNetwork ||
                !candidate.IsSpawned ||
                candidate.OwnerId == base.OwnerId ||
                candidate.HP <= 0)
            {
                continue;
            }

            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private void Reset()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();
    }

    private void OnValidate()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        _damage = Mathf.Max(1, _damage);
    }
}
