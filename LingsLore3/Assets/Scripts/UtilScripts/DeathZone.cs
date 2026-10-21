using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class DieZone : MonoBehaviour
{
    [SerializeField, Min(1f)] private float dano = 1000000f;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        AplicarDano(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Tenta novamente se a esquiva bloqueou o dano na entrada.
        AplicarDano(other);
    }

    private void AplicarDano(Collider other)
    {
        // O collider pode estar em um filho do personagem.
        PlayerResources jogador = other.GetComponentInParent<PlayerResources>();
        if (jogador != null)
        {
            if (!jogador.IsDead)
                jogador.TakeDamage(dano);

            return;
        }

        EnemyHealth inimigo = other.GetComponentInParent<EnemyHealth>();
        if (inimigo != null && !inimigo.IsDead)
            inimigo.TakeDamage(dano);
    }
}
