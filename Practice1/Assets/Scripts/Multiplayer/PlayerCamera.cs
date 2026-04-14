using Unity.Netcode;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Vector3 _offset = new Vector3(0f, 8f, -6f);
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private float _followSmoothTime = 0.05f;
    [SerializeField] private float _rotationLerpSpeed = 14f;

    private Camera _camera;
    private Vector3 _cameraVelocity;
    private bool _isInitialized;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        _camera = Camera.main;
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                return;
            }

            SnapToTarget();
        }

        Vector3 targetPosition = transform.position + _offset;
        Vector3 lookTarget = transform.position + _lookOffset;

        if (!_isInitialized)
        {
            SnapToTarget();
            targetPosition = _camera.transform.position;
            lookTarget = transform.position + _lookOffset;
        }

        _camera.transform.position = Vector3.SmoothDamp(
            _camera.transform.position,
            targetPosition,
            ref _cameraVelocity,
            _followSmoothTime
        );

        Vector3 viewDirection = lookTarget - _camera.transform.position;
        if (viewDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(viewDirection.normalized, Vector3.up);
        _camera.transform.rotation = Quaternion.Slerp(
            _camera.transform.rotation,
            targetRotation,
            _rotationLerpSpeed * Time.deltaTime
        );
    }

    private void SnapToTarget()
    {
        if (_camera == null)
        {
            return;
        }

        Vector3 targetPosition = transform.position + _offset;
        Vector3 lookTarget = transform.position + _lookOffset;
        _camera.transform.position = targetPosition;
        _camera.transform.rotation = Quaternion.LookRotation((lookTarget - targetPosition).normalized, Vector3.up);
        _cameraVelocity = Vector3.zero;
        _isInitialized = true;
    }
}
