using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionUI : MonoBehaviour
{
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private TMP_InputField _nicknameInput;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _clientButton;
    [SerializeField] private Button _attackButton;
    [SerializeField] private GameObject _startPanel;
    [SerializeField] private GameObject _sessionPanel;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private string _address = "127.0.0.1";
    [SerializeField] private ushort _port = 7777;

    public static string PlayerNickname { get; private set; } = "\u0418\u0433\u0440\u043e\u043a";

    private NetworkManager _networkManager;
    private bool _callbacksRegistered;

    private void Awake()
    {
        BindButtons();
        CacheNetworkManager();
        UpdatePanels(false);
        SetStatus(string.Empty);
    }

    private void OnEnable()
    {
        RegisterCallbacks();
        RefreshSessionState();
    }

    private void Start()
    {
        RefreshSessionState();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();
    }

    public void StartAsHost()
    {
        if (!PrepareStart())
        {
            return;
        }

        ConfigureTransport();
        if (_networkManager.StartHost())
        {
            UpdatePanels(true);
            SetStatus($"\u0417\u0430\u043f\u0443\u0441\u043a \u0445\u043e\u0441\u0442\u0430 \u043d\u0430 {_address}:{_port}...");
        }
        else
        {
            SetStatus("\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c \u0445\u043e\u0441\u0442.");
        }
    }

    public void StartAsClient()
    {
        if (!PrepareStart())
        {
            return;
        }

        ConfigureTransport();
        if (_networkManager.StartClient())
        {
            UpdatePanels(true);
            SetStatus($"\u041f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u0435 \u043a\u043b\u0438\u0435\u043d\u0442\u0430 \u043a {_address}:{_port}...");
        }
        else
        {
            SetStatus("\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c \u043a\u043b\u0438\u0435\u043d\u0442\u0430.");
        }
    }

    public void AttackNearest()
    {
        CacheNetworkManager();
        if (_networkManager == null || !_networkManager.IsListening)
        {
            SetStatus("\u0421\u043d\u0430\u0447\u0430\u043b\u0430 \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u0435 \u0441\u0435\u0442\u0435\u0432\u0443\u044e \u0441\u0435\u0441\u0441\u0438\u044e.");
            return;
        }

        NetworkObject localPlayerObject = _networkManager.SpawnManager?.GetLocalPlayerObject();
        if (localPlayerObject == null)
        {
            SetStatus("\u041b\u043e\u043a\u0430\u043b\u044c\u043d\u044b\u0439 \u0438\u0433\u0440\u043e\u043a \u0435\u0449\u0435 \u043d\u0435 \u0437\u0430\u0441\u043f\u0430\u0432\u043d\u0435\u043d.");
            return;
        }

        PlayerCombat combat = localPlayerObject.GetComponent<PlayerCombat>();
        if (combat == null || !combat.TryAttackNearest())
        {
            SetStatus("\u0410\u0442\u0430\u043a\u0430 \u0441\u0435\u0439\u0447\u0430\u0441 \u043d\u0435\u0434\u043e\u0441\u0442\u0443\u043f\u043d\u0430.");
        }
    }

    private bool PrepareStart()
    {
        SaveNickname();
        CacheNetworkManager();

        if (_networkManager == null)
        {
            SetStatus("\u041d\u0430 \u0441\u0446\u0435\u043d\u0435 \u043e\u0442\u0441\u0443\u0442\u0441\u0442\u0432\u0443\u0435\u0442 NetworkManager.");
            return false;
        }

        if (_networkManager.IsListening)
        {
            SetStatus("\u0421\u0435\u0442\u0435\u0432\u0430\u044f \u0441\u0435\u0441\u0441\u0438\u044f \u0443\u0436\u0435 \u0437\u0430\u043f\u0443\u0449\u0435\u043d\u0430.");
            return false;
        }

        return true;
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "\u0418\u0433\u0440\u043e\u043a" : rawValue.Trim();
    }

    private void ConfigureTransport()
    {
        UnityTransport transport = _networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            return;
        }

        transport.SetConnectionData(_address, _port, "0.0.0.0");
    }

    private void BindButtons()
    {
        if (_hostButton != null)
        {
            _hostButton.onClick.RemoveListener(StartAsHost);
            _hostButton.onClick.AddListener(StartAsHost);
        }

        if (_clientButton != null)
        {
            _clientButton.onClick.RemoveListener(StartAsClient);
            _clientButton.onClick.AddListener(StartAsClient);
        }

        if (_attackButton != null)
        {
            _attackButton.onClick.RemoveListener(AttackNearest);
            _attackButton.onClick.AddListener(AttackNearest);
        }
    }

    private void RegisterCallbacks()
    {
        if (_callbacksRegistered)
        {
            return;
        }

        CacheNetworkManager();
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        _networkManager.OnClientStopped += OnClientStopped;
        _callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!_callbacksRegistered || _networkManager == null)
        {
            return;
        }

        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _networkManager.OnClientStopped -= OnClientStopped;
        _callbacksRegistered = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        CacheNetworkManager();
        if (_networkManager == null)
        {
            return;
        }

        if (clientId == _networkManager.LocalClientId)
        {
            UpdatePanels(true);
            string mode = _networkManager.IsHost
                ? "\u0425\u043e\u0441\u0442"
                : "\u041a\u043b\u0438\u0435\u043d\u0442";
            SetStatus($"{mode} \u0437\u0430\u043f\u0443\u0449\u0435\u043d. \u041d\u0438\u043a: {PlayerNickname}.");
            return;
        }

        SetStatus($"\u041a\u043b\u0438\u0435\u043d\u0442 {clientId} \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u043b\u0441\u044f.");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        CacheNetworkManager();
        if (_networkManager == null)
        {
            return;
        }

        if (clientId == _networkManager.LocalClientId && !_networkManager.IsHost)
        {
            UpdatePanels(false);
            SetStatus("\u0421\u043e\u0435\u0434\u0438\u043d\u0435\u043d\u0438\u0435 \u0441 \u0445\u043e\u0441\u0442\u043e\u043c \u0440\u0430\u0437\u043e\u0440\u0432\u0430\u043d\u043e.");
            return;
        }

        SetStatus($"\u041a\u043b\u0438\u0435\u043d\u0442 {clientId} \u043e\u0442\u043a\u043b\u044e\u0447\u0438\u043b\u0441\u044f.");
    }

    private void OnClientStopped(bool _)
    {
        CacheNetworkManager();
        if (_networkManager == null || _networkManager.IsListening)
        {
            return;
        }

        UpdatePanels(false);
        SetStatus(string.IsNullOrWhiteSpace(_networkManager.DisconnectReason)
            ? "\u0421\u0435\u0441\u0441\u0438\u044f \u043e\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u0430."
            : _networkManager.DisconnectReason);
    }

    private void RefreshSessionState()
    {
        CacheNetworkManager();
        bool sessionActive = _networkManager != null && _networkManager.IsListening;
        UpdatePanels(sessionActive);

        if (!sessionActive)
        {
            return;
        }

        string mode = _networkManager.IsHost
            ? "\u0425\u043e\u0441\u0442"
            : (_networkManager.IsServer ? "\u0421\u0435\u0440\u0432\u0435\u0440" : "\u041a\u043b\u0438\u0435\u043d\u0442");
        SetStatus($"{mode} \u0430\u043a\u0442\u0438\u0432\u0435\u043d \u043d\u0430 {_address}:{_port}.");
    }

    private void UpdatePanels(bool sessionActive)
    {
        if (_backdrop != null)
        {
            _backdrop.SetActive(!sessionActive);
        }

        if (_startPanel != null)
        {
            _startPanel.SetActive(!sessionActive);
        }

        if (_sessionPanel != null)
        {
            _sessionPanel.SetActive(sessionActive);
        }

        if (_statusText != null && sessionActive)
        {
            _statusText.gameObject.SetActive(false);
        }

        if (_attackButton != null)
        {
            _attackButton.interactable = sessionActive;
        }
    }

    private void CacheNetworkManager()
    {
        _networkManager = NetworkManager.Singleton != null
            ? NetworkManager.Singleton
            : FindFirstObjectByType<NetworkManager>();
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            bool shouldShow = !string.IsNullOrWhiteSpace(message) && (_networkManager == null || !_networkManager.IsListening);
            _statusText.gameObject.SetActive(shouldShow);
        }
    }
}
