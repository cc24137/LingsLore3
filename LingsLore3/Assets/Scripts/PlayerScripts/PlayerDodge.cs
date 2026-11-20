using UnityEngine;

// Executa apenas o movimento fisico e os tempos do dodge.
// A animacao visual pertence ao Animator; este script nao altera pivots.
// Vida, chao e custo de stamina sao verificados no PlayerMotor.
[RequireComponent(typeof(Rigidbody))]
public class PlayerDodge : MonoBehaviour
{
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
    private CapsuleCollider playerCapsule;
    private PillarLaunchSurface climbSurface;
    private Vector3 climbStart;
    private Vector3 climbOutward;
    private float climbRadius;
    private float climbTime;

    public float TotalDuration => Mathf.Max(0.01f, rollDuration + recoveryDuration);
    public float NormalizedTime => IsActive ? Mathf.Clamp01(dodgeTimer / TotalDuration) : 0f;

    public bool IsActive { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool IsRolling => IsActive && (IsClimbing || dodgeTimer < rollDuration);
    public bool IsRecovering => IsActive && !IsRolling;
    public float RecoverySpeedMultiplier => recoverySpeedMultiplier;

    public bool IsInvulnerable =>
        IsActive && dodgeTimer < rollDuration &&
        dodgeTimer >= invulnerabilityStart &&
        dodgeTimer < invulnerabilityEnd;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerCapsule = GetComponent<CapsuleCollider>();
    }

    public void BeginDodge(Vector3 direction)
    {
        dodgeDirection = Vector3.ProjectOnPlane(
            direction, Vector3.up
        ).normalized;

        dodgeTimer = 0f;
        IsActive = true;
    }

    internal void TryBeginPillarClimb(PillarLaunchSurface surface)
    {
        if (!isActiveAndEnabled || !IsRolling || IsClimbing || playerCapsule == null)
            return;

        Vector3 extents = playerCapsule.bounds.extents;
        float playerRadius = Mathf.Max(extents.x, extents.z);
        if (!surface.TryGetEntry(body.position, dodgeDirection, playerRadius,
            out climbOutward, out climbRadius))
            return;

        climbSurface = surface;
        climbStart = body.position;
        climbTime = 0f;
        IsClimbing = true;
    }

    // Retorna true quando o motor pode devolver o controle no ar.
    public bool Tick(float deltaTime)
    {
        if (!IsActive)
            return false;

        if (IsClimbing)
        {
            dodgeTimer += deltaTime;
            return TickPillarClimb(deltaTime);
        }

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

        return false;
    }

    private bool TickPillarClimb(float deltaTime)
    {
        if (climbSurface == null || !climbSurface.isActiveAndEnabled)
        {
            CancelClimb();
            return false;
        }

        if (body.position.y >= climbSurface.ReleaseY - 0.015f)
        {
            body.linearVelocity = climbSurface.GetLaunchVelocity(climbOutward, body.position.y, deltaTime);
            body.MoveRotation(Quaternion.LookRotation(climbOutward, Vector3.up));
            Cancel();
            return true;
        }

        climbTime += deltaTime;
        Vector3 target = climbSurface.GetClimbPosition(
            climbStart, climbOutward, climbRadius, climbTime);
        Vector3 step = target - body.position;
        if (step.sqrMagnitude < 0.000001f ||
            body.SweepTest(step.normalized, out _, step.magnitude, QueryTriggerInteraction.Ignore))
        {
            CancelClimb();
            return false;
        }

        body.linearVelocity = step / deltaTime;
        if (body.useGravity)
            body.linearVelocity -= Physics.gravity * deltaTime;
        return false;
    }

    private void CancelClimb()
    {
        body.linearVelocity = Vector3.zero;
        Cancel();
    }

    public void Cancel()
    {
        IsActive = false;
        IsClimbing = false;
        climbSurface = null;
        dodgeTimer = 0f;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
