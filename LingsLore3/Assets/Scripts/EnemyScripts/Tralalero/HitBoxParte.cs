using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HitboxParte : MonoBehaviour
{
    public enum ParteCorpo { Frente, Barbatana, Cauda, Outro }

    [SerializeField] private Enemy inimigo;
    [SerializeField] private ParteCorpo parte;

    private Collider hitbox;

    private void Awake()
    {
        hitbox = GetComponent<Collider>();
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

    private void FixedUpdate()
    {
        if (inimigo == null || !inimigo.HitboxAtiva(parte) || !hitbox.enabled)
            return;

        // Testa os pilares mesmo quando suas camadas nao geram eventos de trigger entre si.
        foreach (PillarHealth pilar in PillarHealth.ActivePillars)
        {
            if (!pilar.CanBeHit || !hitbox.bounds.Intersects(pilar.Shaft.bounds))
                continue;

            Collider coluna = pilar.Shaft;
            if (Physics.ComputePenetration(coluna, coluna.transform.position, coluna.transform.rotation,
                hitbox, hitbox.transform.position, hitbox.transform.rotation, out _, out _))
            {
                Vector3 ponto = coluna.ClosestPoint(hitbox.bounds.center);
                inimigo.AtingirPilar(parte, pilar, ponto);
            }
        }
    }

    private void AvisarInimigo(Collider outro)
    {
        if (isActiveAndEnabled && inimigo != null)
            inimigo.JogadorEntrouNaHitbox(parte, outro);
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider caixa))
            return;

        bool ativa = Application.isPlaying && caixa.enabled && caixa.isTrigger &&
            isActiveAndEnabled && inimigo != null && inimigo.HitboxAtiva(parte);
        Color cor = parte switch
        {
            ParteCorpo.Frente => new Color(1f, 0.5f, 0.15f),
            ParteCorpo.Barbatana => new Color(0.15f, 0.8f, 1f),
            ParteCorpo.Cauda => new Color(0.85f, 0.45f, 1f),
            _ => Color.white
        };

        Matrix4x4 matrizAnterior = Gizmos.matrix;
        Color corAnterior = Gizmos.color;
        Gizmos.matrix = caixa.transform.localToWorldMatrix;
        if (ativa)
        {
            Gizmos.color = new Color(cor.r, cor.g, cor.b, 0.23f);
            Gizmos.DrawCube(caixa.center, caixa.size);
        }
        cor.a = ativa || !Application.isPlaying ? 1f : 0.4f;
        Gizmos.color = cor;
        Gizmos.DrawWireCube(caixa.center, caixa.size);
        Gizmos.matrix = matrizAnterior;
        Gizmos.color = corAnterior;
    }
}
