using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HitboxParte : MonoBehaviour
{
    public enum ParteCorpo { Frente, Barbatana, Outro }

    [SerializeField] private Enemy inimigo;
    [SerializeField] private ParteCorpo parte;

    private void Awake()
    {
        if (inimigo == null)
            inimigo = GetComponentInParent<Enemy>();
        if (inimigo == null)
            Debug.LogError("HitboxParte: configure o Enemy responsavel por esta hitbox.", this);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider outro)
    {
        Debug.Log("Entrou na parte: " + parte);
        AvisarInimigo(outro);
    }

    private void OnTriggerStay(Collider outro)
    {
        // Permite dano de contato e detecta a frente mesmo se o jogador ja estava nela.
        AvisarInimigo(outro);
    }

    private void AvisarInimigo(Collider outro)
    {
        if (isActiveAndEnabled && inimigo != null)
            inimigo.JogadorEntrouNaHitbox(parte, outro);
    }
}
