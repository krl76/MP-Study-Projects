using FishNet.Object;
using FishNet.Object.Prediction;
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
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _gravity = -18f;

    private CharacterController _characterController;
    private PlayerNetwork _playerNetwork;
    private float _verticalVelocity;

    private void Awake()
    {
        _playerNetwork = GetComponent<PlayerNetwork>();
        _characterController = GetComponent<CharacterController>();
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

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        base.TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= OnTick;
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
        if (base.IsOwner)
        {
            Vector2 moveInput = ReadMoveInput();
            Replicate(new MoveData(moveInput.x, moveInput.y));
        }

        CreateReconcile();
    }

    public override void CreateReconcile()
    {
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

        float tickDelta = (float)base.TimeManager.TickDelta;
        _verticalVelocity += _gravity * tickDelta;
        Vector3 velocity = (planarMove * _speed) + (Vector3.up * _verticalVelocity);
        _characterController.Move(velocity * tickDelta);
    }

    [Reconcile]
    private void Reconcile(ReconcileData reconcileData, Channel channel = Channel.Unreliable)
    {
        if (_characterController == null)
        {
            transform.SetPositionAndRotation(reconcileData.Position, reconcileData.Rotation);
            _verticalVelocity = reconcileData.VerticalVelocity;
            return;
        }

        bool controllerWasEnabled = _characterController.enabled;
        if (controllerWasEnabled)
        {
            _characterController.enabled = false;
        }

        transform.SetPositionAndRotation(reconcileData.Position, reconcileData.Rotation);
        _verticalVelocity = reconcileData.VerticalVelocity;

        if (controllerWasEnabled)
        {
            _characterController.enabled = true;
        }
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
