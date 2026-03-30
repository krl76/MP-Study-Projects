using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _hpText;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        if (_playerNetwork == null)
        {
            return;
        }

        _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged += OnHpChanged;

        OnNicknameChanged(default, _playerNetwork.Nickname.Value);
        OnHpChanged(0, _playerNetwork.HP.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_playerNetwork == null)
        {
            return;
        }

        _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged -= OnHpChanged;
    }

    private void OnNicknameChanged(FixedString32Bytes _, FixedString32Bytes newValue)
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = newValue.ToString();
        }
    }

    private void OnHpChanged(int _, int newValue)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {newValue}";
        }
    }
}
