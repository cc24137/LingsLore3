using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMelee : MonoBehaviour
{
    public enum AttackType { None, Light1, Light2, Light3, ChargedUp, ChargedDown, AirSpin }

    [Serializable]
    public class AttackSettings
    {
        [Min(0.05f)] public float duration = 0.45f;
        [Min(0f)] public float hitStart = 0.12f;
        [Min(0f)] public float hitEnd = 0.26f;
        [Min(0f)] public float damage = 12f;
        [Min(0f)] public float staminaCost;
        [Range(0f, 1f)] public float movementMultiplier = 0.3f;
        [Min(0f)] public float stepDistance = 0.25f;
        public Vector3 hitboxCenter = new Vector3(0f, 1f, 1.25f);
        public Vector3 hitboxSize = new Vector3(1.8f, 1.6f, 1.8f);
    }

    [Header("Dano")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector3 lineOfSightOrigin = new Vector3(0f, 1f, 0f);

    [Header("Gizmos da hitbox")]
    [SerializeField] private bool showHitboxGizmos = true;
    [SerializeField] private AttackType previewAttack = AttackType.Light1;

    [Header("Janelas de continuacao")]
    [SerializeField, Min(0f)] private float comboWindow = 0.4f;
    [SerializeField, Range(0f, 1f)] private float bufferOpensAt = 0.55f;

    [Header("Combo leve")]
    [SerializeField] private AttackSettings light1 = new AttackSettings();
    [SerializeField] private AttackSettings light2 = new AttackSettings
    {
        duration = 0.45f, damage = 14f, stepDistance = 0.3f
    };
    [SerializeField] private AttackSettings light3 = new AttackSettings
    {
        duration = 0.55f, hitStart = 0.18f, hitEnd = 0.34f,
        damage = 18f, stepDistance = 0.4f,
        hitboxCenter = new Vector3(0f, 1f, 1.4f),
        hitboxSize = new Vector3(2f, 1.8f, 2.2f)
    };

    [Header("Carregado ascendente")]
    [SerializeField] private AttackSettings chargedUp = new AttackSettings
    {
        duration = 0.65f, hitStart = 0.22f, hitEnd = 0.38f,
        damage = 32f, staminaCost = 20f, movementMultiplier = 0.15f,
        stepDistance = 0.3f,
        hitboxCenter = new Vector3(0f, 1.3f, 1.25f),
        hitboxSize = new Vector3(1.8f, 2.4f, 1.9f)
    };

    [Header("Continuacao descendente")]
    [SerializeField] private AttackSettings chargedDown = new AttackSettings
    {
        duration = 0.7f, hitStart = 0.2f, hitEnd = 0.4f,
        damage = 42f, staminaCost = 25f, movementMultiplier = 0.1f,
        stepDistance = 0.35f,
        hitboxCenter = new Vector3(0f, 1.1f, 1.35f),
        hitboxSize = new Vector3(2f, 2.4f, 2.1f)
    };

    [Header("Ataque aereo")]
    [SerializeField] private AttackSettings airSpin = new AttackSettings
    {
        duration = 0.65f, hitStart = 0.12f, hitEnd = 0.48f,
        damage = 22f, movementMultiplier = 0.3f, stepDistance = 0f,
        hitboxCenter = new Vector3(0f, 1f, 1.1f),
        hitboxSize = new Vector3(2f, 2f, 2.2f)
    };

    private Rigidbody body;
    private AttackSettings settings;
    private float attackTimer;
    private int lastLightIndex;
    private float lightChainExpires = float.NegativeInfinity;
    private float downChainExpires = float.NegativeInfinity;
    private Collider[] overlaps = new Collider[16];
    private readonly HashSet<EnemyHealth> hitTargets = new HashSet<EnemyHealth>();

    public AttackType CurrentAttack { get; private set; }
    public int AttackSerial { get; private set; }
    public bool IsActive => CurrentAttack != AttackType.None;
    public bool IsLightAttack =>
        CurrentAttack == AttackType.Light1 ||
        CurrentAttack == AttackType.Light2 ||
        CurrentAttack == AttackType.Light3;
    public bool CanQueueNext => IsActive && NormalizedTime >= bufferOpensAt;
    public bool CanChainChargedDown => !IsActive && Time.time <= downChainExpires;
    public Vector3 AttackDirection { get; private set; } = Vector3.forward;
    public Quaternion AttackRotation => Quaternion.LookRotation(AttackDirection, Vector3.up);
    public float Duration => settings != null ? Mathf.Max(0.05f, settings.duration) : 1f;
    public float NormalizedTime => IsActive ? Mathf.Clamp01(attackTimer / Duration) : 0f;
    public float MovementMultiplier => settings != null ? settings.movementMultiplier : 1f;
    public float WindupFraction => HitStart / Duration;
    public float HitEndFraction => HitEnd / Duration;

    private float HitStart => settings != null
        ? Mathf.Clamp(settings.hitStart, 0f, Duration - 0.01f) : 0f;
    private float HitEnd => settings != null
        ? Mathf.Clamp(settings.hitEnd, HitStart + 0.01f, Duration) : 0f;

    public AttackType NextLightAttack
    {
        get
        {
            if (Time.time > lightChainExpires)
                return AttackType.Light1;

            return lastLightIndex == 1 ? AttackType.Light2 :
                lastLightIndex == 2 ? AttackType.Light3 : AttackType.Light1;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public float GetStaminaCost(AttackType type)
    {
        AttackSettings selected = GetSettings(type);
        return selected != null ? Mathf.Max(0f, selected.staminaCost) : 0f;
    }

    // O motor ja verificou o estado e pagou o custo antes desta chamada.
    public void BeginAttack(AttackType type, Vector3 direction)
    {
        settings = GetSettings(type);
        if (settings == null)
            return;

        AttackDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (AttackDirection.sqrMagnitude < 0.001f)
            AttackDirection = body.rotation * Vector3.forward;

        CurrentAttack = type;
        attackTimer = 0f;
        AttackSerial++;
        hitTargets.Clear();
        lightChainExpires = float.NegativeInfinity;
        downChainExpires = float.NegativeInfinity;
    }

    // Distribui o avanco pela janela ativa sem teleportar o Rigidbody.
    public Vector3 GetAdvanceVelocity(float deltaTime)
    {
        if (!IsActive || deltaTime <= 0f)
            return Vector3.zero;

        float activeTime = Mathf.Max(0f,
            Mathf.Min(attackTimer + deltaTime, HitEnd) -
            Mathf.Max(attackTimer, HitStart));

        return AttackDirection *
            (Mathf.Max(0f, settings.stepDistance) / (HitEnd - HitStart)) *
            (activeTime / deltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (!IsActive)
            return;

        float nextTime = attackTimer + deltaTime;

        // Testa a sobreposicao do intervalo, evitando pular uma janela curta.
        bool queriedHitbox = nextTime > HitStart && attackTimer < HitEnd;
        if (queriedHitbox)
            ApplyHits();

        attackTimer = nextTime;

        if (attackTimer >= Duration)
            FinishAttack();
    }

    private void ApplyHits()
    {
        Quaternion rotation = AttackRotation;
        Vector3 center = body.position + rotation * settings.hitboxCenter;
        Vector3 halfSize = GetSafeSize(settings.hitboxSize) * 0.5f;
        int count;

        while (true)
        {
            count = Physics.OverlapBoxNonAlloc(
                center, halfSize, overlaps, rotation,
                enemyMask, QueryTriggerInteraction.Collide
            );

            if (count < overlaps.Length)
                break;

            Array.Resize(ref overlaps, overlaps.Length * 2);
        }

        Vector3 sightOrigin = body.position + rotation * lineOfSightOrigin;

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlaps[i];
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            EnemyHealth target = hit.GetComponentInParent<EnemyHealth>();
            if (target == null || target.IsDead || hitTargets.Contains(target))
                continue;

            Vector3 point = hit.ClosestPoint(sightOrigin);
            if (Physics.Linecast(
                sightOrigin, point, obstacleMask,
                QueryTriggerInteraction.Ignore))
                continue;

            // Um inimigo recebe dano uma unica vez por golpe, mesmo com varios colliders.
            hitTargets.Add(target);
            target.TakeDamage(settings.damage);
        }
    }

    private void FinishAttack()
    {
        if (IsLightAttack)
        {
            lastLightIndex = CurrentAttack == AttackType.Light1 ? 1 :
                CurrentAttack == AttackType.Light2 ? 2 : 0;
            lightChainExpires = Time.time + comboWindow;
        }
        else
        {
            lastLightIndex = 0;
            lightChainExpires = float.NegativeInfinity;
        }

        if (CurrentAttack == AttackType.ChargedUp)
            downChainExpires = Time.time + comboWindow;

        CurrentAttack = AttackType.None;
        settings = null;
        hitTargets.Clear();
    }

    private AttackSettings GetSettings(AttackType type)
    {
        switch (type)
        {
            case AttackType.Light1: return light1;
            case AttackType.Light2: return light2;
            case AttackType.Light3: return light3;
            case AttackType.ChargedUp: return chargedUp;
            case AttackType.ChargedDown: return chargedDown;
            case AttackType.AirSpin: return airSpin;
            default: return null;
        }
    }

    private static Vector3 GetSafeSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y),
            Mathf.Max(0.01f, size.z)
        );
    }

    public void Cancel()
    {
        CurrentAttack = AttackType.None;
        settings = null;
        attackTimer = 0f;
        lastLightIndex = 0;
        lightChainExpires = float.NegativeInfinity;
        downChainExpires = float.NegativeInfinity;
        hitTargets.Clear();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDrawGizmos()
    {
        if (!showHitboxGizmos || !enabled)
            return;

        // Em Play, mostra apenas o golpe em andamento. Fora de Play, permite
        // conferir cada hitbox escolhendo Preview Attack no Inspector.
        if (Application.isPlaying && !IsActive)
            return;

        AttackSettings selected = Application.isPlaying ? settings : GetSettings(previewAttack);
        if (selected == null)
            return;

        Rigidbody owner = body != null ? body : GetComponent<Rigidbody>();
        Vector3 position = owner != null ? owner.position : transform.position;
        Quaternion rotation = Application.isPlaying ? AttackRotation :
            owner != null ? owner.rotation : transform.rotation;
        Vector3 size = GetSafeSize(selected.hitboxSize);
        bool damageWindow = Application.isPlaying &&
            attackTimer >= HitStart && attackTimer < HitEnd;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        // A consulta de dano usa a pose do Rigidbody e dimensoes em unidades
        // de mundo; a escala do Transform nao deve alterar o desenho.
        Gizmos.matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        Gizmos.color = damageWindow ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(selected.hitboxCenter, size);

        if (damageWindow)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawCube(selected.hitboxCenter, size);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

}
