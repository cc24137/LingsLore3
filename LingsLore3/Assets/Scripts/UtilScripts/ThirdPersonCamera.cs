using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private InputActionReference lookAction;

    [SerializeField] private float targetHeight = 1.2f;
    [SerializeField, Min(0.1f)] private float distance = 6f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float stickDegreesPerSecond = 180f;

    [SerializeField] private float minPitch = -58f;
    [SerializeField] private float maxPitch = 65f;

    [SerializeField] private LayerMask obstacleMask;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.25f;
    [SerializeField, Min(0f)] private float collisionPadding = 0.1f;

    [SerializeField] private PlayerMotor playerMotor;
    
    [SerializeField, Range(1f, 179f)]
    private float sprintFieldOfView = 75f;
    
    [SerializeField, Min(0.1f)]
    private float fovChangeSpeed = 60f;
    
    private Camera cameraComponent;
    private InputAction lookStickAction;
    private float normalFieldOfView;

    private float yaw;
    private float pitch = 20f;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        normalFieldOfView = cameraComponent.fieldOfView;
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
        lookStickAction = lookAction.action.actionMap.FindAction("LookStick");
        lookStickAction?.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction.action.Disable();
        lookStickAction?.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameraComponent != null)
            cameraComponent.fieldOfView = normalFieldOfView;
    }

    private void Update()
    {
        if (MenuPausa.BloqueiaEntradaDeJogo || Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 mouseDelta = lookAction.action.ReadValue<Vector2>();
        Vector2 stick = lookStickAction?.ReadValue<Vector2>() ?? Vector2.zero;
        Vector2 rotationDelta = mouseDelta * mouseSensitivity + stick * (stickDegreesPerSecond * Time.deltaTime);

        yaw += rotationDelta.x;
        pitch -= rotationDelta.y;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateFieldOfView()
    {
        float targetFov = playerMotor.IsSprinting
            ? sprintFieldOfView
            : normalFieldOfView;
    
        cameraComponent.fieldOfView = Mathf.MoveTowards(
            cameraComponent.fieldOfView,
            targetFov,
            fovChangeSpeed * Time.deltaTime
        );
    }

    private void LateUpdate()
    {
        if (MenuPausa.BloqueiaEntradaDeJogo)
            return;

        Vector3 focusPoint =
            target.position + Vector3.up * targetHeight;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 backwards = rotation * Vector3.back;
        float actualDistance = distance;

        if (Physics.SphereCast(
            focusPoint,
            collisionRadius,
            backwards,
            out RaycastHit hit,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore))
        {
            actualDistance = Mathf.Max(
                0.05f,
                hit.distance - collisionPadding
            );
        }

        Vector3 cameraPosition =
            focusPoint + backwards * actualDistance;

        transform.SetPositionAndRotation(cameraPosition, rotation);

        UpdateFieldOfView();
    }
}
