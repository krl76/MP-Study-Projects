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
    [SerializeField] private GameObject _lobbyPanel;
    [SerializeField] private GameObject _resultsPanel;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private TMP_Text _lobbyText;
    [SerializeField] private TMP_Text _matchText;
    [SerializeField] private TMP_Text _resultsText;
    [SerializeField] private string _address = "127.0.0.1";
    [SerializeField] private ushort _port = 7777;

    public static string PlayerNickname { get; private set; } = "Игрок";

    private NetworkManager _networkManager;
    private bool _callbacksRegistered;
    private PlayerNetwork _localPlayer;
    private GameManager _gameManager;
    private float _respawnCountdownEndsAt;

    private void Awake()
    {
        ResolveOptionalUiReferences();
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
        UnbindGameManager();
        UnregisterCallbacks();
    }

    private void OnDestroy()
    {
        UnbindLocalPlayer();
        UnbindGameManager();
        UnregisterCallbacks();
    }

    private void ResolveOptionalUiReferences()
    {
        _lobbyPanel ??= FindChildGameObject("LobbyPanel");
        _resultsPanel ??= FindChildGameObject("ResultsPanel");
        _lobbyText ??= FindChildComponent<TMP_Text>("LobbyText");
        _matchText ??= FindChildComponent<TMP_Text>("MatchText");
        _resultsText ??= FindChildComponent<TMP_Text>("ResultsText");

        if (_lobbyPanel == null)
        {
            _lobbyPanel = CreateOverlayPanel("LobbyPanel", new Vector2(620f, 180f), new Vector2(0f, 220f));
            _lobbyText = CreateOverlayText("LobbyText", _lobbyPanel.transform, "Ожидание игроков...", 30f);
        }

        if (_matchText == null)
        {
            _matchText = CreateOverlayText("MatchText", transform, string.Empty, 28f);
            RectTransform matchRect = _matchText.rectTransform;
            matchRect.anchorMin = new Vector2(0.5f, 1f);
            matchRect.anchorMax = new Vector2(0.5f, 1f);
            matchRect.pivot = new Vector2(0.5f, 1f);
            matchRect.sizeDelta = new Vector2(720f, 48f);
            matchRect.anchoredPosition = new Vector2(0f, -24f);
        }

        if (_resultsPanel == null)
        {
            _resultsPanel = CreateOverlayPanel("ResultsPanel", new Vector2(700f, 420f), Vector2.zero);
            _resultsText = CreateOverlayText("ResultsText", _resultsPanel.transform, "Результаты матча", 28f);
        }
    }

    private GameObject FindChildGameObject(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private GameObject CreateOverlayPanel(string panelName, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.08f, 0.11f, 0.16f, 0.92f);
        return panel;
    }

    private TMP_Text CreateOverlayText(string textName, Transform parent, string value, float fontSize)
    {
        GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(28f, 24f);
        rect.offsetMax = new Vector2(-28f, -24f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
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

        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(false);
        }

        if (_resultsPanel != null)
        {
            _resultsPanel.SetActive(false);
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
        _localPlayer.ScoreChanged += OnLocalStatsChanged;
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
        _localPlayer.ScoreChanged -= OnLocalStatsChanged;
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

        if (_networkManager.IsServerStarted)
        {
            GameManager.EnsureServerInstance();
        }

        TryBindGameManager();
        if (_localPlayer == null)
        {
            RefreshPanelsForGameState();
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

        RefreshPanelsForGameState();

        string stateText = _gameManager != null
            ? GetGameStateStatusText()
            : "Состояние матча ещё не синхронизировано.";

        SetStatus(
            $"{mode} активен на {_address}:{_port}\n" +
            stateText + "\n" +
            $"Здоровье: {_localPlayer.HP}/{_localPlayer.MaxHp} | Патроны: {_localPlayer.Ammo} | Очки: {_localPlayer.Score}\n" +
            "Управление: WASD для перемещения, пробел для выстрела." +
            respawnText
        );
    }

    private void TryBindGameManager()
    {
        GameManager nextGameManager = GameManager.Instance != null
            ? GameManager.Instance
            : FindFirstObjectByType<GameManager>();

        if (nextGameManager == _gameManager)
        {
            return;
        }

        UnbindGameManager();
        _gameManager = nextGameManager;
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.GameStateChanged += OnGameStateChanged;
        _gameManager.ConnectedPlayersChanged += OnConnectedPlayersChanged;
        _gameManager.MatchTimerChanged += OnMatchTimerChanged;
        _gameManager.LobbyStartCountdownChanged += OnLobbyStartCountdownChanged;
        RefreshPanelsForGameState();
    }

    private void UnbindGameManager()
    {
        if (_gameManager == null)
        {
            return;
        }

        _gameManager.GameStateChanged -= OnGameStateChanged;
        _gameManager.ConnectedPlayersChanged -= OnConnectedPlayersChanged;
        _gameManager.MatchTimerChanged -= OnMatchTimerChanged;
        _gameManager.LobbyStartCountdownChanged -= OnLobbyStartCountdownChanged;
        _gameManager = null;
    }

    private void OnGameStateChanged(GameState _, GameState __)
    {
        RefreshPanelsForGameState();
    }

    private void OnConnectedPlayersChanged(int _, int __)
    {
        RefreshPanelsForGameState();
    }

    private void OnMatchTimerChanged(float _, float __)
    {
        RefreshPanelsForGameState();
    }

    private void OnLobbyStartCountdownChanged(float _, float __)
    {
        RefreshPanelsForGameState();
    }

    private void RefreshPanelsForGameState()
    {
        if (_gameManager == null)
        {
            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
            }

            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(false);
            }

            return;
        }

        bool waiting = _gameManager.CurrentState == GameState.WaitingForPlayers;
        bool inProgress = _gameManager.CurrentState == GameState.InProgress;
        bool showingResults = _gameManager.CurrentState == GameState.ShowingResults;

        if (_lobbyPanel != null)
        {
            _lobbyPanel.SetActive(waiting);
        }

        if (_resultsPanel != null)
        {
            _resultsPanel.SetActive(showingResults);
        }

        if (_lobbyText != null)
        {
            _lobbyText.text = GetLobbyText();
        }

        if (_matchText != null)
        {
            _matchText.text = inProgress ? GetMatchText() : string.Empty;
            _matchText.gameObject.SetActive(inProgress);
        }

        if (_resultsText != null)
        {
            _resultsText.text = GetResultsText();
        }
    }

    private string GetGameStateStatusText()
    {
        if (_gameManager.CurrentState == GameState.WaitingForPlayers)
        {
            return GetLobbyText();
        }

        if (_gameManager.CurrentState == GameState.ShowingResults)
        {
            return "Матч завершён. Итоги показаны на экране.";
        }

        return GetMatchText();
    }

    private string GetLobbyText()
    {
        if (_gameManager == null)
        {
            return "Ожидание данных лобби...";
        }

        string countdown = _gameManager.LobbyStartCountdown > 0f
            ? $"\nСтарт через {_gameManager.LobbyStartCountdown:0.0} сек."
            : "\nОжидание игроков...";

        return $"Ожидание игроков: {_gameManager.ConnectedPlayers}/{_gameManager.RequiredPlayers}" + countdown;
    }

    private string GetMatchText()
    {
        if (_gameManager == null)
        {
            return "Матч запускается...";
        }

        return $"Матч идёт. Осталось: {_gameManager.MatchTimer:0.0} сек.";
    }

    private string GetResultsText()
    {
        string result = "Результаты матча\n";
        foreach (PlayerNetwork player in PlayerNetwork.SpawnedPlayers)
        {
            if (player == null)
            {
                continue;
            }

            result += $"{player.Nickname}: {player.Score} очк.\n";
        }

        result += "\nВозврат в лобби через несколько секунд...";
        return result;
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
