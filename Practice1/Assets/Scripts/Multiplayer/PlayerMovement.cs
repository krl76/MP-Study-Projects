using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Connection;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public struct MoveData : IReplicateData
{
    public float Horizontal;
    public float Vertical;

    private uint _tick;

    public MoveData(float horizontal, float vertical)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        _tick = 0u;
    }

    public void Dispose()
    {
    }

    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

public struct ReconcileData : IReconcileData
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float VerticalVelocity;

    private uint _tick;

    public ReconcileData(Vector3 position, Quaternion rotation, float verticalVelocity)
    {
        Position = position;
        Rotation = rotation;
        VerticalVelocity = verticalVelocity;
        _tick = 0u;
    }

    public void Dispose()
    {
    }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

[RequireComponent(typeof(PlayerNetwork))]
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    private const string GraphicsChildName = "Graphics";

    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _gravity = -18f;
    [SerializeField] private bool _usePrediction = true;
    [SerializeField] private Key _togglePredictionKey = Key.F2;

    private CharacterController _characterController;
    private PlayerNetwork _playerNetwork;
    private float _verticalVelocity;

    public bool UsePrediction => _usePrediction;

    private void Awake()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();
        _characterController = GetComponent<CharacterController>();
        EnsurePredictionGraphicsObject();
        if (_characterController == null)
        {
            Debug.LogError("PlayerMovement requires CharacterController on the player prefab.");
            enabled = false;
            return;
        }

        _characterController.height = 2f;
        _characterController.radius = 0.45f;
        _characterController.center = Vector3.up;
        _characterController.minMoveDistance = 0f;

        CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }
    }

    private void EnsurePredictionGraphicsObject()
    {
        if (base.NetworkObject == null || base.NetworkObject.GetGraphicalObject() != null)
        {
            return;
        }

        MeshFilter rootMeshFilter = GetComponent<MeshFilter>();
        MeshRenderer rootMeshRenderer = GetComponent<MeshRenderer>();
        if (rootMeshFilter == null || rootMeshRenderer == null)
        {
            return;
        }

        Transform graphicsTransform = transform.Find(GraphicsChildName);
        if (graphicsTransform == null)
        {
            GameObject graphicsObject = new GameObject(GraphicsChildName);
            graphicsTransform = graphicsObject.transform;
            graphicsTransform.SetParent(transform, false);
        }

        MeshFilter graphicsMeshFilter = graphicsTransform.GetComponent<MeshFilter>();
        if (graphicsMeshFilter == null)
        {
            graphicsMeshFilter = graphicsTransform.gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer graphicsMeshRenderer = graphicsTransform.GetComponent<MeshRenderer>();
        if (graphicsMeshRenderer == null)
        {
            graphicsMeshRenderer = graphicsTransform.gameObject.AddComponent<MeshRenderer>();
        }

        graphicsMeshFilter.sharedMesh = rootMeshFilter.sharedMesh;
        graphicsMeshRenderer.sharedMaterials = rootMeshRenderer.sharedMaterials;
        rootMeshRenderer.enabled = false;
        base.NetworkObject.SetGraphicalObject(graphicsTransform);
    }

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        base.TimeManager.OnTick += OnTick;
        base.TimeManager.OnPostTick += OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= OnTick;
        base.TimeManager.OnPostTick -= OnPostTick;
        base.OnStopNetwork();
    }

    public void TeleportServer(Vector3 position, Quaternion rotation)
    {
        if (!base.IsServerInitialized)
        {
            return;
        }

        bool controllerWasEnabled = _characterController != null && _characterController.enabled;
        if (controllerWasEnabled)
        {
            _characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);
        _verticalVelocity = 0f;

        if (controllerWasEnabled)
        {
            _characterController.enabled = true;
        }

        Reconcile(new ReconcileData(transform.position, transform.rotation, _verticalVelocity));
    }

    private void OnTick()
    {
        if (_usePrediction)
        {
            if (base.IsOwner)
            {
                TryTogglePredictionMode();
                Vector2 moveInput = ReadMoveInput();
                Replicate(new MoveData(moveInput.x, moveInput.y));
            }

            if (base.IsServerInitialized && !base.IsOwner)
            {
                Replicate(default);
            }

            return;
        }

        if (base.IsOwner)
        {
            TryTogglePredictionMode();
            Vector2 moveInput = ReadMoveInput();
            MoveWithoutPredictionServerRpc(moveInput.x, moveInput.y);
        }
    }

    private void OnPostTick()
    {
        if (base.IsServerInitialized && _usePrediction)
        {
            CreateReconcile();
        }
    }

    public override void CreateReconcile()
    {
        if (!base.IsServerInitialized || !_usePrediction)
        {
            return;
        }

        Reconcile(new ReconcileData(transform.position, transform.rotation, _verticalVelocity));
    }

    [Replicate]
    private void Replicate(MoveData moveData, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        if (!base.IsOwner && !base.IsServerInitialized)
        {
            return;
        }

        if (_playerNetwork == null || !_playerNetwork.IsAlive || _characterController == null)
        {
            return;
        }

        ApplyMove(moveData, (float)base.TimeManager.TickDelta);
    }

    [ServerRpc]
    private void MoveWithoutPredictionServerRpc(float horizontal, float vertical)
    {
        if (_playerNetwork == null || !_playerNetwork.IsAlive || _characterController == null)
        {
            return;
        }

        MoveData moveData = new MoveData(horizontal, vertical);
        ApplyMove(moveData, (float)base.TimeManager.TickDelta);
        ApplyAuthoritativeStateTargetRpc(base.Owner, transform.position, transform.rotation, _verticalVelocity);
    }

    [TargetRpc]
    private void ApplyAuthoritativeStateTargetRpc(NetworkConnection connection, Vector3 position, Quaternion rotation, float verticalVelocity, Channel channel = Channel.Reliable)
    {
        if (_usePrediction)
        {
            return;
        }

        ApplyAuthoritativeState(position, rotation, verticalVelocity);
    }

    private void ApplyMove(MoveData moveData, float delta)
    {
        Vector3 planarMove = new Vector3(moveData.Horizontal, 0f, moveData.Vertical);
        if (planarMove.sqrMagnitude > 1f)
        {
            planarMove.Normalize();
        }

        if (planarMove.sqrMagnitude > 0.001f)
        {
            transform.forward = planarMove;
        }

        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -1f;
        }

        _verticalVelocity += _gravity * delta;
        Vector3 velocity = (planarMove * _speed) + (Vector3.up * _verticalVelocity);
        _characterController.Move(velocity * delta);
    }

    [Reconcile]
    private void Reconcile(ReconcileData reconcileData, Channel channel = Channel.Unreliable)
    {
        if (!_usePrediction)
        {
            return;
        }

        ApplyAuthoritativeState(reconcileData.Position, reconcileData.Rotation, reconcileData.VerticalVelocity);
    }

    private void ApplyAuthoritativeState(Vector3 position, Quaternion rotation, float verticalVelocity)
    {
        if (_characterController == null)
        {
            transform.SetPositionAndRotation(position, rotation);
            _verticalVelocity = verticalVelocity;
            return;
        }

        bool controllerWasEnabled = _characterController.enabled;
        if (controllerWasEnabled)
        {
            _characterController.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);
        _verticalVelocity = verticalVelocity;

        if (controllerWasEnabled)
        {
            _characterController.enabled = true;
        }
    }

    private void TryTogglePredictionMode()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[_togglePredictionKey].wasPressedThisFrame)
        {
            return;
        }

        _usePrediction = !_usePrediction;
        SetPredictionModeServerRpc(_usePrediction);
        Debug.Log($"Client-side prediction {(_usePrediction ? "enabled" : "disabled")} for {name}.");
    }

    [ServerRpc]
    private void SetPredictionModeServerRpc(bool usePrediction)
    {
        _usePrediction = usePrediction;
    }

    private Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }
        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }
        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }
}
