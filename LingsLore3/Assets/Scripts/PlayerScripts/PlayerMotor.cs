using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody), typeof(PlayerJump), typeof(PlayerDodge))]
[RequireComponent(typeof(PlayerResources), typeof(PlayerMelee))]
[RequireComponent(typeof(PlayerHashi), typeof(PlayerKnockback))]
public class PlayerMotor : MonoBehaviour
{
    public enum AttackLessonMode { All, LightComboOnly, ChargedComboOnly, AirSpinOnly, HashiOnly }

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
    [SerializeField, Min(0f)] private float minStaminaForContinuousPogo = 30f;

    [Header("Carregamento do garfo")]
    [SerializeField, Min(0.05f)] private float chargeDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float followupChargeDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float chargingMovementMultiplier = 0.2f;

    private const float HealingMovementMultiplier = 0.3f;

    private InputAction healAction;
    private InputAction aimHashiAction;
    private InputAction chargedAttackAction;
    private bool healRequested;
    private PlayerHashi hashi;
    private bool throwHashiRequested;
    private PlayerMelee melee;
    private enum ChargeIntent { None, Up, Down }
    private ChargeIntent chargeIntent;
    private InputAction chargeInput;
    private bool attackPressed;
    private bool mouseAttackPressed;
    private bool attackReleased;
    private bool chargedAttackPressed;
    private bool chargedAttackReleased;
    private bool queuedLightAttack;
    private PlayerMelee.AttackType queuedChargedAttack;
    private float pendingHitBounceHeight;
    private int? lastBouncedAttackSerial;
    private float chargeTimer;
    private AttackLessonMode attackLessonMode;
    private bool hashiAtivoAntesDaRestricao;

    private Rigidbody body;
    private PlayerResources resources;
    private PlayerJump jump;
    private PlayerDodge dodge;
    private PlayerKnockback knockback;

    private Vector2 moveInput;
    private bool jumpRequested;
    private bool jumpHeld;
    private bool dodgeRequested;
    private bool sprintHeld;
    private bool sprintExhausted;
    private bool sprintNeedsRelease;
    private bool isTutorial;

    private bool wasAirborne;
    private float airborneReferenceSpeed;
    private int airSpinCount;

    public bool IsDead => resources != null && resources.IsDead;
    public bool IsGrounded { get; private set; }
    public float HorizontalSpeed => body != null
        ? Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude : 0f;
    public bool IsMoving => HorizontalSpeed > 0.1f;
    public bool IsRising => !IsGrounded && body != null && body.linearVelocity.y > 0.1f;
    public bool IsFalling => !IsGrounded && !IsRising;
    public bool IsSprinting { get; private set; }
    public bool IsHealing => resources != null && resources.IsHealing;
    public bool IsAttacking => melee != null && melee.IsActive;
    public bool IsCharging { get; private set; }
    internal bool IsAimingHashi => aimHashiAction?.IsPressed() == true;
    public bool IsChargingDown => IsCharging && chargeIntent == ChargeIntent.Down;
    public float ChargeProgress => IsCharging
        ? Mathf.Clamp01(chargeTimer / Mathf.Max(0.05f,
            IsChargingDown ? followupChargeDuration : chargeDuration)) : 0f;
    public bool BlocksStaminaRegeneration =>
        IsDodging || IsSprinting || IsAttacking || IsCharging ||
        queuedLightAttack || queuedChargedAttack != PlayerMelee.AttackType.None;

    public bool CanThrowHashi =>
        (attackLessonMode == AttackLessonMode.All ||
            attackLessonMode == AttackLessonMode.HashiOnly) &&
        !IsDead && !IsKnockedBack && !IsDodging && !IsAttacking && !IsCharging && !IsHealing &&
        queuedChargedAttack == PlayerMelee.AttackType.None && !queuedLightAttack &&
        hashi != null && hashi.isActiveAndEnabled && hashi.CanFire;

    public bool CanStartAttack =>
        !IsDead && !IsKnockedBack && !IsDodging && !IsAttacking && !IsHealing &&
        melee != null && melee.enabled;

