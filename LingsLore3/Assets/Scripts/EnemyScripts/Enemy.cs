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

    [SerializeField, Min(0f)] private float distanciaAposJogador = 3f;

    [SerializeField, Min(0f)] private float velocidadeGiroPulo = 360f;

    [Min(0f)] public float delayAfterAttack = 2f;

    [Min(0f)] public float tempoDaAnimacaoCue = 1f;

    [Header("Ataque pelo chao")]
    [SerializeField] private Collider apoioPiso;
    [SerializeField, Min(0.01f)] private float profundidadeAtaqueChao = 4f;
    [SerializeField, Min(0.01f)] private float velocidadeVerticalAtaqueChao = 6f;
    [SerializeField, Min(0f)] private float tempoSubmerso = 1f;
    [SerializeField, Min(0f)] private float delayAntesDeEmergir = 1.5f;

    [SerializeField] private GameObject prefabMarcadorEmergencia;



    [Header("Dano ao jogador")]
    [SerializeField, Min(0f)] private float danoFrente = 25f;
    [SerializeField, Min(0f)] private float danoBarbatana = 10f;
    [SerializeField, Min(0.01f)] private float intervaloDanoBarbatana = 0.75f;
    [Tooltip("Conecte ao metodo que reduz a vida do jogador, usando o float dinamico.")]
    [SerializeField] private UnityEvent<float> aoCausarDano = new UnityEvent<float>();



    private enum State { idle, walking, jumpAttack, groundAttack, givingCue }
    [Header("Inclinacao visual")]

    [SerializeField] private Transform pivotVisual;

    [SerializeField] private float inclinacaoVisualZ;

    private Quaternion rotacaoVisualInicial;
    private float inclinacaoVisualX;




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
    private enum EtapaAtaqueChao { nenhuma, descendo, submerso, avisando, subindo }
    private EtapaAtaqueChao etapaAtaqueChao;
    private GameObject marcadorEmergencia;
    private Collider colisorPrincipal;
    private bool colisorPrincipalEstavaAtivo;
    private bool apoioPisoEstavaAtivo;
    private bool bodyEraCinematico;
    private bool gravidadeEstavaAtiva;
    private Vector3 posicaoAntesDeSubmergir;
    private Vector3 posicaoDeEmergencia;
    private float fimDaEtapaAtaqueChao;






    protected override void Awake()
    {
        if (pivotVisual != null)
            rotacaoVisualInicial = pivotVisual.localRotation;

        base.Awake();
        body = GetComponent<Rigidbody>();
        colisorPrincipal = GetComponent<Collider>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (jogadorPosition == null)
            Debug.LogWarning("Enemy: arraste o objeto principal do jogador para Jogador Position.", this);
        if (animator == null)
            Debug.LogWarning("Enemy: configure o Animator para exibir as animacoes.", this);
        if (apoioPiso == null || pivotVisual == null || colisorPrincipal == null)
            Debug.LogWarning("Enemy: configure Pivot Visual, Apoio Piso e um Collider no objeto principal para o ataque pelo chao.", this);
    }

    private void OnEnable()
    {
        frenteAcertouNesteAtaque = false;
        proximoDanoBarbatana = float.NegativeInfinity;
        fimDoDescanso = 0f;
        Died += AoMorrer;
        MudarEstado(State.walking);
    }

    private void LateUpdate()
    {
        if (pivotVisual == null)
            return;

        pivotVisual.localRotation =
            rotacaoVisualInicial *
            Quaternion.AngleAxis(inclinacaoVisualX, Vector3.right) *
            Quaternion.AngleAxis(inclinacaoVisualZ, Vector3.forward);
    }

    private void FixedUpdate()
    {
        //Debug.Log(estado);
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
                    SetAttack();
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

            case State.groundAttack:
                AtualizarAtaqueChao();
                break;
        }
    }

    private void SetAttack()
    {
        EscolherProximoAtaque();
        fimDaCue = Time.time + tempoDaAnimacaoCue;
        MudarEstado(State.givingCue);
    }
    private void EscolherProximoAtaque()
    {
        int a = Random.Range(0, 2);
        if (a < 0)
        {
            proximoAtaque = State.jumpAttack;
        }
        else
        {
            proximoAtaque = State.groundAttack;
        }
    }

    private void AtualizarCueState()
    {
        PararMovimento();
        if (Time.time > fimDaCue)
        {
            Debug.Log("Acaba cue");
            if (proximoAtaque == State.jumpAttack)
            {
                Debug.Log("Agora eh pulo");
                SetJumpAttack();
            }
            else if (proximoAtaque == State.groundAttack)
            {
                SetGroundAttack();
            }
        }
    }

    private Vector3 CalcularDestinoJumpAttack(
        Vector3 posicaoBoss,
        Vector3 posicaoJogador,
        float distanciaAposJogador)
    {
        posicaoJogador.y = posicaoBoss.y;

        Vector3 direcao = (posicaoJogador - posicaoBoss).normalized;

        return posicaoJogador +
            direcao * Mathf.Max(0f, distanciaAposJogador);
    }

    private void SetJumpAttack()
    {
        MudarEstado(State.jumpAttack);
        // Guarda uma posicao, em vez de uma referencia que acompanha o jogador.
        jumpAttackCoordinate = CalcularDestinoJumpAttack(
            body.position,
            jogadorPosition.position,
            distanciaAposJogador
        );
        jumpAttackCoordinate.y = body.position.y;
        frenteAcertouNesteAtaque = false;
        fimDoAtaque = Time.time + duracaoMaximaAtaque;
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

    private void SetGroundAttack()
    {
        if (pivotVisual == null || colisorPrincipal == null || apoioPiso == null)
        {
            fimDoDescanso = Time.time + delayAfterAttack;
            MudarEstado(State.idle);
            return;
        }

        frenteAcertouNesteAtaque = false;

        posicaoAntesDeSubmergir = body.position;
        colisorPrincipalEstavaAtivo = colisorPrincipal.enabled;
        apoioPisoEstavaAtivo = apoioPiso.enabled;
        bodyEraCinematico = body.isKinematic;
        gravidadeEstavaAtiva = body.useGravity;
        PararMovimento();
        body.isKinematic = true;
        body.useGravity = false;
        colisorPrincipal.enabled = false;
        apoioPiso.enabled = false;
        inclinacaoVisualX = 90f;
        etapaAtaqueChao = EtapaAtaqueChao.descendo;
        MudarEstado(State.groundAttack);
    }

    private void AtualizarAtaqueChao()
    {
        Vector3 destino;
        switch (etapaAtaqueChao)
        {
            case EtapaAtaqueChao.descendo:
                destino = posicaoAntesDeSubmergir - Vector3.up * profundidadeAtaqueChao;
                body.MovePosition(Vector3.MoveTowards(body.position, destino,
                    velocidadeVerticalAtaqueChao * Time.fixedDeltaTime));
                if (Vector3.Distance(body.position, destino) <=
                    velocidadeVerticalAtaqueChao * Time.fixedDeltaTime + 0.001f)
                {
                    etapaAtaqueChao = EtapaAtaqueChao.submerso;
                    fimDaEtapaAtaqueChao = Time.time + tempoSubmerso;
                }
                break;

            case EtapaAtaqueChao.submerso:
                if (Time.time >= fimDaEtapaAtaqueChao)
                {
                    // Captura a posicao apenas uma vez; o jogador pode desviar durante o aviso.
                    posicaoDeEmergencia = jogadorPosition.position;
                    posicaoDeEmergencia.y = posicaoAntesDeSubmergir.y;

                    Vector3 posicaoMarcador = new Vector3(
                        posicaoDeEmergencia.x,
                        jogadorPosition.position.y + 0.03f,
                        posicaoDeEmergencia.z
                    );

                    if (prefabMarcadorEmergencia != null)
                    {
                        marcadorEmergencia = Instantiate(
                            prefabMarcadorEmergencia,
                            posicaoMarcador,
                            Quaternion.identity
                        );
                        marcadorEmergencia
                            .GetComponent<MarcadorEmergencia>()
                            .Iniciar(delayAntesDeEmergir);
                    }

                    body.MovePosition(posicaoDeEmergencia - Vector3.up * profundidadeAtaqueChao);
                    etapaAtaqueChao = EtapaAtaqueChao.avisando;
                    fimDaEtapaAtaqueChao = Time.time + delayAntesDeEmergir;
                }
                break;

            case EtapaAtaqueChao.avisando:
                if (Time.time >= fimDaEtapaAtaqueChao)
                {
                    if (marcadorEmergencia != null)
                        Destroy(marcadorEmergencia);

                    marcadorEmergencia = null;
                    inclinacaoVisualX = 270f;
                    etapaAtaqueChao = EtapaAtaqueChao.subindo;
                }
                break;

            case EtapaAtaqueChao.subindo:
                destino = posicaoDeEmergencia;
                body.MovePosition(Vector3.MoveTowards(body.position, destino,
                    velocidadeVerticalAtaqueChao * Time.fixedDeltaTime));
                if (Vector3.Distance(body.position, destino) <=
                    velocidadeVerticalAtaqueChao * Time.fixedDeltaTime + 0.001f)
                {
                    body.position = destino;
                    FinalizarAtaqueChao(false);
                    fimDoDescanso = Time.time + delayAfterAttack;
                    MudarEstado(State.idle);
                }
                break;
        }
        AtualizarAnimacao();
    }

    private void FinalizarAtaqueChao(bool interrompido)
    {
        if (etapaAtaqueChao == EtapaAtaqueChao.nenhuma)
            return;

        if (interrompido)
            body.position = posicaoAntesDeSubmergir;

        inclinacaoVisualX = 0f;
        if (pivotVisual != null)
            pivotVisual.localRotation = rotacaoVisualInicial *
                Quaternion.AngleAxis(inclinacaoVisualZ, Vector3.forward);
        body.isKinematic = bodyEraCinematico;
        body.useGravity = gravidadeEstavaAtiva;
        colisorPrincipal.enabled = colisorPrincipalEstavaAtivo;
        apoioPiso.enabled = apoioPisoEstavaAtivo;
        etapaAtaqueChao = EtapaAtaqueChao.nenhuma;
    }
    
    /*
    // versao do atualizar ataque pulo com giro, mas que nao deu tao certo
    private void AtualizarAtaquePulo()
    {
        if (Time.time >= fimDoAtaque ||
            DistanciaHorizontal(jumpAttackCoordinate) <= distanciaChegadaAtaque + 0.01f)
        {
            inclinacaoVisualZ = 0f;

            fimDoDescanso = Time.time + delayAfterAttack;
            MudarEstado(State.idle);
            return;
        }

        inclinacaoVisualZ = Mathf.Repeat(
            inclinacaoVisualZ + velocidadeGiroPulo * Time.fixedDeltaTime,
            360f
        );

        MoverAte(
            jumpAttackCoordinate,
            velocidadeAtaquePulo,
            distanciaChegadaAtaque
        );

        AtualizarAnimacao();
    }
    */


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
        Quaternion rotacaoAtual = body.rotation;

        Vector3 direcaoDestino = destino - body.position;
        direcaoDestino.y = 0f;

        Vector3 frenteAtual = rotacaoAtual * Vector3.forward;
        frenteAtual.y = 0f;

        if (direcaoDestino.sqrMagnitude < 0.0001f ||
            frenteAtual.sqrMagnitude < 0.0001f)
            return;

        float diferencaY = Vector3.SignedAngle(
            frenteAtual,
            direcaoDestino,
            Vector3.up
        );

        float passoMaximo = Mathf.Max(0f, velocidadeRotacao)
            * Time.fixedDeltaTime;

        float passoY = Mathf.Clamp(
            diferencaY,
            -passoMaximo,
            passoMaximo
        );

        Quaternion giroY = Quaternion.AngleAxis(passoY, Vector3.up);

        body.MoveRotation(giroY * rotacaoAtual);
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
    bool movimentando = estado == State.jumpAttack || estado == State.groundAttack ||
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
                if ((estado != State.jumpAttack && estado != State.groundAttack) || frenteAcertouNesteAtaque)
                    return;
                dano = danoFrente;
                if (dano <= 0f)
                    return;
                frenteAcertouNesteAtaque = true;
                break;

            case HitboxParte.ParteCorpo.Barbatana:
                if (estado == State.groundAttack)
                    return;
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
        FinalizarAtaqueChao(true);
        MudarEstado(State.idle);
    }

    private void OnDisable()
    {
        FinalizarAtaqueChao(true);
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
        profundidadeAtaqueChao = Mathf.Max(0.01f, profundidadeAtaqueChao);
        velocidadeVerticalAtaqueChao = Mathf.Max(0.01f, velocidadeVerticalAtaqueChao);
        tempoSubmerso = Mathf.Max(0f, tempoSubmerso);
        delayAntesDeEmergir = Mathf.Max(0f, delayAntesDeEmergir);
    }
}
