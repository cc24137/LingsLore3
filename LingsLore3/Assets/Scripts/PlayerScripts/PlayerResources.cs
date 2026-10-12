using UnityEngine;
using UnityEngine.Events;

// O motor (-100) gasta stamina antes de calcularmos a regeneracao.
[DefaultExecutionOrder(-90)]
public class PlayerResources : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 25f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.75f;

    [Header("Valores atuais - acompanhe durante o Play Mode")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentStamina;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    private PlayerMotor motor;
    private float lastStaminaUseTime = float.NegativeInfinity;

    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;

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

    private void FixedUpdate()
    {
        if (IsDead)
            return;

        // O motor informa se alguma acao esta bloqueando a regeneracao.
        if (motor != null && motor.BlocksStaminaRegeneration)
            return;

        if (Time.time - lastStaminaUseTime < staminaRegenDelay)
            return;

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRegenPerSecond * Time.fixedDeltaTime
        );
    }

    // Para acoes com custo fixo: ou paga tudo, ou a acao nao comeca.
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

    // Para consumo continuo: usa o restante e para em zero.
    public void DrainStamina(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return;

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
    }

    // Retorna true somente quando o dano foi aplicado.
    public bool TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return false;

        if (motor != null && motor.IsInvulnerable)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (IsDead)
        {
            Debug.Log("Ling morreu.", this);
            onDeath.Invoke();
        }

        return true;
    }

    // Cura nao revive. Para reiniciar o personagem, use RestoreFullResources.
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) ||
            float.IsInfinity(amount))
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void RestoreFullResources()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        lastStaminaUseTime = float.NegativeInfinity;
    }

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
