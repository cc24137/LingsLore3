using UnityEngine;
using UnityEngine.Events;

// Alvo simples para testar o garfo. Pode ser usado como base da vida dos bosses.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 200f;
    [SerializeField, Min(0f)] private float secondBarHealth;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool somenteAtaqueBasico;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public event System.Action<EnemyHealth> Died;
    public event System.Action<int> HealthBarChanged;

    private float invulnerableUntil;
    private bool usingSecondBar;

    public bool IsDead => currentHealth <= 0f;
    public float CurrentHealth => currentHealth;
    public int CurrentBar => usingSecondBar ? 2 : 1;
    public float MaxHealth => usingSecondBar ? secondBarHealth : maxHealth;

    public void SetBasicAttackOnly(bool active)
    {
        somenteAtaqueBasico = active;
    }

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool ataqueBasico)
    {
        TryTakeDamage(amount, ataqueBasico);
    }

    public virtual bool TryTakeDamage(float amount, bool ataqueBasico = false)
    {
        if (IsDead || (somenteAtaqueBasico && !ataqueBasico) || amount <= 0f ||
            float.IsNaN(amount) || float.IsInfinity(amount) || Time.time < invulnerableUntil)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        DamageFlash.PlayOn(this);
        Debug.Log($"{name}: recebeu {amount} de dano. Vida: {currentHealth}/{MaxHealth}", this);

        if (IsDead)
        {
            if (!usingSecondBar && secondBarHealth > 0f)
            {
                usingSecondBar = true;
                currentHealth = secondBarHealth;
                HealthBarChanged?.Invoke(CurrentBar);
            }
            else
            {
                onDeath.Invoke();
                Died?.Invoke(this);
            }
        }

        return true;
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    [ContextMenu("Teste/Restaurar vida")]
    public void RestoreHealth()
    {
        if (!Application.isPlaying)
            return;

        bool changedBar = usingSecondBar;
        usingSecondBar = false;
        currentHealth = maxHealth;
        invulnerableUntil = 0f;
        if (changedBar)
            HealthBarChanged?.Invoke(CurrentBar);
    }
}
