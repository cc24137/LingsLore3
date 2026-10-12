using UnityEngine;
using UnityEngine.Events;

// Alvo simples para testar o garfo. Pode ser usado como base da vida dos bosses.
public class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 200f;
    [SerializeField] private float currentHealth;
    [SerializeField] private UnityEvent onDeath = new UnityEvent();

    public bool IsDead => currentHealth <= 0f;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"{name}: recebeu {amount} de dano. Vida: {currentHealth}/{maxHealth}", this);

        if (IsDead)
            onDeath.Invoke();
    }

    [ContextMenu("Teste/Restaurar vida")]
    private void RestoreHealth()
    {
        if (Application.isPlaying)
            currentHealth = maxHealth;
    }
}
