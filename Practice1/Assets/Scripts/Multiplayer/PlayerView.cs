using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TMP_Text _nicknameText;
    [SerializeField] private TMP_Text _hpText;
    [SerializeField] private MeshRenderer _bodyRenderer;
    [SerializeField] private Canvas _nameplateCanvas;

    private Camera _mainCamera;
    private bool _showNameplate;

    private void Awake()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        _bodyRenderer ??= GetComponent<MeshRenderer>();
        _nameplateCanvas ??= GetComponentInChildren<Canvas>();
    }

    private void LateUpdate()
    {
        if (!_showNameplate || _nameplateCanvas == null || !_nameplateCanvas.gameObject.activeSelf)
        {
            return;
        }

        _mainCamera = _mainCamera != null ? _mainCamera : Camera.main;
        if (_mainCamera == null)
        {
            return;
        }

        Vector3 directionToCamera = _nameplateCanvas.transform.position - _mainCamera.transform.position;
        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            _nameplateCanvas.transform.forward = directionToCamera.normalized;
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

        _showNameplate = !IsOwner;
        if (_nameplateCanvas != null && !_showNameplate)
        {
            _nameplateCanvas.gameObject.SetActive(false);
        }

        _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged += OnHpChanged;
        _playerNetwork.IsAlive.OnValueChanged += OnAliveChanged;

        OnNicknameChanged(default, _playerNetwork.Nickname.Value);
        OnHpChanged(0, _playerNetwork.HP.Value);
        OnAliveChanged(true, _playerNetwork.IsAlive.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_playerNetwork == null)
        {
            return;
        }

        _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged -= OnHpChanged;
        _playerNetwork.IsAlive.OnValueChanged -= OnAliveChanged;
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

    private void OnAliveChanged(bool _, bool isAlive)
    {
        if (_bodyRenderer != null)
        {
            _bodyRenderer.enabled = isAlive;
        }

        if (_nameplateCanvas != null)
        {
            _nameplateCanvas.gameObject.SetActive(isAlive && _showNameplate);
        }
    }
}
