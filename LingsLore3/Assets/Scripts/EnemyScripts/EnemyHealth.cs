using UnityEngine;
using UnityEngine.Events;

// Alvo simples para testar o garfo. Pode ser usado como base da vida dos bosses.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 200f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool somenteAtaqueBasico;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public event System.Action<EnemyHealth> Died;

    public bool IsDead => currentHealth <= 0f;
    public float CurrentHealth => currentHealth;

    public void SetBasicAttackOnly(bool active)
    {
        somenteAtaqueBasico = active;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }

    public void TakeDamage(float amount, bool ataqueBasico)
    {
        if (IsDead || (somenteAtaqueBasico && !ataqueBasico) || amount <= 0f ||
            float.IsNaN(amount) || float.IsInfinity(amount))
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name}: recebeu {amount} de dano. Vida: {currentHealth}/{maxHealth}", this);

        if (IsDead)
        {
            onDeath.Invoke();
            Died?.Invoke(this);
        }
    }

    [ContextMenu("Teste/Restaurar vida")]
    public void RestoreHealth()
    {
        if (Application.isPlaying)
            currentHealth = maxHealth;
    }
}
