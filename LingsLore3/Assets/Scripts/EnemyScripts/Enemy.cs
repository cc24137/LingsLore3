using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : EnemyHealth
{
    [Header("Referencias")]
    [SerializeField] private Transform jogadorPosition;
    [SerializeField] private Animator animator;

    [Header("Perseguicao")]
    [SerializeField, Min(0.01f)] private float velocidadeAndando = 2.5f;
    [SerializeField, Min(0f)] private float distanciaParada = 1.5f;
    [SerializeField, Min(0f)] private float velocidadeRotacao = 360f;

    [Header("Ataque de pulo (investida horizontal)")]
    [SerializeField, Min(0.01f)] private float distanciaParaAtaquePulo = 8f;
    [SerializeField, Min(0.01f)] private float velocidadeAtaquePulo = 25f;
    [SerializeField, Min(0.01f)] private float distanciaChegadaAtaque = 0.35f;
    [SerializeField, Min(0.01f)] private float duracaoMaximaAtaque = 3f;
    [Min(0f)] public float delayAfterAttack = 2f;
    [Min(0f)] public float tempoDaAnimacaoCue = 2f;

    [Header("Dano ao jogador")]
    [SerializeField, Min(0f)] private float danoFrente = 25f;
    [SerializeField, Min(0f)] private float danoBarbatana = 10f;
    [SerializeField, Min(0.01f)] private float intervaloDanoBarbatana = 0.75f;
    [Tooltip("Conecte ao metodo que reduz a vida do jogador, usando o float dinamico.")]
    [SerializeField] private UnityEvent<float> aoCausarDano = new UnityEvent<float>();

    private enum State { idle, walking, jumpAttack, groundAttack, givingCue }

    [Header("Estado atual (controlado pelo codigo)")]
    [SerializeField] private State estado = State.walking;

    private static readonly int IsIdle = Animator.StringToHash("isIdle");
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int IsGivingCue = Animator.StringToHash("isGivingCue");

    private Rigidbody body;
    private Vector3 jumpAttackCoordinate;
    private float fimDoAtaque;
    private float fimDoDescanso;
    private float fimDaCue;
    private float proximoDanoBarbatana;
    private bool frenteAcertouNesteAtaque;
    private State proximoAtaque;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (jogadorPosition == null)
            Debug.LogWarning("Enemy: arraste o objeto principal do jogador para Jogador Position.", this);
        if (animator == null)
            Debug.LogWarning("Enemy: configure o Animator para exibir as animacoes.", this);
    }

    private void OnEnable()
    {
        frenteAcertouNesteAtaque = false;
        proximoDanoBarbatana = float.NegativeInfinity;
        fimDoDescanso = 0f;
        Died += AoMorrer;
        MudarEstado(State.walking);
    }

    private void FixedUpdate()
    {
        Debug.Log(estado);
        if (IsDead || jogadorPosition == null || !jogadorPosition.gameObject.activeInHierarchy)
        {
            PararMovimento();
            AtualizarAnimacao();
            return;
        }

        switch (estado)
        {
            case State.idle:
                PararMovimento();
                AtualizarAnimacao();
                if (Time.time >= fimDoDescanso)
                    MudarEstado(State.walking);
                break;

            case State.walking:
                if (DistanciaHorizontal(jogadorPosition.position) >= distanciaParaAtaquePulo)
                {
                    setJumpAttack();
                    AtualizarCueState();
                }
                else
                {
                    bool andando = MoverAte(jogadorPosition.position,
                        velocidadeAndando, distanciaParada);
                    AtualizarAnimacao(andando);
                }
                break;

            case State.givingCue:
                AtualizarCueState();
                break;

            case State.jumpAttack:
                AtualizarAtaquePulo();
                break;
        }
    }

    private void AtualizarCueState()
    {
        PararMovimento();
        if (Time.time > fimDaCue)
        {
            fimDoAtaque = Time.time + duracaoMaximaAtaque;
            MudarEstado(State.jumpAttack);
        }
    }

    private void setJumpAttack()
    {
        // Guarda uma posicao, em vez de uma referencia que acompanha o jogador.
        jumpAttackCoordinate = jogadorPosition.position;
        jumpAttackCoordinate.y = body.position.y;
        frenteAcertouNesteAtaque = false;
        //fimDoAtaque = Time.time + duracaoMaximaAtaque;
        fimDaCue = Time.time + tempoDaAnimacaoCue;
        MudarEstado(State.givingCue);
    }

    private void AtualizarAtaquePulo()
    {
        if (Time.time >= fimDoAtaque ||
            DistanciaHorizontal(jumpAttackCoordinate) <= distanciaChegadaAtaque + 0.01f)
        {
            fimDoDescanso = Time.time + delayAfterAttack;
            MudarEstado(State.idle);
            return;
        }

        MoverAte(jumpAttackCoordinate, velocidadeAtaquePulo, distanciaChegadaAtaque);
        AtualizarAnimacao();
    }

    private bool MoverAte(Vector3 destino, float velocidade, float distanciaLimite)
    {
        Vector3 direcao = destino - body.position;
        direcao.y = 0f;
        float distancia = direcao.magnitude;

        RotacionarPara(destino);
        if (distancia <= distanciaLimite)
        {
            PararMovimento();
            return false;
        }

        // Limita o ultimo passo para nao ultrapassar o destino.
        float velocidadeAtual = Mathf.Min(velocidade,
            (distancia - distanciaLimite) / Time.fixedDeltaTime);
        Vector3 movimento = direcao.normalized * velocidadeAtual;
        body.linearVelocity = new Vector3(movimento.x, body.linearVelocity.y, movimento.z);
        return true;
    }

    private void RotacionarPara(Vector3 destino)
    {
        Vector3 direcao = destino - body.position;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.0001f)
            return;

        body.MoveRotation(Quaternion.RotateTowards(body.rotation,
            Quaternion.LookRotation(direcao), velocidadeRotacao * Time.fixedDeltaTime));
    }

    private float DistanciaHorizontal(Vector3 destino)
    {
        Vector3 diferenca = destino - body.position;
        diferenca.y = 0f;
        return diferenca.magnitude;
    }

    private void PararMovimento()
    {
        if (body != null && !body.isKinematic)
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
    }

    private void MudarEstado(State novoEstado)
    {
        estado = novoEstado;
        if (estado == State.idle || estado == State.givingCue)
            PararMovimento();
        AtualizarAnimacao();
    }
