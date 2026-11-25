using UnityEngine;
using UnityEngine.Events;

// Alvo simples para testar o garfo. Pode ser usado como base da vida dos bosses.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 200f;
    [SerializeField, Min(0f)] private float secondBarHealth;
    [SerializeField, Min(0f)] private float thirdBarHealth;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool somenteAtaqueBasico;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public event System.Action<EnemyHealth> Died;
    public event System.Action<int> HealthBarChanged;

    private float invulnerableUntil;
    public bool IsDead => currentHealth <= 0f;
    public float CurrentHealth => currentHealth;
    public int CurrentBar { get; private set; } = 1;
    public int BarCount => secondBarHealth <= 0f ? 1 : (thirdBarHealth > 0f ? 3 : 2);
    public float MaxHealth => CurrentBar switch
    {
        2 => secondBarHealth,
        3 => thirdBarHealth,
        _ => maxHealth
    };

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
            if ((CurrentBar == 1 && secondBarHealth > 0f) || (CurrentBar == 2 && thirdBarHealth > 0f))
            {
                CurrentBar++;
                currentHealth = MaxHealth;
                HealthBarChanged?.Invoke(CurrentBar);
            }
            else
            {
                if (this is Enemy || TryGetComponent<BossNetanyahu>(out _) ||
                    TryGetComponent<BossTungTungTungSahur>(out _))
                    EfeitosSonoros.Tocar(Som.MorteDoChefe);

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

        bool changedBar = CurrentBar != 1;
        CurrentBar = 1;
        currentHealth = maxHealth;
        invulnerableUntil = 0f;
        if (changedBar)
            HealthBarChanged?.Invoke(CurrentBar);
    }
}