    public void SetBasicAttackOnly(bool active)
    {
        SetAttackLessonMode(active
            ? AttackLessonMode.LightComboOnly
            : AttackLessonMode.All);
    }

    public void SetAttackLessonMode(AttackLessonMode mode)
    {
        if (attackLessonMode == mode)
            return;

        bool wasHashiRestricted = attackLessonMode != AttackLessonMode.All &&
            attackLessonMode != AttackLessonMode.HashiOnly;
        bool willHashiBeRestricted = mode != AttackLessonMode.All &&
            mode != AttackLessonMode.HashiOnly;
        attackLessonMode = mode;
        CancelCharge();
        queuedChargedAttack = PlayerMelee.AttackType.None;
        queuedLightAttack = false;

        if (!wasHashiRestricted && willHashiBeRestricted)
        {
            hashiAtivoAntesDaRestricao = hashi != null && hashi.enabled;
            if (hashi != null)
                hashi.enabled = false;
        }
        else if (wasHashiRestricted && !willHashiBeRestricted && hashi != null)
        {
            hashi.enabled = hashiAtivoAntesDaRestricao;
        }

        if (melee != null && melee.IsActive)
        {
            bool activeAttackAllowed = mode == AttackLessonMode.All ||
                (mode == AttackLessonMode.LightComboOnly && melee.IsLightAttack) ||
                (mode == AttackLessonMode.ChargedComboOnly &&
                    (melee.CurrentAttack == PlayerMelee.AttackType.ChargedUp ||
                     melee.CurrentAttack == PlayerMelee.AttackType.ChargedDown)) ||
                (mode == AttackLessonMode.AirSpinOnly &&
                    melee.CurrentAttack == PlayerMelee.AttackType.AirSpin);
            if (!activeAttackAllowed)
                melee.Cancel();
        }
    }
    public bool IsDodging => dodge != null && dodge.IsActive;
    public bool IsKnockedBack => knockback != null && knockback.IsActive;
    public bool IsInvulnerable =>
        !IsDead && IsDodging && dodge.IsInvulnerable;

    // As permissoes para as acoes ficam centralizadas aqui.
    public bool CanJump =>
        !IsDead && !IsKnockedBack && IsGrounded && !IsDodging &&
        !IsAttacking && !IsCharging && !IsHealing &&
        queuedChargedAttack == PlayerMelee.AttackType.None && jump != null && jump.enabled;

