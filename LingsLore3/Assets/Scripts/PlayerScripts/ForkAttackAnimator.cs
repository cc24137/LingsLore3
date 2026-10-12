using UnityEngine;

// Apenas envia parametros. Os estados, transicoes e clips ficam no Animator Controller.
[RequireComponent(typeof(PlayerMotor), typeof(PlayerMelee), typeof(PlayerDodge))]
public class ForkAttackAnimator : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool useAnimatorForDodge;

    private PlayerMotor motor;
    private PlayerMelee melee;
    private PlayerDodge dodge;
    private bool configured;
    private bool originalProceduralDodge;

    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int Sprinting = Animator.StringToHash("Sprinting");
    private static readonly int Grounded = Animator.StringToHash("Grounded");
    private static readonly int Rising = Animator.StringToHash("Rising");
    private static readonly int Dead = Animator.StringToHash("Dead");
    private static readonly int Dodging = Animator.StringToHash("Dodging");
    private static readonly int Attacking = Animator.StringToHash("Attacking");
    private static readonly int AttackId = Animator.StringToHash("AttackId");
    private static readonly int Charging = Animator.StringToHash("Charging");
    private static readonly int ChargeDown = Animator.StringToHash("ChargeDown");
    private static readonly int Free = Animator.StringToHash("Free");

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        melee = GetComponent<PlayerMelee>();
        dodge = GetComponent<PlayerDodge>();
    }

    private void OnEnable()
    {
        originalProceduralDodge = dodge.UseProceduralRollAnimation;

        if (animator != null)
        {
            // Agora o Animator reproduz os clips normalmente.
            animator.speed = 1f;
            animator.applyRootMotion = false;
        }
    }

    private void Start()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("Atribua o Animator do LingVisual e seu Controller.", this);
            enabled = false;
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        configured = true;
        configured &= RequireParameter(parameters, "Moving", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Sprinting", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Grounded", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Rising", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Dead", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Dodging", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Attacking", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Charging", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "ChargeDown", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "Free", AnimatorControllerParameterType.Bool);
        configured &= RequireParameter(parameters, "AttackId", AnimatorControllerParameterType.Int);

        if (!configured)
            enabled = false;
    }

    private bool RequireParameter(
        AnimatorControllerParameter[] parameters,
        string parameterName,
        AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
                return true;
        }

        Debug.LogError($"Crie o parametro {parameterName} ({type}) no Animator Controller.", this);
        return false;
    }

    private void Update()
    {
        if (!configured || !animator.isActiveAndEnabled)
        {
            dodge.UseProceduralRollAnimation = originalProceduralDodge;
            return;
        }

        dodge.UseProceduralRollAnimation = useAnimatorForDodge
            ? false : originalProceduralDodge;

        bool dead = motor.IsDead;
        bool dodging = motor.IsDodging;
        bool attacking = melee.IsActive && !dead && !dodging;
        bool charging = motor.IsCharging && !dead && !dodging && !attacking;
        bool free = !dead && !dodging && !attacking && !motor.IsCharging;

        animator.SetBool(Moving, motor.IsMoving);
        animator.SetBool(Sprinting, motor.IsSprinting);
        animator.SetBool(Grounded, motor.IsGrounded);
        animator.SetBool(Rising, motor.IsRising);
        animator.SetBool(Dead, dead);
        animator.SetBool(Dodging, useAnimatorForDodge && dodging && !dead);
        animator.SetBool(Attacking, attacking);
        animator.SetInteger(AttackId, attacking ? (int)melee.CurrentAttack : 0);
        animator.SetBool(Charging, charging);
        animator.SetBool(ChargeDown, motor.IsChargingDown);
        animator.SetBool(Free, free);
    }

    private void OnDisable()
    {
        if (dodge != null)
            dodge.UseProceduralRollAnimation = originalProceduralDodge;
    }
}
