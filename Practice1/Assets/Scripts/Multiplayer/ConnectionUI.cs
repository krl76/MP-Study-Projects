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

    public static string PlayerNickname { get; private set; } = "Игрок";

    private NetworkManager _networkManager;
    private bool _callbacksRegistered;
    private PlayerNetwork _localPlayer;
    private float _respawnCountdownEndsAt;

    private void Awake()
    {
        BindButtons();
        CacheNetworkManager();
        EnsureRequiredNetworkPrefabs();
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
        if (_networkManager == null || !_networkManager.IsListening)
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
        if (_networkManager.StartHost())
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
        if (_networkManager.StartClient())
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
            SetStatus("На сцене отсутствует NetworkManager.");
            return false;
        }

        if (_networkManager.IsListening)
        {
            SetStatus("Сетевая сессия уже запущена.");
            return false;
        }

        EnsureRequiredNetworkPrefabs();

        return true;
    }

    private void SaveNickname()
    {
        string rawValue = _nicknameInput != null ? _nicknameInput.text : string.Empty;
        PlayerNickname = string.IsNullOrWhiteSpace(rawValue) ? "Игрок" : rawValue.Trim();
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
            TryBindLocalPlayer();
            string mode = _networkManager.IsHost
                ? "Хост"
                : "Клиент";
            SetStatus($"{mode} запущен. Ник: {PlayerNickname}.");
            return;
        }

        SetStatus($"Клиент {clientId} подключился.");
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
            UnbindLocalPlayer();
            UpdatePanels(false);
            SetStatus("Соединение с хостом разорвано.");
            return;
        }

        SetStatus($"Клиент {clientId} отключился.");
    }

    private void OnClientStopped(bool _)
    {
        CacheNetworkManager();
        if (_networkManager == null || _networkManager.IsListening)
        {
            return;
        }

        UnbindLocalPlayer();
        UpdatePanels(false);
        SetStatus(string.IsNullOrWhiteSpace(_networkManager.DisconnectReason)
            ? "Сессия остановлена."
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
        _networkManager = NetworkManager.Singleton != null
            ? NetworkManager.Singleton
            : FindFirstObjectByType<NetworkManager>();
    }

    private void EnsureRequiredNetworkPrefabs()
    {
        if (_networkManager == null)
        {
            return;
        }

        RegisterNetworkPrefab(_networkManager.NetworkConfig.PlayerPrefab);

        GameObject playerPrefab = _networkManager.NetworkConfig.PlayerPrefab;
        if (playerPrefab != null)
        {
            PlayerShooting shooting = playerPrefab.GetComponent<PlayerShooting>();
            RegisterNetworkPrefab(shooting != null ? shooting.ProjectilePrefab : null);
        }

        PickupManager pickupManager = FindFirstObjectByType<PickupManager>();
        if (pickupManager != null)
        {
            RegisterNetworkPrefab(pickupManager.HealthPickupPrefab);
        }
    }

    private void RegisterNetworkPrefab(GameObject prefab)
    {
        if (_networkManager == null || prefab == null)
        {
            return;
        }

        NetworkPrefabs prefabs = _networkManager.NetworkConfig.Prefabs;
        if (prefabs.Contains(prefab) || IsPrefabInConfiguredLists(prefab, prefabs))
        {
            return;
        }

        prefabs.Add(new NetworkPrefab
        {
            Prefab = prefab
        });
    }

    private static bool IsPrefabInConfiguredLists(GameObject prefab, NetworkPrefabs prefabs)
    {
        for (int i = 0; i < prefabs.NetworkPrefabsLists.Count; i++)
        {
            NetworkPrefabsList list = prefabs.NetworkPrefabsLists[i];
            if (list != null && list.Contains(prefab))
            {
                return true;
            }
        }

        return false;
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
        if (_networkManager == null || !_networkManager.IsListening)
        {
            UnbindLocalPlayer();
            return;
        }

        NetworkObject localPlayerObject = _networkManager.SpawnManager?.GetLocalPlayerObject();
        PlayerNetwork nextPlayer = localPlayerObject != null
            ? localPlayerObject.GetComponent<PlayerNetwork>()
            : null;

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

        _localPlayer.HP.OnValueChanged += OnLocalStatsChanged;
        _localPlayer.Ammo.OnValueChanged += OnLocalStatsChanged;
        _localPlayer.IsAlive.OnValueChanged += OnLocalAliveChanged;

        if (!_localPlayer.IsAlive.Value)
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

        _localPlayer.HP.OnValueChanged -= OnLocalStatsChanged;
        _localPlayer.Ammo.OnValueChanged -= OnLocalStatsChanged;
        _localPlayer.IsAlive.OnValueChanged -= OnLocalAliveChanged;
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
        if (_networkManager == null || !_networkManager.IsListening)
        {
            return;
        }

        if (_localPlayer == null)
        {
            SetStatus("Сессия активна. Ожидание спавна локального игрока...");
            return;
        }

        string mode = _networkManager.IsHost
            ? "Хост"
            : (_networkManager.IsServer ? "Сервер" : "Клиент");

        string respawnText = string.Empty;
        if (!_localPlayer.IsAlive.Value)
        {
            float secondsRemaining = Mathf.Max(0f, _respawnCountdownEndsAt - Time.unscaledTime);
            respawnText = $"\nВозрождение через {secondsRemaining:0.0} сек.";
        }

        SetStatus(
            $"{mode} активен на {_address}:{_port}\n" +
            $"Здоровье: {_localPlayer.HP.Value}/{_localPlayer.MaxHp} | Патроны: {_localPlayer.Ammo.Value}\n" +
            "Управление: WASD для перемещения, пробел для выстрела." +
            respawnText
        );
    }
}