    public bool CanDodge =>
        !IsDead && !IsKnockedBack && IsGrounded && !IsDodging && !IsAttacking && !IsHealing &&
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
        knockback = GetComponent<PlayerKnockback>();
        isTutorial = gameObject.scene.name == "Tutorial";
    }

    private void OnEnable()
    {
        melee.HitLanded += OnMeleeHit;
        hashi.TargetHit += OnHashiHit;
        dodgeSprintAction.action.performed += OnDodgeSprintPerformed;
        dodgeSprintAction.action.canceled += OnDodgeSprintCanceled;

        moveAction.action.Enable();
        jumpAction.action.Enable();
        dodgeSprintAction.action.Enable();
        attackAction.action.Enable();
        if (throwHashiAction != null)
            throwHashiAction.action.Enable();
        InputActionMap gameplay = moveAction.action.actionMap;
        healAction = gameplay.FindAction("Heal");
        aimHashiAction = gameplay.FindAction("AimHashi");
        chargedAttackAction = gameplay.FindAction("ChargedAttack");
        healAction?.Enable();
        aimHashiAction?.Enable();
        chargedAttackAction?.Enable();
    }

    private void OnDisable()
    {
        melee.HitLanded -= OnMeleeHit;
        hashi.TargetHit -= OnHashiHit;
        dodgeSprintAction.action.performed -= OnDodgeSprintPerformed;
        dodgeSprintAction.action.canceled -= OnDodgeSprintCanceled;

        moveAction.action.Disable();
        jumpAction.action.Disable();
        dodgeSprintAction.action.Disable();
        attackAction.action.Disable();
        if (throwHashiAction != null)
            throwHashiAction.action.Disable();
        healAction?.Disable();
        aimHashiAction?.Disable();
        chargedAttackAction?.Disable();

        StopActions();
        IsGrounded = false;
    }

    private void OnDodgeSprintPerformed(InputAction.CallbackContext context)
    {
        if (MenuPausa.BloqueiaEntradaDeJogo)
            return;

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
        moveInput = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        jumpHeld = jumpAction.action.IsPressed();

        attackReleased |= attackAction.action.WasReleasedThisFrame();
        chargedAttackReleased |= chargedAttackAction?.WasReleasedThisFrame() == true;

        if (MenuPausa.BloqueiaEntradaDeJogo)
        {
            // Cancela o Tap/Hold para que soltar o botao da pausa nao inicie uma esquiva.
            dodgeSprintAction.action.Disable();
            jumpRequested = false;
            dodgeRequested = false;
            healRequested = false;
            throwHashiRequested = false;
            attackPressed = false;
            mouseAttackPressed = false;
            chargedAttackPressed = false;
            return;
        }

        dodgeSprintAction.action.Enable();
        Keyboard teclado = Keyboard.current;
        if (MenuPausa.Ativo == null && teclado != null && teclado.escapeKey.wasPressedThisFrame)
        {
            bool liberarCursor = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = liberarCursor
                ? CursorLockMode.None
                : CursorLockMode.Locked;
            Cursor.visible = liberarCursor;
        }

        if (jumpAction.action.WasPressedThisFrame())
            jumpRequested = true;

        if (!IsHealing && healAction != null && healAction.WasPressedThisFrame())
            healRequested = true;

        bool sobreBotao = MenuPausa.Ativo != null && MenuPausa.Ativo.PonteiroSobreBotao();
        bool ataqueComControle = attackAction.action.activeControl?.device is Gamepad;
        attackReleased |= sobreBotao;
        if ((!sobreBotao || ataqueComControle) && attackAction.action.WasPressedThisFrame())
        {
            attackPressed = true;
            mouseAttackPressed = !ataqueComControle;
        }

        chargedAttackPressed |= chargedAttackAction?.WasPressedThisFrame() == true;

        InputAction arremesso = throwHashiAction?.action;
        if (arremesso != null && arremesso.WasPressedThisFrame() &&
            (!sobreBotao || arremesso.activeControl?.device is Gamepad))
            throwHashiRequested = true;
    }

    private void OnGUI()
    {
        if (isTutorial || MenuPausa.Ativo != null || MenuPausa.BloqueiaEntradaDeJogo)
            return;

        const float margin = 16f;
        const float largura = 148f;
        GUI.Label(new Rect(Screen.width - margin - largura, 12f, largura, 32f),
            "Esc  Liberar mouse", GUI.skin.box);
    }

    private void FixedUpdate()
    {
        RefreshGroundedState();

        if (IsDead)
        {
            StopActions();
            return;
        }

        if (IsGrounded){
            airSpinCount = 0;
            wasAirborne = false;
        }

        if (IsKnockedBack)
        {
            IsSprinting = false;
            dodgeRequested = false;
            jumpRequested = false;
            healRequested = false;
            throwHashiRequested = false;
            CancelCombat();
            dodge.Cancel();
            jump.Cancel();

            knockback.Tick(Time.fixedDeltaTime);
            if (body.linearVelocity.y > 0.1f)
                IsGrounded = false;
            UpdateAirControlReference();
            return;
        }

        ApplyHitBounce();
        UpdateAirControlReference();

        float resumeThreshold = Mathf.Min(
            minStaminaToResumeSprint, resources.MaxStamina
        );

        if (sprintExhausted && resources.CurrentStamina >= resumeThreshold)
            sprintExhausted = false;

        Vector3 direction = GetMovementDirection();
        IsSprinting = false;

        // Se os dois pedidos chegarem juntos, o dodge tem prioridade.
        if (dodgeRequested && CanDodge &&
            resources.TrySpendDodgeStamina(dodgeStaminaCost))
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

        if (IsDodging || dodge.IsLaunchingToPillarTop)
        {
            healRequested = false;
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

            bool terminouLancamento = dodge.Tick(Time.fixedDeltaTime);
            if (dodge.IsClimbing || dodge.IsLaunchingToPillarTop || terminouLancamento)
                IsGrounded = false;
            if (terminouLancamento)
                CaptureAirControlReference();
            return;
        }

        if (healRequested && !MenuPausa.BloqueiaEntradaDeJogo && !IsAttacking && !IsCharging &&
            !queuedLightAttack && queuedChargedAttack == PlayerMelee.AttackType.None)
            resources.TryStartHealing();
        healRequested = false;

        if (IsHealing)
        {
            throwHashiRequested = false;
            ClearCombatInputRequests();
            jumpRequested = false;
            jump.Cancel();
            MoveNormally(direction, moveSpeed * HealingMovementMultiplier, true);
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
        bool chargeReleased = chargeInput == attackAction.action ? attackReleased : chargedAttackReleased;
        if (chargeReleased || !IsGrounded)
            CancelCharge();

        ConsumeQueuedAttacks(direction);

        if (chargedAttackPressed &&
            (attackLessonMode == AttackLessonMode.All || attackLessonMode == AttackLessonMode.ChargedComboOnly))
        {
            if (melee.CurrentAttack == PlayerMelee.AttackType.ChargedUp || melee.CanChainChargedDown)
                ArmCharge(ChargeIntent.Down, chargedAttackAction);
            else if (CanStartAttack || (melee.IsLightAttack && melee.CanQueueNext))
                ArmCharge(ChargeIntent.Up, chargedAttackAction);
        }
        else if (attackPressed)
        {
            if (IsAttacking)
            {
                if (mouseAttackPressed && attackLessonMode != AttackLessonMode.LightComboOnly &&
                    melee.CurrentAttack == PlayerMelee.AttackType.ChargedUp)
                {
                    // Exige uma nova pressao: manter o primeiro hold nao encadeia.
                    ArmCharge(ChargeIntent.Down, attackAction.action);
                }
                else if (attackLessonMode != AttackLessonMode.ChargedComboOnly &&
                    attackLessonMode != AttackLessonMode.AirSpinOnly &&
                    melee.IsLightAttack && melee.CanQueueNext)
                {
                    if (melee.CurrentAttack != PlayerMelee.AttackType.Light3)
                        queuedLightAttack = true;

                    if (mouseAttackPressed && attackLessonMode == AttackLessonMode.All)
                        ArmCharge(ChargeIntent.Up, attackAction.action);
                }
            }
            else if (CanStartAttack)
            {
                if (IsGrounded)
                {
                    if (attackLessonMode == AttackLessonMode.ChargedComboOnly)
                    {
                        if (mouseAttackPressed)
                            ArmCharge(ChargeIntent.Up, attackAction.action);
                    }
                    else if (attackLessonMode != AttackLessonMode.AirSpinOnly &&
                        TryStartAttack(melee.NextLightAttack, direction) &&
                        mouseAttackPressed &&
                        attackLessonMode == AttackLessonMode.All)
                    {
                        // No modo normal, o golpe leve sai antes de sabermos se sera um hold.
                        ArmCharge(ChargeIntent.Up, attackAction.action);
                    }
                }
                else if (attackLessonMode == AttackLessonMode.All ||
                    attackLessonMode == AttackLessonMode.AirSpinOnly)
                {
                    TryStartAttack(PlayerMelee.AttackType.AirSpin, direction);
                }
            }
        }

        if (attackLessonMode == AttackLessonMode.LightComboOnly)
        {
            CancelCharge();
            queuedChargedAttack = PlayerMelee.AttackType.None;
        }
        else
        {
            if (attackLessonMode == AttackLessonMode.ChargedComboOnly)
                queuedLightAttack = false;
            UpdateChargeIntent();
        }
        ClearCombatInputRequests();
        ConsumeQueuedAttacks(direction);
    }

    private void ArmCharge(ChargeIntent intent, InputAction input)
    {
        PlayerMelee.AttackType type = intent == ChargeIntent.Down
            ? PlayerMelee.AttackType.ChargedDown : PlayerMelee.AttackType.ChargedUp;

        if (!IsGrounded || resources.CurrentStamina < melee.GetStaminaCost(type))
            return;

        chargeIntent = intent;
        chargeInput = input;
        chargeTimer = 0f;
        IsCharging = true;
    }

    private void UpdateChargeIntent()
    {
        if (!IsCharging)
            return;

        if (!chargeInput.IsPressed() || !IsGrounded || !melee.enabled)
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
        bool lightAttack = type == PlayerMelee.AttackType.Light1 ||
            type == PlayerMelee.AttackType.Light2 ||
            type == PlayerMelee.AttackType.Light3;
        bool chargedAttack = type == PlayerMelee.AttackType.ChargedUp ||
            type == PlayerMelee.AttackType.ChargedDown;
        bool aerialAttack = type == PlayerMelee.AttackType.AirSpin;
        if ((attackLessonMode == AttackLessonMode.LightComboOnly && !lightAttack) ||
            (attackLessonMode == AttackLessonMode.ChargedComboOnly && !chargedAttack) ||
            (attackLessonMode == AttackLessonMode.AirSpinOnly && !aerialAttack) ||
            attackLessonMode == AttackLessonMode.HashiOnly)
            return false;

        float staminaCost = melee.GetStaminaCost(type);
        if (type == PlayerMelee.AttackType.AirSpin && airSpinCount == 0)
            staminaCost = 0f;
            
        if (!CanStartAttack ||
            !resources.TrySpendStamina(staminaCost))
            return false;

        Vector3 attackDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized : body.rotation * Vector3.forward;

        queuedLightAttack = false;
        queuedChargedAttack = PlayerMelee.AttackType.None;
        melee.BeginAttack(type, attackDirection);
        if (type == PlayerMelee.AttackType.AirSpin)
            airSpinCount++;
        IsSprinting = false;

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
        mouseAttackPressed = false;
        attackReleased = false;
        chargedAttackPressed = false;
        chargedAttackReleased = false;
    }

    private void CancelCharge()
    {
        IsCharging = false;
        chargeIntent = ChargeIntent.None;
        chargeInput = null;
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

    internal void RequestDamageKnockback(Vector3? sourcePosition)
    {
        if (!isActiveAndEnabled || knockback == null)
            return;

        knockback.QueueHit(sourcePosition);
        if (IsKnockedBack)
            pendingHitBounceHeight = 0f;
    }

    internal void ClearImpactMotion()
    {
        pendingHitBounceHeight = 0f;
        if (knockback != null)
            knockback.Cancel();
    }

    private void OnMeleeHit(EnemyHealth target, PlayerMelee.AttackType attack)
    {
        int attackSerial = melee.AttackSerial;
        if (lastBouncedAttackSerial == attackSerial)
            return;

        lastBouncedAttackSerial = attackSerial;
        QueueHitBounce(melee.GetHitBounceHeight(attack));
    }

    private void OnHashiHit(EnemyHealth target)
    {
        QueueHitBounce(hashi.HitBounceHeight);
    }

    private void QueueHitBounce(float height)
    {
        if (!IsDead && !IsKnockedBack && !IsDodging)
            pendingHitBounceHeight = Mathf.Max(pendingHitBounceHeight, height);
    }

    private void ApplyHitBounce()
    {
        if (pendingHitBounceHeight <= 0f)
            return;

        float upwardSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * pendingHitBounceHeight);
        pendingHitBounceHeight = 0f;

        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, upwardSpeed);
        body.linearVelocity = velocity;
        jump.Cancel();
        IsGrounded = false;
    }

    private void RefreshGroundedState()
    {
        IsGrounded =
            groundCheck != null &&
            !dodge.IsLaunchingToPillarTop &&
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
        ClearImpactMotion();
        resources?.CancelHealing();
        healRequested = false;
        throwHashiRequested = false;
        moveInput = Vector2.zero;
        jumpRequested = false;
        jumpHeld = false;
        dodgeRequested = false;
        sprintHeld = false;
        sprintExhausted = false;
        sprintNeedsRelease = false;
        wasAirborne = false;
        airSpinCount = 0;
        airborneReferenceSpeed = 0f;
        IsSprinting = false;
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
