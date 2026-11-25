using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class MiniTralalero : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0.1f)] private float velocidade = 4.8f;
    [SerializeField, Min(0.1f)] private float velocidadeDaInvestida = 11f;
    [SerializeField, Min(0.1f)] private float duracaoDaInvestida = 0.55f;
    [SerializeField, Min(0.1f)] private float tempoDeVida = 13f;
    [SerializeField, Min(0f)] private float danoDeContato = 5f;
    [SerializeField, Min(0f)] private float danoDaInvestida = 9f;
    [SerializeField, Min(0.1f)] private float intervaloDoDano = 1f;

    private static readonly int AnimacaoAndando = Animator.StringToHash("isWalking");
    private static readonly int AnimacaoParado = Animator.StringToHash("isIdle");
    private static readonly int AnimacaoAviso = Animator.StringToHash("isGivingCue");

    private enum Estado { Aguardando, Perseguindo, Preparando, Investindo, Desativado }

    private EnemyHealth vida;
    private Rigidbody corpo;
    private CapsuleCollider contato;
    private PlayerResources alvo;
    private Rigidbody corpoDoAlvo;
    private Collider colisorDoAlvo;
    private LayerMask obstaculos;
    private IReadOnlyList<MiniTralalero> grupo;
    private Vector3 centroDaArena;
    private float raioUtilDaArena;
    private Vector3 direcaoDaInvestida;
    private float fimDaInvestida;
    private float proximoDano;
    private Estado estado;

    public bool EstaAtivo => estado != Estado.Desativado && isActiveAndEnabled;

    private void Awake()
    {
        vida = GetComponent<EnemyHealth>();
        corpo = GetComponent<Rigidbody>();
        contato = GetComponent<CapsuleCollider>();
    }

    private void OnEnable() => vida.Died += AoMorrer;

    private void OnDisable() => vida.Died -= AoMorrer;

    public void Iniciar(PlayerResources jogador, Rigidbody corpoDoJogador, Collider colisorDoJogador,
        LayerMask mascaraDeObstaculos, Bounds arena, IReadOnlyList<MiniTralalero> aliados)
    {
        alvo = jogador;
        corpoDoAlvo = corpoDoJogador;
        colisorDoAlvo = colisorDoJogador;
        obstaculos = mascaraDeObstaculos;
        grupo = aliados;
        centroDaArena = arena.center;
        raioUtilDaArena = Mathf.Max(0f, Mathf.Min(arena.extents.x, arena.extents.z) - contato.radius - 0.1f);
        MudarEstado(Estado.Perseguindo);
        Destroy(gameObject, tempoDeVida);
    }

    public void PrepararInvestida()
    {
        if (estado != Estado.Perseguindo)
            return;

        Vector3 ateLing = Horizontal(corpoDoAlvo.position - corpo.position);
        direcaoDaInvestida = ateLing.sqrMagnitude > 0.001f ? ateLing.normalized : transform.forward;
        corpo.MoveRotation(Quaternion.LookRotation(direcaoDaInvestida));
        MudarEstado(Estado.Preparando);
    }

    public void Investir()
    {
        if (estado != Estado.Preparando)
            return;

        fimDaInvestida = Time.time + duracaoDaInvestida;
        MudarEstado(Estado.Investindo);
    }

    public void CancelarOrdem()
    {
        if (estado == Estado.Preparando)
            MudarEstado(Estado.Perseguindo);
    }

    private void FixedUpdate()
    {
        if (alvo == null || alvo.IsDead)
            return;

        if (estado == Estado.Investindo && Time.time >= fimDaInvestida)
            MudarEstado(Estado.Perseguindo);

        if (estado == Estado.Perseguindo)
            Mover(DirecaoDaPerseguicao(), velocidade, desviar: true);
        else if (estado == Estado.Investindo)
            Mover(direcaoDaInvestida, velocidadeDaInvestida, desviar: false);
    }

    private Vector3 DirecaoDaPerseguicao()
    {
        const float separacao = 1.15f;
        Vector3 rumo = Horizontal(corpoDoAlvo.position - corpo.position).normalized;
        foreach (MiniTralalero outro in grupo)
        {
            if (outro == null || outro == this || !outro.EstaAtivo)
                continue;

            Vector3 delta = Horizontal(corpo.position - outro.corpo.position);
            float distancia = delta.magnitude;
            if (distancia < separacao && distancia > 0.001f)
                rumo += delta / distancia * (1f - distancia / separacao);
        }
        return rumo.normalized;
    }

    private void Mover(Vector3 direcao, float rapidez, bool desviar)
    {
        if (direcao.sqrMagnitude < 0.001f)
            return;

        float passo = rapidez * Time.fixedDeltaTime;
        Vector3 centro = corpo.position + contato.center;
        if (desviar && Physics.SphereCast(centro, contato.radius, direcao, out RaycastHit parede,
            passo + 0.65f, obstaculos, QueryTriggerInteraction.Ignore))
        {
            Vector3 normal = Horizontal(parede.normal).normalized;
            Vector3 tangente = Vector3.Cross(Vector3.up, normal);
            if (normal.sqrMagnitude > 0.001f)
            {
                float lado = Vector3.Dot(tangente, direcao) >= 0f ? 1f : -1f;
                direcao = (tangente * lado + normal * 0.2f).normalized;
            }
        }

        Vector3 destino = corpo.position + direcao * passo;
        Vector3 noPiso = centroDaArena + Vector3.ClampMagnitude(Horizontal(destino - centroDaArena), raioUtilDaArena);
        destino = new Vector3(noPiso.x, destino.y, noPiso.z);
        Vector3 deslocamento = destino - corpo.position;
        float distancia = deslocamento.magnitude;
        if (distancia > 0.001f && Physics.SphereCast(centro, contato.radius, deslocamento / distancia,
            out RaycastHit bloqueio, distancia + 0.04f, obstaculos, QueryTriggerInteraction.Ignore))
            destino = corpo.position + deslocamento / distancia * Mathf.Clamp(bloqueio.distance - 0.04f, 0f, distancia);

        corpo.MoveRotation(Quaternion.LookRotation(direcao));
        corpo.MovePosition(destino);
    }

    private static Vector3 Horizontal(Vector3 vetor) => new Vector3(vetor.x, 0f, vetor.z);

    private void OnTriggerEnter(Collider outro) => TentarDarDano(outro);

    private void OnTriggerStay(Collider outro) => TentarDarDano(outro);

    private void TentarDarDano(Collider outro)
    {
        if (outro != colisorDoAlvo || alvo == null || Time.time < proximoDano
            || (estado != Estado.Perseguindo && estado != Estado.Investindo))
            return;

        float dano = estado == Estado.Investindo ? danoDaInvestida : danoDeContato;
        if (alvo.TakeBossDamage(dano, corpo.position))
            proximoDano = Time.time + intervaloDoDano;
    }

    private void MudarEstado(Estado novo)
    {
        estado = novo;
        if (animator == null)
            return;

        animator.SetBool(AnimacaoAndando, novo == Estado.Perseguindo || novo == Estado.Investindo);
        animator.SetBool(AnimacaoParado, novo == Estado.Aguardando || novo == Estado.Desativado);
        animator.SetBool(AnimacaoAviso, novo == Estado.Preparando);
    }

    private void AoMorrer(EnemyHealth inimigo)
    {
        MudarEstado(Estado.Desativado);
        contato.enabled = false;
        Destroy(gameObject, 0.15f);
    }
}
