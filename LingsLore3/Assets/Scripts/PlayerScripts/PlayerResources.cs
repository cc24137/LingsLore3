using System;
using UnityEngine;
using UnityEngine.Events;

// O motor (-100) gasta stamina antes de calcularmos a regeneracao.
[DefaultExecutionOrder(-90)]
public class PlayerResources : MonoBehaviour
{
    private const float HealRechargeInterval = 15f;
    private const float HealAmount = 30f;
    private const float HealDuration = 1.5f;

    [Header("Vida")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 25f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.75f;

    [Header("Valores atuais - acompanhe durante o Play Mode")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentStamina;
    [SerializeField] private int healCharges;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    [Header("Comportamento de morte")]
    [Tooltip("Desmarque no Tutorial para impedir a chamada do evento antigo, como FimDeJogo.ExibirDerrota.")]
    [SerializeField] private bool invokeUnityEventOnDeath = true;

    private PlayerMotor motor;
    private float lastStaminaUseTime = float.NegativeInfinity;
    private float respawnInvulnerableUntil = float.NegativeInfinity;
    private bool respawnDoTutorial;
    private bool iniciou;
    private double healStartedAt = -1d;
    private double nextHealChargeAt;
    private float staminaDaEsquiva;
    private static int MaxHealCharges => SelecaoDificuldade.DificuldadeEscolhida switch
    {
        Dificuldade.Facil => 5,
        Dificuldade.Impossivel => 3,
        _ => 4
    };

    // Evento C# usado pelo ContadorCreditos para reiniciar a rodada atual.
    public event Action Died;
    internal event Action CuraConcluida;

    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;
    public int HealCharges => healCharges;
    public bool IsHealing => healStartedAt >= 0d;
    internal float HealProgress => Mathf.Clamp01((float)((Time.timeAsDouble - healStartedAt) / HealDuration));

    public float HealthNormalized =>
        maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

    public float StaminaNormalized =>
        maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        RestoreFullResources();
    }

    private void Start()
    {
        iniciou = true;
        EfeitosSonoros.Tocar(Som.AparecimentoDoLing);
    }

    private void OnDisable()
    {
        CancelHealing();
    }

    private void Update()
    {
        if (BalanceamentoDificuldade.Facil && !IsDead && healCharges < MaxHealCharges &&
            Time.timeAsDouble >= nextHealChargeAt)
        {
            healCharges++;
            nextHealChargeAt += HealRechargeInterval;
        }

        if (!IsHealing || Time.timeAsDouble - healStartedAt < HealDuration)
            return;

        CancelHealing();
        float vidaAnterior = currentHealth;
        Heal(HealAmount);
        if (currentHealth > vidaAnterior)
            CuraConcluida?.Invoke();
    }

    private void FixedUpdate()
    {
        if (IsDead)
            return;

        if (motor != null && motor.BlocksStaminaRegeneration)
            return;

        if (Time.time - lastStaminaUseTime < staminaRegenDelay)
            return;

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRegenPerSecond * Time.fixedDeltaTime
        );
    }

    public bool TrySpendStamina(float amount)
    {
        if (IsDead || amount < 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return false;

        if (amount == 0f)
            return true;

        if (currentStamina < amount)
            return false;

        currentStamina -= amount;
        lastStaminaUseTime = Time.time;
        return true;
    }

    public void DrainStamina(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return;

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
    }

    internal bool TrySpendDodgeStamina(float amount)
    {
        if (!TrySpendStamina(amount))
            return false;

        staminaDaEsquiva = amount;
        return true;
    }

    internal bool EvitouDanoDeChefe()
    {
        if (motor == null || !motor.IsInvulnerable)
            return false;

        if (BalanceamentoDificuldade.Impossivel && staminaDaEsquiva > 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaDaEsquiva);
            staminaDaEsquiva = 0f;
        }

        return true;
    }