private void AtualizarAnimacao(bool andando = true)
{
    if (animator == null)
        return;

    bool preparando = estado == State.givingCue;
    bool movimentando = estado == State.jumpAttack ||
        (estado == State.walking && andando);

    animator.SetBool(IsIdle, !preparando && !movimentando);
    animator.SetBool(IsWalking, movimentando);
    animator.SetBool(IsGivingCue, preparando);
}

    // Mantem a assinatura usada pelo seu script de hitbox original.
    public void JogadorEntrouNaHitbox(HitboxParte.ParteCorpo parte)
    {
        if (!isActiveAndEnabled || IsDead || jogadorPosition == null ||
            !jogadorPosition.gameObject.activeInHierarchy)
            return;

        float dano;
        switch (parte)
        {
            case HitboxParte.ParteCorpo.Frente:
            //Debug.Log()
                if (estado != State.jumpAttack || frenteAcertouNesteAtaque)
                    return;
                dano = danoFrente;
                if (dano <= 0f)
                    return;
                frenteAcertouNesteAtaque = true;
                break;

            case HitboxParte.ParteCorpo.Barbatana:
                if (Time.time < proximoDanoBarbatana)
                    return;
                dano = danoBarbatana;
                if (dano <= 0f)
                    return;
                proximoDanoBarbatana = Time.time + intervaloDanoBarbatana;
                break;

            default:
                return;
        }


        Debug.Log("Invocando o aoCausarDano");
        aoCausarDano.Invoke(dano);
    }

    // O novo HitboxParte usa esta sobrecarga para filtrar o jogador correto.
    public void JogadorEntrouNaHitbox(HitboxParte.ParteCorpo parte, Collider outro)
    {
        if (outro == null || jogadorPosition == null)
            return;
        if (outro.transform != jogadorPosition && !outro.transform.IsChildOf(jogadorPosition))
            return;
        JogadorEntrouNaHitbox(parte);
    }

    private void AoMorrer(EnemyHealth inimigo)
    {
        MudarEstado(State.idle);
    }

    private void OnDisable()
    {
        Died -= AoMorrer;
        PararMovimento();
        AtualizarAnimacao();
    }

    private void OnValidate()
    {
        velocidadeAndando = Mathf.Max(0.01f, velocidadeAndando);
        velocidadeAtaquePulo = Mathf.Max(velocidadeAndando, velocidadeAtaquePulo);
        distanciaParada = Mathf.Max(0f, distanciaParada);
        distanciaParaAtaquePulo = Mathf.Max(distanciaParada + 0.1f, distanciaParaAtaquePulo);
        distanciaChegadaAtaque = Mathf.Max(0.01f, distanciaChegadaAtaque);
        duracaoMaximaAtaque = Mathf.Max(0.01f, duracaoMaximaAtaque);
        intervaloDanoBarbatana = Mathf.Max(0.01f, intervaloDanoBarbatana);
        delayAfterAttack = Mathf.Max(0f, delayAfterAttack);
        velocidadeRotacao = Mathf.Max(0f, velocidadeRotacao);
    }
}
