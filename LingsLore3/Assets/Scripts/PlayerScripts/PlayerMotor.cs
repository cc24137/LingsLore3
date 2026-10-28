using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody), typeof(PlayerJump), typeof(PlayerDodge))]
[RequireComponent(typeof(PlayerResources), typeof(PlayerMelee))]
[RequireComponent(typeof(PlayerHashi))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference dodgeSprintAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference throwHashiAction;

    [Header("Referencias")]
    [SerializeField] private Transform movementCamera;
    [SerializeField] private Transform groundCheck;

    [Header("Deteccao de chao")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField, Min(0.01f)] private float groundRadius = 0.15f;

    [Header("Movimento")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;
    [SerializeField, Min(0f)] private float sprintSpeed = 10f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;
    [SerializeField, Min(0.01f)] private float acceleration = 25f;
    [SerializeField, Min(0.01f)] private float deceleration = 40f;
    [SerializeField, Min(0.01f)] private float steeringSpeed = 720f;

    [Header("Controle no ar")]
    [SerializeField] private bool useAirControlRestriction = true;
    [SerializeField, Min(0f)] private float airControlAcceleration = 2f;
    [SerializeField, Min(0f)] private float standingJumpAirSpeed = 1.5f;

    [Header("Custos de stamina")]
    [SerializeField, Min(0f)] private float dodgeStaminaCost = 20f;
    [SerializeField, Min(0f)] private float sprintStaminaPerSecond = 15f;
    [SerializeField, Min(0f)] private float minStaminaToResumeSprint = 20f;

    [Header("Carregamento do garfo")]
    [SerializeField, Min(0.05f)] private float chargeDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float followupChargeDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float chargingMovementMultiplier = 0.2f;

    private PlayerHashi hashi;
    private bool throwHashiRequested;
    private PlayerMelee melee;
    private enum ChargeIntent { None, Up, Down }
    private ChargeIntent chargeIntent;
    private bool attackPressed;
    private bool attackReleased;
    private bool attackHeld;
    private bool queuedLightAttack;
    private PlayerMelee.AttackType queuedChargedAttack;
    private bool airAttackUsed;
    private float chargeTimer;

    private Rigidbody body;
    private PlayerResources resources;
    private PlayerJump jump;
    private PlayerDodge dodge;

    private Vector2 moveInput;
    private bool jumpRequested;
    private bool jumpHeld;
    private bool dodgeRequested;
    private bool sprintHeld;
    private bool sprintExhausted;
    private bool sprintNeedsRelease;

    private bool wasAirborne;
    private float airborneReferenceSpeed;

    public bool IsDead => resources != null && resources.IsDead;
    public bool IsGrounded { get; private set; }
    public float HorizontalSpeed => body != null
        ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0f;
    public bool IsMoving => HorizontalSpeed > 0.1f;
    public bool IsRising => !IsGrounded && body != null && body.linearVelocity.y > 0.1f;
    public bool IsFalling => !IsGrounded && !IsRising;
    public bool IsSprinting { get; private set; }
    public bool IsAttacking => melee != null && melee.IsActive;
    public bool IsCharging { get; private set; }
    public bool IsChargingDown => IsCharging && chargeIntent == ChargeIntent.Down;
    public float ChargeProgress => IsCharging
        ? Mathf.Clamp01(chargeTimer / Mathf.Max(0.05f,
            IsChargingDown ? followupChargeDuration : chargeDuration)) : 0f;
    public bool BlocksStaminaRegeneration =>
        IsDodging || IsSprinting || IsAttacking || IsCharging ||
        queuedLightAttack || queuedChargedAttack != PlayerMelee.AttackType.None;

    public bool CanThrowHashi =>
        !IsDead && !IsDodging && !IsAttacking && !IsCharging &&
        queuedChargedAttack == PlayerMelee.AttackType.None && !queuedLightAttack &&
        hashi != null && hashi.isActiveAndEnabled && hashi.CanFire;

    public bool CanStartAttack =>
        !IsDead && !IsDodging && !IsAttacking &&
        melee != null && melee.enabled;
    public bool IsDodging => dodge != null && dodge.IsActive;
    public bool IsInvulnerable =>
        !IsDead && IsDodging && dodge.IsInvulnerable;

    // As permissoes para as acoes ficam centralizadas aqui.
    public bool CanJump =>
        !IsDead && IsGrounded && !IsDodging && !IsAttacking && !IsCharging &&
        queuedChargedAttack == PlayerMelee.AttackType.None && jump != null && jump.enabled;

    public bool CanDodge =>
        !IsDead && IsGrounded && !IsDodging && !IsAttacking &&
        dodge != null && dodge.enabled &&
        resources != null && resources.CurrentStamina >= dodgeStaminaCost;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        resources = GetComponent<PlayerResources>();
        jump = GetComponent<PlayerJump>();
        dodge = GetComponent<PlayerDodge>();
        melee = GetComponent<PlayerMelee>();
        hashi = GetComponent<PlayerHashi>();
    }

    private void OnEnable()
    {
        dodgeSprintAction.action.performed += OnDodgeSprintPerformed;
        dodgeSprintAction.action.canceled += OnDodgeSprintCanceled;

        moveAction.action.Enable();
        jumpAction.action.Enable();
        dodgeSprintAction.action.Enable();
        attackAction.action.Enable();
        if (throwHashiAction != null)
            throwHashiAction.action.Enable();
    }

    private void OnDisable()
    {
        dodgeSprintAction.action.performed -= OnDodgeSprintPerformed;
        dodgeSprintAction.action.canceled -= OnDodgeSprintCanceled;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        dodgeSprintAction.action.Disable();
        attackAction.action.Disable();
        if (throwHashiAction != null)
            throwHashiAction.action.Disable();

        StopActions();
        IsGrounded = false;
    }

    private void OnDodgeSprintPerformed(InputAction.CallbackContext context)
    {
        // Os callbacks registram pedidos; o FixedUpdate decide se pode executar.
        if (context.interaction is TapInteraction)
            dodgeRequested = true;
        else if (context.interaction is HoldInteraction)
            sprintHeld = true;
    }

    private void OnDodgeSprintCanceled(InputAction.CallbackContext context)
    {
        sprintHeld = false;
        sprintNeedsRelease = false;
    }

    private void Update()
    {
        moveInput = Vector2.ClampMagnitude(
            moveAction.action.ReadValue<Vector2>(), 1f
        );

        if (jumpAction.action.WasPressedThisFrame())
            jumpRequested = true;

        jumpHeld = jumpAction.action.IsPressed();

        if (attackAction.action.WasPressedThisFrame())
            attackPressed = true;
        if (attackAction.action.WasReleasedThisFrame())
            attackReleased = true;

        attackHeld = attackAction.action.IsPressed();

        if (throwHashiAction != null && throwHashiAction.action.WasPressedThisFrame())
            throwHashiRequested = true;
    }

    private void FixedUpdate()
    {
        RefreshGroundedState();

        if (IsDead)
        {
            StopActions();
            return;
        }

        UpdateAirControlReference();

        if (IsGrounded)
            airAttackUsed = false;

        float resumeThreshold = Mathf.Min(
            minStaminaToResumeSprint, resources.MaxStamina
        );

        if (sprintExhausted && resources.CurrentStamina >= resumeThreshold)
            sprintExhausted = false;

        Vector3 direction = GetMovementDirection();
        IsSprinting = false;

        // Se os dois pedidos chegarem juntos, o dodge tem prioridade.
        if (dodgeRequested && CanDodge &&
            resources.TrySpendStamina(dodgeStaminaCost))
        {
            Vector3 rollDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.ProjectOnPlane(
                    body.rotation * Vector3.forward, Vector3.up
                ).normalized;

            CancelCombat();
            dodge.BeginDodge(rollDirection);
        }

        dodgeRequested = false;

        if (IsDodging)
        {
            throwHashiRequested = false;
            ClearCombatInputRequests();
            jumpRequested = false;
            jump.Cancel();

            // O dodge executa o impulso; o motor controla o movimento na recuperacao.
            if (dodge.IsRecovering)
            {
                MoveNormally(
                    direction,
                    moveSpeed * dodge.RecoverySpeedMultiplier,
                    true
                );
            }

            dodge.Tick(Time.fixedDeltaTime);
            return;
        }

        HandleCombatRequests(direction);

        // Garfo e dodge tem prioridade sobre pedidos simultaneos de disparo.
        if (throwHashiRequested && CanThrowHashi)
            hashi.Fire(transform);
        throwHashiRequested = false;

        if (IsAttacking)
        {
            jumpRequested = false;
            ApplyAttackMovement(direction);
            melee.Tick(Time.fixedDeltaTime);

            if (jump.enabled)
                jump.Tick(jumpHeld);

            return;
        }

        if (IsCharging)
        {
            jumpRequested = false;
            jump.Cancel();
            MoveNormally(direction, moveSpeed * chargingMovementMultiplier, true);
            return;
        }

        UpdateSprint(direction);

        if (IsGrounded || !useAirControlRestriction)
        {
            MoveNormally(
                direction,
                IsSprinting ? sprintSpeed : moveSpeed
            );
        }
        else
        {
            MoveInAir(direction);
        }

        if (jumpRequested && CanJump)
        {
            melee.Cancel();
            jump.ExecuteJump();
            IsGrounded = false;
            IsSprinting = false;
            CaptureAirControlReference();
        }

        // Pedidos recusados nao ficam guardados para executar ao aterrissar.
        jumpRequested = false;

        if (jump.enabled)
            jump.Tick(jumpHeld);
    }

    private void HandleCombatRequests(Vector3 direction)
    {
        // Soltar cancela apenas o carregamento ainda incompleto.
        // Um golpe ja confirmado continua aguardando o fim da animacao atual.
        if (attackReleased || !IsGrounded)
            CancelCharge();

        ConsumeQueuedAttacks(direction);

        if (attackPressed)
        {
            if (IsAttacking)
            {
                if (melee.CurrentAttack == PlayerMelee.AttackType.ChargedUp)
                {
                    // Exige uma nova pressao: manter o primeiro hold nao encadeia.
                    ArmCharge(ChargeIntent.Down);
                }
                else if (melee.IsLightAttack && melee.CanQueueNext)
                {
                    if (melee.CurrentAttack != PlayerMelee.AttackType.Light3)
                        queuedLightAttack = true;

                    ArmCharge(ChargeIntent.Up);
                }
            }
            else if (CanStartAttack)
            {
                if (IsGrounded)
                {
                    // O golpe leve sai imediatamente, antes de sabermos se sera um hold.
                    if (TryStartAttack(melee.NextLightAttack, direction))
                        ArmCharge(ChargeIntent.Up);
                }
                else if (!airAttackUsed)
                {
                    TryStartAttack(PlayerMelee.AttackType.AirSpin, direction);
                }
            }
        }

        UpdateChargeIntent();
        ClearCombatInputRequests();
        ConsumeQueuedAttacks(direction);
    }

    private void ArmCharge(ChargeIntent intent)
    {
        PlayerMelee.AttackType type = intent == ChargeIntent.Down
            ? PlayerMelee.AttackType.ChargedDown : PlayerMelee.AttackType.ChargedUp;

        if (!IsGrounded || resources.CurrentStamina < melee.GetStaminaCost(type))
            return;

        chargeIntent = intent;
        chargeTimer = 0f;
        IsCharging = true;
    }

    private void UpdateChargeIntent()
    {
        if (!IsCharging)
            return;

        if (!attackHeld || !IsGrounded || !melee.enabled)
        {
            CancelCharge();
            return;
        }

        bool validSequence = chargeIntent == ChargeIntent.Up
            ? !IsAttacking || melee.IsLightAttack
            : melee.CurrentAttack == PlayerMelee.AttackType.ChargedUp ||
                melee.CanChainChargedDown;

        if (!validSequence)
        {
            CancelCharge();
            return;
        }

        chargeTimer += Time.fixedDeltaTime;
        float requiredTime = chargeIntent == ChargeIntent.Down
            ? followupChargeDuration : chargeDuration;

        if (chargeTimer >= requiredTime)
        {
            queuedChargedAttack = chargeIntent == ChargeIntent.Down
                ? PlayerMelee.AttackType.ChargedDown : PlayerMelee.AttackType.ChargedUp;

            queuedLightAttack = false;
            CancelCharge();
        }
    }

    private void ConsumeQueuedAttacks(Vector3 direction)
    {
        if (!CanStartAttack)
            return;

        if (queuedChargedAttack != PlayerMelee.AttackType.None)
        {
            PlayerMelee.AttackType type = queuedChargedAttack;
            queuedChargedAttack = PlayerMelee.AttackType.None;
            queuedLightAttack = false;
            CancelCharge();

            if (IsGrounded && (type == PlayerMelee.AttackType.ChargedUp ||
                melee.CanChainChargedDown))
                TryStartAttack(type, direction);
        }
        else if (queuedLightAttack)
        {
            queuedLightAttack = false;

            if (IsGrounded)
                TryStartAttack(melee.NextLightAttack, direction);
        }
    }

    private bool TryStartAttack(PlayerMelee.AttackType type, Vector3 direction)
    {
        if (!CanStartAttack ||
            !resources.TrySpendStamina(melee.GetStaminaCost(type)))
            return false;

        Vector3 attackDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized : body.rotation * Vector3.forward;

        queuedLightAttack = false;
        queuedChargedAttack = PlayerMelee.AttackType.None;
        melee.BeginAttack(type, attackDirection);
        IsSprinting = false;

        if (type == PlayerMelee.AttackType.AirSpin)
            airAttackUsed = true;

        return true;
    }

    private void ApplyAttackMovement(Vector3 direction)
    {
        if (IsGrounded)
        {
            Vector3 inputVelocity = direction * moveSpeed * melee.MovementMultiplier;
            SetHorizontalVelocity(
                inputVelocity + melee.GetAdvanceVelocity(Time.fixedDeltaTime)
            );
        }
        else if (useAirControlRestriction)
        {
            MoveInAir(direction);
        }
        else
        {
            MoveNormally(direction, moveSpeed, false, false);
        }

        body.MoveRotation(melee.AttackRotation);
    }

    private void ClearCombatInputRequests()
    {
        attackPressed = false;
        attackReleased = false;
    }

    private void CancelCharge()
    {
        IsCharging = false;
        chargeIntent = ChargeIntent.None;
        chargeTimer = 0f;
    }

    private void CancelCombat()
    {
        CancelCharge();
        queuedLightAttack = false;
        queuedChargedAttack = PlayerMelee.AttackType.None;
        ClearCombatInputRequests();

        if (melee != null)
            melee.Cancel();
    }

    private void RefreshGroundedState()
    {
        IsGrounded =
            groundCheck != null &&
            body.linearVelocity.y <= 0.1f &&
            Physics.CheckSphere(
                groundCheck.position,
                groundRadius,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
    }

    private void UpdateAirControlReference()
    {
        if (IsGrounded)
            wasAirborne = false;
        else if (!wasAirborne)
            CaptureAirControlReference();
    }

    private void CaptureAirControlReference()
    {
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
            body.linearVelocity, Vector3.up
        );

        airborneReferenceSpeed = Mathf.Max(
            horizontalVelocity.magnitude,
            standingJumpAirSpeed
        );
        wasAirborne = true;
    }

    private void UpdateSprint(Vector3 direction)
    {
        bool canSprint =
            !IsDead &&
            IsGrounded &&
            !IsDodging &&
            !IsAttacking &&
            !IsCharging &&
            !sprintExhausted &&
            !sprintNeedsRelease;

        if (!canSprint || !sprintHeld || direction.sqrMagnitude <= 0.001f)
            return;

        if (sprintStaminaPerSecond <= 0f)
        {
            IsSprinting = true;
            return;
        }

        resources.DrainStamina(
            sprintStaminaPerSecond * Time.fixedDeltaTime
        );

        IsSprinting = resources.CurrentStamina > 0f;

        if (!IsSprinting)
        {
            sprintExhausted = true;
            sprintNeedsRelease = true;
        }
    }

    private void StopActions()
    {
        throwHashiRequested = false;
        moveInput = Vector2.zero;
        jumpRequested = false;
        jumpHeld = false;
        dodgeRequested = false;
        sprintHeld = false;
        sprintExhausted = false;
        sprintNeedsRelease = false;
        wasAirborne = false;
        airborneReferenceSpeed = 0f;
        IsSprinting = false;
        airAttackUsed = false;
        attackHeld = false;
        CancelCombat();

        if (jump != null)
            jump.Cancel();

        if (dodge != null)
            dodge.Cancel();

        if (body != null)
            SetHorizontalVelocity(Vector3.zero);
    }

    private Vector3 GetMovementDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(
            movementCamera.forward, Vector3.up
        ).normalized;

        Vector3 right = Vector3.ProjectOnPlane(
            movementCamera.right, Vector3.up
        ).normalized;

        return Vector3.ClampMagnitude(
            forward * moveInput.y + right * moveInput.x, 1f
        );
    }

    private void MoveNormally(
        Vector3 direction,
        float speed,
        bool limitSpeedImmediately = false,
        bool rotateBody = true)
    {
        // Separa a rapidez da direcao para manter a velocidade nas curvas.
        Vector3 currentVelocity = Vector3.ProjectOnPlane(
            body.linearVelocity, Vector3.up
        );

        float currentSpeed = currentVelocity.magnitude;
        bool hasInput = direction.sqrMagnitude > 0.001f;
        float targetSpeed = hasInput ? direction.magnitude * speed : 0f;

        if (limitSpeedImmediately)
        {
            currentSpeed = Mathf.Min(currentSpeed, speed);
        }

        float rate = targetSpeed < currentSpeed ? deceleration : acceleration;
        float nextSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            rate * Time.fixedDeltaTime
        );

        Vector3 movementDirection = currentVelocity.normalized;

        if (hasInput)
        {
            if (currentSpeed < 0.001f)
            {
                movementDirection = direction.normalized;
            }
            else
            {
                float currentYaw = Mathf.Atan2(
                    currentVelocity.x, currentVelocity.z
                ) * Mathf.Rad2Deg;

                float targetYaw = Mathf.Atan2(
                    direction.x, direction.z
                ) * Mathf.Rad2Deg;

                float nextYaw = Mathf.MoveTowardsAngle(
                    currentYaw,
                    targetYaw,
                    steeringSpeed * Time.fixedDeltaTime
                );

                movementDirection = Quaternion.Euler(0f, nextYaw, 0f)
                    * Vector3.forward;
            }
        }

        SetHorizontalVelocity(movementDirection * nextSpeed);

        if (!hasInput || !rotateBody)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(movementDirection, Vector3.up);

        body.MoveRotation(Quaternion.RotateTowards(
            body.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        ));
    }

    private void MoveInAir(Vector3 direction)
    {
        // Sem input, conserva o impulso: soltar WASD nao freia no ar.
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Vector3 currentVelocity = Vector3.ProjectOnPlane(
            body.linearVelocity, Vector3.up
        );

        // Usa a velocidade de entrada no ar, incluindo um salto correndo.
        Vector3 targetVelocity = direction * airborneReferenceSpeed;

        // O limite vale para a mudanca do vetor inteiro, inclusive inversoes.
        Vector3 nextVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            airControlAcceleration * Time.fixedDeltaTime
        );

        SetHorizontalVelocity(nextVelocity);

        if (nextVelocity.sqrMagnitude <= 0.001f)
            return;

        // O corpo acompanha a trajetoria real, nao gira direto para o input.
        Quaternion targetRotation = Quaternion.LookRotation(
            nextVelocity.normalized, Vector3.up
        );

        body.MoveRotation(Quaternion.RotateTowards(
            body.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        ));
    }

    private void SetHorizontalVelocity(Vector3 velocity)
    {
        // Preserva salto e gravidade.
        body.linearVelocity = new Vector3(
            velocity.x,
            body.linearVelocity.y,
            velocity.z
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