    internal bool TakeBossDamage(float amount, Vector3 sourcePosition, bool ignorarInvulnerabilidadeDaEsquiva = false)
    {
        if (!ignorarInvulnerabilidadeDaEsquiva && EvitouDanoDeChefe())
            return false;

        return TakeDamage(amount * BalanceamentoDificuldade.Atual.Dano, sourcePosition, ignorarInvulnerabilidadeDaEsquiva);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, null);
        Debug.Log("Ling tem " + currentHealth +" de vida");
    }

    public bool TakeDamage(float amount, Vector3? sourcePosition) =>
        TakeDamage(amount, sourcePosition, ignorarInvulnerabilidadeDaEsquiva: false);

    public bool TakeDamage(float amount, Vector3? sourcePosition, bool ignorarInvulnerabilidadeDaEsquiva)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return false;

        // A protecao vale para qualquer origem de dano, inclusive OnTriggerStay.
        if (Time.time < respawnInvulnerableUntil)
            return false;

        if (!ignorarInvulnerabilidadeDaEsquiva && motor != null && motor.IsInvulnerable)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        CancelHealing();
        DamageFlash.PlayOn(this);
        EfeitosSonoros.Tocar(Som.DanoDoLing);

        if (IsDead)
        {
            EfeitosSonoros.Tocar(Som.MorteDoLing);
            motor?.ClearImpactMotion();
            Debug.Log("Ling morreu.", this);
            Died?.Invoke();

            // Mantem a tela de derrota na arena normal; no Tutorial, desmarcar no Inspector.
            if (invokeUnityEventOnDeath && !respawnDoTutorial)
                onDeath.Invoke();
        }
        else
        {
            motor?.RequestDamageKnockback(sourcePosition);
        }

        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    internal void TryStartHealing()
    {
        if (!isActiveAndEnabled || IsDead || IsHealing ||
            healCharges <= 0 || currentHealth >= maxHealth)
            return;

        if (healCharges == MaxHealCharges)
            nextHealChargeAt = Time.timeAsDouble + HealRechargeInterval;

        healCharges--;
        healStartedAt = Time.timeAsDouble;
        EfeitosSonoros.Tocar(Som.Cura);
    }

    internal void CancelHealing()
    {
        healStartedAt = -1d;
    }

    internal void PrepararAulaDeCura()
    {
        CancelHealing();
        float deficitDaAula = Mathf.Min(HealAmount, maxHealth * 0.3f);
        currentHealth = Mathf.Min(currentHealth, maxHealth - deficitDaAula);

        if (healCharges > 0)
            return;

        healCharges = 1;
        nextHealChargeAt = Time.timeAsDouble + HealRechargeInterval;
    }

    internal void RestoreHealthAndHealCharges()
    {
        currentHealth = maxHealth;
        healCharges = MaxHealCharges;
        nextHealChargeAt = 0d;
        CancelHealing();
    }

    public void RestoreFullResources()
    {
        if (TryGetComponent<DamageFlash>(out var flash))
            flash.Clear();

        bool reapareceu = iniciou && IsDead;
        RestoreHealthAndHealCharges();
        currentStamina = maxStamina;
        staminaDaEsquiva = 0f;
        lastStaminaUseTime = float.NegativeInfinity;
        if (reapareceu)
            EfeitosSonoros.Tocar(Som.AparecimentoDoLing);
    }

    // Chamado pelo ContadorCreditos depois de reposicionar o jogador.
    public void SetRespawnInvulnerability(float duration)
    {
        respawnInvulnerableUntil = Time.time + Mathf.Max(0f, duration);
    }

    public void DefinirRespawnDoTutorial(bool ativo) => respawnDoTutorial = ativo;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaRegenPerSecond = Mathf.Max(0f, staminaRegenPerSecond);
        staminaRegenDelay = Mathf.Max(0f, staminaRegenDelay);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    [ContextMenu("Teste/Receber 25 de dano")]
    private void TestDamage()
    {
        if (Application.isPlaying)
            TakeDamage(25f);
    }

    [ContextMenu("Teste/Curar 25 de vida")]
    private void TestHeal()
    {
        if (Application.isPlaying)
            Heal(25f);
    }

    [ContextMenu("Teste/Restaurar vida e stamina")]
    private void TestRestore()
    {
        if (Application.isPlaying)
            RestoreFullResources();
    }
}
