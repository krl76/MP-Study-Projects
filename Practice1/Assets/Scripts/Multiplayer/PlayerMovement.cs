using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private void Update()
    {
        if (!IsOwner || !IsSpawned || _playerNetwork == null || !_playerNetwork.IsAlive.Value)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 moveInput = ReadMoveInput(keyboard);
        Vector3 planarMove = new Vector3(moveInput.x, 0f, moveInput.y);
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

        _verticalVelocity += _gravity * Time.deltaTime;
        Vector3 velocity = (planarMove * _speed) + (Vector3.up * _verticalVelocity);
        _characterController.Move(velocity * Time.deltaTime);
    }

    private Vector2 ReadMoveInput(Keyboard keyboard)
    {
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
