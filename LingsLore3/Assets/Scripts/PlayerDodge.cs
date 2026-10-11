using UnityEngine;

// Executa o dodge. Vida, chao e custo de stamina sao verificados no motor.
[RequireComponent(typeof(Rigidbody))]
public class PlayerDodge : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Transform rollPivot;

    [Header("Rolamento")]
    [SerializeField, Min(0.01f)] private float rollDuration = 0.45f;
    [SerializeField, Min(0f)] private float rollStartSpeed = 14f;
    [SerializeField, Min(0f)] private float rollEndSpeed = 2f;

    [SerializeField] private AnimationCurve rollSpeedCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Recuperacao")]
    [SerializeField, Min(0.01f)] private float recoveryDuration = 0.2f;
    [SerializeField, Range(0f, 1f)]
    private float recoverySpeedMultiplier = 0.35f;

    [Header("Invulnerabilidade")]
    [SerializeField, Min(0f)] private float invulnerabilityStart = 0.06f;
    [SerializeField, Min(0f)] private float invulnerabilityEnd = 0.28f;

    private Rigidbody body;
    private Vector3 dodgeDirection;
    private float dodgeTimer;
    private Quaternion initialPivotRotation = Quaternion.identity;

    public bool IsActive { get; private set; }
    public bool IsRolling => IsActive && dodgeTimer < rollDuration;
    public bool IsRecovering => IsActive && !IsRolling;
    public float RecoverySpeedMultiplier => recoverySpeedMultiplier;

    public bool IsInvulnerable =>
        IsRolling &&
        dodgeTimer >= invulnerabilityStart &&
        dodgeTimer < invulnerabilityEnd;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        if (rollPivot != null)
            initialPivotRotation = rollPivot.localRotation;
    }

    public void BeginDodge(Vector3 direction)
    {
        dodgeDirection = Vector3.ProjectOnPlane(
            direction, Vector3.up
        ).normalized;

        dodgeTimer = 0f;
        IsActive = true;
    }

    // Chamado pelo motor; este componente nao tem FixedUpdate proprio.
    public void Tick(float deltaTime)
    {
        if (!IsActive)
            return;

        if (IsRolling)
        {
            float progress = Mathf.Clamp01(dodgeTimer / rollDuration);
            float speed = Mathf.Lerp(
                rollEndSpeed,
                rollStartSpeed,
                rollSpeedCurve.Evaluate(progress)
            );

            Vector3 velocity = body.linearVelocity;
            Vector3 horizontalVelocity = dodgeDirection * speed;

            body.linearVelocity = new Vector3(
                horizontalVelocity.x,
                velocity.y,
                horizontalVelocity.z
            );

            body.MoveRotation(
                Quaternion.LookRotation(dodgeDirection, Vector3.up)
            );
        }

        dodgeTimer += deltaTime;

        if (dodgeTimer >= rollDuration + recoveryDuration)
            Cancel();
    }

    private void LateUpdate()
    {
        if (rollPivot == null)
            return;

        if (IsRolling)
        {
            float progress = Mathf.Clamp01(dodgeTimer / rollDuration);
            rollPivot.localRotation =
                initialPivotRotation *
                Quaternion.AngleAxis(360f * progress, Vector3.right);
        }
        else
        {
            rollPivot.localRotation = initialPivotRotation;
        }
    }

    public void Cancel()
    {
        IsActive = false;
        dodgeTimer = 0f;

        if (rollPivot != null)
            rollPivot.localRotation = initialPivotRotation;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
