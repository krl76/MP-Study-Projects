using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using TMPro;
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

    public static string PlayerNickname { get; private set; } = "Игрок";

    private NetworkManager _networkManager;
    private bool _callbacksRegistered;
    private PlayerNetwork _localPlayer;
    private float _respawnCountdownEndsAt;

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

    private void Update()
    {
        CacheNetworkManager();
        if (_networkManager == null || !IsSessionActive())
        {
            return;
        }

        TryBindLocalPlayer();
        RefreshSessionHud();
    }

    private void OnDisable()
    {
        UnbindLocalPlayer();
        UnregisterCallbacks();
    }

    private void OnDestroy()
    {
        UnbindLocalPlayer();
        UnregisterCallbacks();
    }

    public void StartAsHost()
    {
        if (!PrepareStart())
        {
            return;
        }

        ConfigureTransport();
        bool serverStarted = _networkManager.ServerManager.StartConnection();
        bool clientStarted = serverStarted && _networkManager.ClientManager.StartConnection();
        if (serverStarted && clientStarted)
        {
            UpdatePanels(true);
            SetStatus($"Запуск хоста на {_address}:{_port}...");
        }
        else
        {
            SetStatus("Не удалось запустить хост.");
        }
    }

    public void StartAsClient()
    {
        if (!PrepareStart())
        {
            return;
        }

        ConfigureTransport();
        if (_networkManager.ClientManager.StartConnection())
        {
            UpdatePanels(true);
            SetStatus($"Подключение клиента к {_address}:{_port}...");
        }
        else
        {
            SetStatus("Не удалось запустить клиента.");
        }
    }

    private bool PrepareStart()
    {
        SaveNickname();
        CacheNetworkManager();

        if (_networkManager == null)
        {
            SetStatus("На сцене отсутствует FishNet NetworkManager.");
            return false;
        }

        if (IsSessionActive())
        {
            SetStatus("Сетевая сессия уже запущена.");
            return false;
        }

        return true;
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Игрок" : rawValue.Trim();
    }

    private void ConfigureTransport()
    {
        Tugboat transport = _networkManager.GetComponent<Tugboat>();
        if (transport == null)
        {
            return;
        }

        transport.SetClientAddress(_address);
        transport.SetPort(_port);
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
            _attackButton.onClick.RemoveAllListeners();
            _attackButton.interactable = false;
            _attackButton.gameObject.SetActive(false);
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

        _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!_callbacksRegistered || _networkManager == null)
        {
            return;
        }

        _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _callbacksRegistered = false;
    }

    private void OnClientConnectionState(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            UpdatePanels(true);
            TryBindLocalPlayer();
            string mode = _networkManager != null && _networkManager.IsHostStarted ? "Хост" : "Клиент";
            SetStatus($"{mode} запущен. Ник: {PlayerNickname}.");
            return;
        }

        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            UnbindLocalPlayer();
            UpdatePanels(false);
            SetStatus("Сессия остановлена.");
        }
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        if (connection == null)
        {
            return;
        }

        if (args.ConnectionState == RemoteConnectionState.Started)
        {
            SetStatus($"Клиент {connection.ClientId} подключился.");
            return;
        }

        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            SetStatus($"Клиент {connection.ClientId} отключился.");
        }
    }

    private void RefreshSessionState()
    {
        CacheNetworkManager();
        bool sessionActive = IsSessionActive();
        UpdatePanels(sessionActive);

        if (!sessionActive)
        {
            return;
        }

        TryBindLocalPlayer();
        RefreshSessionHud();
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

        if (_attackButton != null)
        {
            _attackButton.gameObject.SetActive(false);
        }
    }

    private void CacheNetworkManager()
    {
        _networkManager = InstanceFinder.NetworkManager != null
            ? InstanceFinder.NetworkManager
            : FindFirstObjectByType<NetworkManager>();
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }
    }

    private void TryBindLocalPlayer()
    {
        if (_networkManager == null || !IsSessionActive())
        {
            UnbindLocalPlayer();
            return;
        }

        PlayerNetwork nextPlayer = FindLocalPlayer();
        if (nextPlayer == _localPlayer)
        {
            return;
        }

        UnbindLocalPlayer();
        _localPlayer = nextPlayer;

        if (_localPlayer == null)
        {
            return;
        }

        _localPlayer.HpChanged += OnLocalStatsChanged;
        _localPlayer.AmmoChanged += OnLocalStatsChanged;
        _localPlayer.AliveChanged += OnLocalAliveChanged;

        if (!_localPlayer.IsAlive)
        {
            _respawnCountdownEndsAt = Time.unscaledTime + _localPlayer.RespawnDelay;
        }
    }

    private void UnbindLocalPlayer()
    {
        if (_localPlayer == null)
        {
            return;
        }

        _localPlayer.HpChanged -= OnLocalStatsChanged;
        _localPlayer.AmmoChanged -= OnLocalStatsChanged;
        _localPlayer.AliveChanged -= OnLocalAliveChanged;
        _localPlayer = null;
        _respawnCountdownEndsAt = 0f;
    }

    private void OnLocalStatsChanged(int _, int __)
    {
        RefreshSessionHud();
    }

    private void OnLocalAliveChanged(bool _, bool isAlive)
    {
        _respawnCountdownEndsAt = isAlive
            ? 0f
            : Time.unscaledTime + (_localPlayer != null ? _localPlayer.RespawnDelay : 0f);
        RefreshSessionHud();
    }

    private void RefreshSessionHud()
    {
        if (_networkManager == null || !IsSessionActive())
        {
            return;
        }

        if (_localPlayer == null)
        {
            SetStatus("Сессия активна. Ожидание спавна локального игрока...");
            return;
        }

        string mode = _networkManager.IsHostStarted
            ? "Хост"
            : (_networkManager.IsServerStarted ? "Сервер" : "Клиент");

        string respawnText = string.Empty;
        if (!_localPlayer.IsAlive)
        {
            float secondsRemaining = Mathf.Max(0f, _respawnCountdownEndsAt - Time.unscaledTime);
            respawnText = $"\nВозрождение через {secondsRemaining:0.0} сек.";
        }

        SetStatus(
            $"{mode} активен на {_address}:{_port}\n" +
            $"Здоровье: {_localPlayer.HP}/{_localPlayer.MaxHp} | Патроны: {_localPlayer.Ammo}\n" +
            "Управление: WASD для перемещения, пробел для выстрела." +
            respawnText
        );
    }

    private bool IsSessionActive()
    {
        return _networkManager != null && (_networkManager.IsClientStarted || _networkManager.IsServerStarted);
    }

    private static PlayerNetwork FindLocalPlayer()
    {
        foreach (PlayerNetwork player in PlayerNetwork.SpawnedPlayers)
        {
            if (player != null && player.IsOwner)
            {
                return player;
            }
        }

        return null;
    }
}
