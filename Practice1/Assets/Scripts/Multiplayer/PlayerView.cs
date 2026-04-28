using FishNet.Object;
using TMPro;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    private const string GraphicsChildName = "Graphics";

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

        _bodyRenderer ??= ResolveBodyRenderer();
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

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
        }

        _bodyRenderer = ResolveBodyRenderer();

        if (_playerNetwork == null)
        {
            return;
        }

        _showNameplate = !base.Owner.IsLocalClient;
        if (_nameplateCanvas != null && !_showNameplate)
        {
            _nameplateCanvas.gameObject.SetActive(false);
        }

        _playerNetwork.NicknameChanged += OnNicknameChanged;
        _playerNetwork.HpChanged += OnHpChanged;
        _playerNetwork.AliveChanged += OnAliveChanged;

        OnNicknameChanged(_playerNetwork.Nickname, _playerNetwork.Nickname);
        OnHpChanged(_playerNetwork.HP, _playerNetwork.HP);
        OnAliveChanged(_playerNetwork.IsAlive, _playerNetwork.IsAlive);
    }

    public override void OnStopNetwork()
    {
        if (_playerNetwork != null)
        {
            _playerNetwork.NicknameChanged -= OnNicknameChanged;
            _playerNetwork.HpChanged -= OnHpChanged;
            _playerNetwork.AliveChanged -= OnAliveChanged;
        }

        base.OnStopNetwork();
    }

    private void OnNicknameChanged(string _, string newValue)
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = newValue;
        }
    }

    private MeshRenderer ResolveBodyRenderer()
    {
        Transform graphicsTransform = transform.Find(GraphicsChildName);
        MeshRenderer graphicsRenderer = graphicsTransform != null
            ? graphicsTransform.GetComponent<MeshRenderer>()
            : null;

        return graphicsRenderer != null ? graphicsRenderer : GetComponent<MeshRenderer>();
    }

    private void OnHpChanged(int _, int newValue)
    {
        if (_hpText != null)
        {
            _hpText.text = $"Здоровье: {newValue}";
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
