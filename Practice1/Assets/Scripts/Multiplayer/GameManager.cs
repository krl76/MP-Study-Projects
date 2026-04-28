using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    [SerializeField] private int _requiredPlayers = 2;
    [SerializeField] private float _matchDuration = 60f;
    [SerializeField] private float _resultsDuration = 5f;
    [SerializeField] private float _lobbyAutoStartDelay = 3f;

    private readonly SyncVar<GameState> _currentState = new SyncVar<GameState>(GameState.WaitingForPlayers);
    private readonly SyncVar<int> _connectedPlayers = new SyncVar<int>();
    private readonly SyncVar<float> _matchTimer = new SyncVar<float>(60f);
    private readonly SyncVar<float> _lobbyStartCountdown = new SyncVar<float>();

    private bool _callbacksRegistered;
    private float _resultsTimer;

    public static GameManager Instance { get; private set; }

    public int RequiredPlayers => _requiredPlayers;
    public GameState CurrentState => _currentState.Value;
    public int ConnectedPlayers => _connectedPlayers.Value;
    public float MatchTimer => _matchTimer.Value;
    public float LobbyStartCountdown => _lobbyStartCountdown.Value;

    public event Action<GameState, GameState> GameStateChanged;
    public event Action<int, int> ConnectedPlayersChanged;
    public event Action<float, float> MatchTimerChanged;
    public event Action<float, float> LobbyStartCountdownChanged;

    private void Awake()
    {
        Instance = this;
        _matchTimer.Value = _matchDuration;
        _currentState.OnChange += OnGameStateSyncChanged;
        _connectedPlayers.OnChange += OnConnectedPlayersSyncChanged;
        _matchTimer.OnChange += OnMatchTimerSyncChanged;
        _lobbyStartCountdown.OnChange += OnLobbyStartCountdownSyncChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RegisterCallbacks();
        ResetRoundServer(keepState: true);
        RefreshConnectedPlayersServer();
        Debug.Log("[Server] GameManager started. Waiting for players...");
    }

    private void Update()
    {
        if (!base.IsServerInitialized)
        {
            return;
        }

        RefreshConnectedPlayersServer();

        if (_currentState.Value == GameState.WaitingForPlayers)
        {
            TickLobbyServer();
            return;
        }

        if (_currentState.Value == GameState.InProgress)
        {
            TickMatchServer();
            return;
        }

        if (_currentState.Value == GameState.ShowingResults)
        {
            TickResultsServer();
        }
    }

    private void OnDestroy()
    {
        UnregisterCallbacks();
        _currentState.OnChange -= OnGameStateSyncChanged;
        _connectedPlayers.OnChange -= OnConnectedPlayersSyncChanged;
        _matchTimer.OnChange -= OnMatchTimerSyncChanged;
        _lobbyStartCountdown.OnChange -= OnLobbyStartCountdownSyncChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RegisterCallbacks()
    {
        if (_callbacksRegistered || base.ServerManager == null)
        {
            return;
        }

        base.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        _callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!_callbacksRegistered || base.ServerManager == null)
        {
            return;
        }

        base.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
        _callbacksRegistered = false;
    }

    private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
    {
        RefreshConnectedPlayersServer();
    }

    private void TickLobbyServer()
    {
        if (_connectedPlayers.Value < _requiredPlayers)
        {
            _lobbyStartCountdown.Value = 0f;
            return;
        }

        if (_lobbyStartCountdown.Value <= 0f)
        {
            _lobbyStartCountdown.Value = _lobbyAutoStartDelay;
        }

        _lobbyStartCountdown.Value = Mathf.Max(0f, _lobbyStartCountdown.Value - Time.deltaTime);
        if (_lobbyStartCountdown.Value <= 0f)
        {
            StartMatchServer();
        }
    }

    private void TickMatchServer()
    {
        if (_connectedPlayers.Value < _requiredPlayers)
        {
            EndMatchServer("not enough players");
            return;
        }

        _matchTimer.Value = Mathf.Max(0f, _matchTimer.Value - Time.deltaTime);
        if (_matchTimer.Value <= 0f)
        {
            EndMatchServer("timer expired");
        }
    }

    private void TickResultsServer()
    {
        _resultsTimer -= Time.deltaTime;
        if (_resultsTimer <= 0f)
        {
            ResetToLobbyServer();
        }
    }

    private void RefreshConnectedPlayersServer()
    {
        if (base.ServerManager == null)
        {
            _connectedPlayers.Value = 0;
            return;
        }

        _connectedPlayers.Value = base.ServerManager.Clients.Count;
    }

    private void StartMatchServer()
    {
        if (_currentState.Value != GameState.WaitingForPlayers)
        {
            return;
        }

        ResetRoundServer(keepState: true);
        _matchTimer.Value = _matchDuration;
        _lobbyStartCountdown.Value = 0f;
        _currentState.Value = GameState.InProgress;
        Debug.Log("[Server] Match started!");
    }

    private void EndMatchServer(string reason)
    {
        if (_currentState.Value != GameState.InProgress)
        {
            return;
        }

        _resultsTimer = _resultsDuration;
        _currentState.Value = GameState.ShowingResults;
        Debug.Log($"[Server] Match ended ({reason}). Showing results...");
    }

    private void ResetToLobbyServer()
    {
        ResetRoundServer(keepState: false);
        _currentState.Value = GameState.WaitingForPlayers;
        Debug.Log("[Server] Lobby reset. Waiting for players...");
    }

    private void ResetRoundServer(bool keepState)
    {
        foreach (PlayerNetwork player in GetSpawnedPlayersSnapshot())
        {
            player.ResetForMatchServer();
        }

        _matchTimer.Value = _matchDuration;
        _lobbyStartCountdown.Value = 0f;

        if (!keepState)
        {
            _currentState.Value = GameState.WaitingForPlayers;
        }
    }

    private static List<PlayerNetwork> GetSpawnedPlayersSnapshot()
    {
        List<PlayerNetwork> players = new List<PlayerNetwork>();
        foreach (PlayerNetwork player in PlayerNetwork.SpawnedPlayers)
        {
            if (player != null && player.IsSpawned)
            {
                players.Add(player);
            }
        }

        return players;
    }

    public static GameManager EnsureServerInstance()
    {
        GameManager existing = Instance != null
            ? Instance
            : FindFirstObjectByType<GameManager>();
        if (existing != null)
        {
            return existing;
        }

        Debug.LogError("[Server] GameManager scene NetworkObject is missing. Rebuild MainScene from Tools/FishNet Practice/Rebuild Main Scene.");
        return null;
    }

    private void OnGameStateSyncChanged(GameState previous, GameState next, bool asServer)
    {
        GameStateChanged?.Invoke(previous, next);
        Debug.Log($"Game state changed: {previous} -> {next}");
    }

    private void OnConnectedPlayersSyncChanged(int previous, int next, bool asServer)
    {
        ConnectedPlayersChanged?.Invoke(previous, next);
    }

    private void OnMatchTimerSyncChanged(float previous, float next, bool asServer)
    {
        MatchTimerChanged?.Invoke(previous, next);
    }

    private void OnLobbyStartCountdownSyncChanged(float previous, float next, bool asServer)
    {
        LobbyStartCountdownChanged?.Invoke(previous, next);
    }
}
