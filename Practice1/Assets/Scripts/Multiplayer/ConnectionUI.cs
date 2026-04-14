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

        EnsureRequiredNetworkPrefabs();

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
            UnbindLocalPlayer();
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

        UnbindLocalPlayer();
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
            SetStatus("\u0421\u0435\u0441\u0441\u0438\u044f \u0430\u043a\u0442\u0438\u0432\u043d\u0430. \u041e\u0436\u0438\u0434\u0430\u043d\u0438\u0435 \u0441\u043f\u0430\u0432\u043d\u0430 \u043b\u043e\u043a\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u0438\u0433\u0440\u043e\u043a\u0430...");
            return;
        }

        string mode = _networkManager.IsHost
            ? "\u0425\u043e\u0441\u0442"
            : (_networkManager.IsServer ? "\u0421\u0435\u0440\u0432\u0435\u0440" : "\u041a\u043b\u0438\u0435\u043d\u0442");

        string respawnText = string.Empty;
        if (!_localPlayer.IsAlive.Value)
        {
            float secondsRemaining = Mathf.Max(0f, _respawnCountdownEndsAt - Time.unscaledTime);
            respawnText = $"\n\u0412\u043e\u0437\u0440\u043e\u0436\u0434\u0435\u043d\u0438\u0435 \u0447\u0435\u0440\u0435\u0437 {secondsRemaining:0.0} \u0441\u0435\u043a.";
        }

        SetStatus(
            $"{mode} \u0430\u043a\u0442\u0438\u0432\u0435\u043d \u043d\u0430 {_address}:{_port}\n" +
            $"HP: {_localPlayer.HP.Value}/{_localPlayer.MaxHp} | \u041f\u0430\u0442\u0440\u043e\u043d\u044b: {_localPlayer.Ammo.Value}\n" +
            "WASD - \u0434\u0432\u0438\u0436\u0435\u043d\u0438\u0435, Space - \u0432\u044b\u0441\u0442\u0440\u0435\u043b" +
            respawnText
        );
    }
}
