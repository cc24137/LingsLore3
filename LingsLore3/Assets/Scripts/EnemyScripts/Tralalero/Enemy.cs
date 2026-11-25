using System.Collections.Generic;
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

    [Header("Ataque de cauda")]
    [SerializeField, Min(0f)] private float distanciaParaAtaqueCauda = 3f;
    [SerializeField, Min(0.01f)] private float duracaoAtaqueCauda = 1.6f;
    [Tooltip("Segundos apos iniciar a animacao ate fixar a posicao do jogador.")]
    [SerializeField, Min(0f)] private float tempoTravarAlvoCauda = 0.55f;
    [Tooltip("Intervalo, em segundos desde o inicio da animacao, em que a cauda causa dano.")]
    [SerializeField, Min(0f)] private float inicioDanoCauda = 0.9f;
    [SerializeField, Min(0f)] private float fimDanoCauda = 1.35f;
    [SerializeField, Min(0f)] private float velocidadeAvancoCauda = 2f;
    [SerializeField, Min(0f)] private float distanciaAvancoCauda = 0.7f;
    [SerializeField, Min(0f)] private float velocidadeRotacaoCauda = 720f;
    [SerializeField, Min(0f)] private float descansoAposAtaqueCauda = 2f;



    [Header("Dano ao jogador")]
    [SerializeField, Min(0f)] private float danoFrente = 25f;
    [SerializeField, Min(0f)] private float danoBarbatana = 10f;
    [SerializeField, Min(0f)] private float danoCauda = 25f;
    [SerializeField, Min(0.01f)] private float intervaloDanoBarbatana = 0.75f;
    [Tooltip("Conecte ao metodo que reduz a vida do jogador, usando o float dinamico.")]
    [SerializeField] private UnityEvent<float> aoCausarDano = new UnityEvent<float>();



    private enum State { idle, walking, jumpAttack, groundAttack, givingCue, tailAttack }
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
    private static readonly int IsTailAttacking = Animator.StringToHash("isTailAttacking");


    private Rigidbody body;
    private Vector3 jumpAttackCoordinate;
    private float fimDoAtaque;
    private float velocidadeDaAnimacao = 1f;
    private float EscalaDoTempoDaCauda => inicioDanoCauda > 0f
        ? BalanceamentoDificuldade.Atual.Aviso(inicioDanoCauda, 0.45f) / inicioDanoCauda
        : 1f;
    private bool tocouSomDaCauda;
    private float fimDoDescanso;
    private float fimDaCue;
    private float proximoDanoBarbatana;
    private bool frenteAcertouNesteAtaque;
    private readonly HashSet<PillarHealth> pilaresAtingidosPelaFrente = new HashSet<PillarHealth>();
    private readonly HashSet<PillarHealth> pilaresAtingidosPelaCauda = new HashSet<PillarHealth>();
    private readonly Dictionary<PillarHealth, float> proximoDanoNosPilares = new Dictionary<PillarHealth, float>();
    private State proximoAtaque;
    private State ultimoAtaqueSorteado;
    private int repeticoesDoAtaque;
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
    private float inicioAtaqueCauda;
    private bool alvoCaudaTravado;
    private bool caudaAcertouNesteAtaque;
    private Vector3 direcaoFrenteCauda;
    private Vector3 destinoAvancoCauda;






    protected override void Awake()
    {
        if (pivotVisual != null)
            rotacaoVisualInicial = pivotVisual.localRotation;

        base.Awake();
        body = GetComponent<Rigidbody>();
        colisorPrincipal = GetComponent<Collider>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            velocidadeDaAnimacao = animator.speed;

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
        pilaresAtingidosPelaFrente.Clear();
        proximoDanoBarbatana = float.NegativeInfinity;
        fimDoDescanso = 0f;
        ultimoAtaqueSorteado = State.idle;
        repeticoesDoAtaque = 0;
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
                float distanciaJogador = DistanciaHorizontal(jogadorPosition.position);
                bool pertoParaCauda = distanciaJogador <= distanciaParaAtaqueCauda;
                if (pertoParaCauda || distanciaJogador >= distanciaParaAtaquePulo)
                {
                    if (SortearAtaque(pertoParaCauda) == State.tailAttack)
                        SetTailAttack();
                    else
                    {
                        SetAttack();
                        AtualizarCueState();
                    }
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

            case State.tailAttack:
                AtualizarAtaqueCauda();
                break;
        }
    }

    private void SetAttack()
    {
        fimDaCue = Time.time + BalanceamentoDificuldade.Atual.Aviso(tempoDaAnimacaoCue);
        MudarEstado(State.givingCue);
    }
    private State SortearAtaque(bool pertoParaCauda)
    {
        State ataquePorDistancia = pertoParaCauda ? State.tailAttack : State.jumpAttack;
        bool podeSubmergir = pivotVisual != null && colisorPrincipal != null && apoioPiso != null;
        proximoAtaque = ataquePorDistancia;
        if (podeSubmergir)
        {
            float chanceDeSubmergir = ultimoAtaqueSorteado == State.groundAttack
                ? (repeticoesDoAtaque >= 2 ? 0f : 0.20f)
                : ultimoAtaqueSorteado == ataquePorDistancia
                    ? (repeticoesDoAtaque >= 2 ? 1f : 0.80f)
                    : 0.50f;
            if (Random.value < chanceDeSubmergir || chanceDeSubmergir >= 1f)
                proximoAtaque = State.groundAttack;
        }

        repeticoesDoAtaque = proximoAtaque == ultimoAtaqueSorteado ? repeticoesDoAtaque + 1 : 1;
        ultimoAtaqueSorteado = proximoAtaque;
        return proximoAtaque;
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

    private void SetTailAttack()
    {
        PararMovimento();
        inicioAtaqueCauda = Time.time;
        alvoCaudaTravado = false;
        caudaAcertouNesteAtaque = false;
        tocouSomDaCauda = false;
        pilaresAtingidosPelaCauda.Clear();
        MudarEstado(State.tailAttack);
    }

    private void AtualizarAtaqueCauda()
    {
        const bool girarPelaDireita = true; // Troque para false para girar pela esquerda.

        float tempoDecorrido = (Time.time - inicioAtaqueCauda) / EscalaDoTempoDaCauda;
        if (tempoDecorrido >= duracaoAtaqueCauda)
        {
            PararMovimento();
            fimDoDescanso = Time.time + BalanceamentoDificuldade.Atual.Descanso(descansoAposAtaqueCauda);
            MudarEstado(State.idle);
            return;
        }

        if (!tocouSomDaCauda && tempoDecorrido >= inicioDanoCauda && tempoDecorrido < fimDanoCauda)
        {
            tocouSomDaCauda = true;
            EfeitosSonoros.Tocar(Som.Bonk, body.position);
        }

        if (!alvoCaudaTravado)
        {
            PararMovimento();
            if (tempoDecorrido < tempoTravarAlvoCauda)
                return;

            Vector3 direcaoAoJogador = jogadorPosition.position - body.position;
            direcaoAoJogador.y = 0f;

            direcaoFrenteCauda = direcaoAoJogador.sqrMagnitude > 0.0001f
                ? -direcaoAoJogador.normalized
                : Vector3.ProjectOnPlane(
                    body.rotation * Vector3.forward, Vector3.up).normalized;

            destinoAvancoCauda =
                body.position + direcaoFrenteCauda * distanciaAvancoCauda;
            alvoCaudaTravado = true;
        }

        Quaternion rotacaoAtual = body.rotation;
        Vector3 frenteAtual = Vector3.ProjectOnPlane(
            rotacaoAtual * Vector3.forward, Vector3.up);

        if (frenteAtual.sqrMagnitude > 0.0001f &&
            direcaoFrenteCauda.sqrMagnitude > 0.0001f &&
            Vector3.Angle(frenteAtual, direcaoFrenteCauda) > 0.5f)
        {
            float diferencaY = Vector3.SignedAngle(
                frenteAtual, direcaoFrenteCauda, Vector3.up);

            // Repeat permite percorrer o lado escolhido mesmo quando ele
            // representa o caminho mais longo até a direção desejada.
            float anguloRestante = girarPelaDireita
                ? Mathf.Repeat(diferencaY, 360f)
                : Mathf.Repeat(-diferencaY, 360f);

            float passo = Mathf.Min(
                anguloRestante,
                velocidadeRotacaoCauda * Time.fixedDeltaTime / EscalaDoTempoDaCauda);

            body.MoveRotation(
                Quaternion.AngleAxis(girarPelaDireita ? passo : -passo, Vector3.up)
                * rotacaoAtual);
        }

        if (tempoDecorrido >= inicioDanoCauda &&
            tempoDecorrido < fimDanoCauda &&
            distanciaAvancoCauda > 0f &&
            velocidadeAvancoCauda > 0f &&
            !body.isKinematic)
        {
            Vector3 restante = destinoAvancoCauda - body.position;
            restante.y = 0f;

            float passo = Mathf.Min(
                restante.magnitude,
                velocidadeAvancoCauda * Time.fixedDeltaTime / EscalaDoTempoDaCauda);

            Vector3 movimento = restante.sqrMagnitude > 0.0001f
                ? restante.normalized * (passo / Time.fixedDeltaTime)
                : Vector3.zero;

            body.linearVelocity = new Vector3(
                movimento.x, body.linearVelocity.y, movimento.z);
        }
        else
        {
            PararMovimento();
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
        EfeitosSonoros.Tocar(Som.Bonk, body.position);
        MudarEstado(State.jumpAttack);
        // Guarda uma posicao, em vez de uma referencia que acompanha o jogador.
        jumpAttackCoordinate = CalcularDestinoJumpAttack(
            body.position,
            jogadorPosition.position,
            distanciaAposJogador
        );
        jumpAttackCoordinate.y = body.position.y;
        frenteAcertouNesteAtaque = false;
        pilaresAtingidosPelaFrente.Clear();
        fimDoAtaque = Time.time + duracaoMaximaAtaque;
    }

    private void AtualizarAtaquePulo()
    {
        if (Time.time >= fimDoAtaque ||
            DistanciaHorizontal(jumpAttackCoordinate) <= distanciaChegadaAtaque + 0.01f)
        {
            fimDoDescanso = Time.time + BalanceamentoDificuldade.Atual.Descanso(delayAfterAttack);
            MudarEstado(State.idle);
            return;
        }

        MoverAte(jumpAttackCoordinate, BalanceamentoDificuldade.Atual.Velocidade(velocidadeAtaquePulo), distanciaChegadaAtaque);
        AtualizarAnimacao();
    }

    private void SetGroundAttack()
    {
        if (pivotVisual == null || colisorPrincipal == null || apoioPiso == null)
        {
            fimDoDescanso = Time.time + BalanceamentoDificuldade.Atual.Descanso(delayAfterAttack);
            MudarEstado(State.idle);
            return;
        }

        frenteAcertouNesteAtaque = false;
        pilaresAtingidosPelaFrente.Clear();

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
                    BalanceamentoDificuldade.Atual.Velocidade(velocidadeVerticalAtaqueChao) * Time.fixedDeltaTime));
                if (Vector3.Distance(body.position, destino) <=
                    BalanceamentoDificuldade.Atual.Velocidade(velocidadeVerticalAtaqueChao) * Time.fixedDeltaTime + 0.001f)
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
                            .Iniciar(BalanceamentoDificuldade.Atual.Aviso(delayAntesDeEmergir, 0.65f));
                    }

                    body.MovePosition(posicaoDeEmergencia - Vector3.up * profundidadeAtaqueChao);
                    etapaAtaqueChao = EtapaAtaqueChao.avisando;
                    fimDaEtapaAtaqueChao = Time.time + BalanceamentoDificuldade.Atual.Aviso(delayAntesDeEmergir, 0.65f);
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
                    EfeitosSonoros.Tocar(Som.Bonk, posicaoDeEmergencia);
                }
                break;

            case EtapaAtaqueChao.subindo:
                destino = posicaoDeEmergencia;
                body.MovePosition(Vector3.MoveTowards(body.position, destino,
                    BalanceamentoDificuldade.Atual.Velocidade(velocidadeVerticalAtaqueChao) * Time.fixedDeltaTime));
                if (Vector3.Distance(body.position, destino) <=
                    BalanceamentoDificuldade.Atual.Velocidade(velocidadeVerticalAtaqueChao) * Time.fixedDeltaTime + 0.001f)
                {
                    body.position = destino;
                    FinalizarAtaqueChao(false);
                    fimDoDescanso = Time.time + BalanceamentoDificuldade.Atual.Descanso(delayAfterAttack);
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

            fimDoDescanso = Time.time + BalanceamentoDificuldade.Atual.Descanso(delayAfterAttack);
            MudarEstado(State.idle);
            return;
        }

        inclinacaoVisualZ = Mathf.Repeat(
            inclinacaoVisualZ + velocidadeGiroPulo * Time.fixedDeltaTime,
            360f
        );

        MoverAte(
            jumpAttackCoordinate,
            BalanceamentoDificuldade.Atual.Velocidade(velocidadeAtaquePulo),
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
    bool atacandoCauda = estado == State.tailAttack;

    float escala = atacandoCauda ? EscalaDoTempoDaCauda : preparando && tempoDaAnimacaoCue > 0f
        ? BalanceamentoDificuldade.Atual.Aviso(tempoDaAnimacaoCue) / tempoDaAnimacaoCue
        : 1f;
    animator.speed = velocidadeDaAnimacao / escala;
    animator.SetBool(IsIdle, !preparando && !movimentando && !atacandoCauda);
    animator.SetBool(IsWalking, movimentando);
    animator.SetBool(IsGivingCue, preparando);
    animator.SetBool(IsTailAttacking, atacandoCauda);
}

    // Mantem a assinatura usada pelo seu script de hitbox original.
    public void JogadorEntrouNaHitbox(HitboxParte.ParteCorpo parte)
    {
        if (jogadorPosition == null || !jogadorPosition.gameObject.activeInHierarchy || !HitboxAtiva(parte))
            return;

        float dano;
        switch (parte)
        {
            case HitboxParte.ParteCorpo.Frente:
                if (frenteAcertouNesteAtaque)
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

            case HitboxParte.ParteCorpo.Cauda:
                if (caudaAcertouNesteAtaque)
                    return;
                dano = danoCauda;
                if (dano <= 0f)
                    return;
                caudaAcertouNesteAtaque = true;
                break;

            default:
                return;
        }


        Debug.Log("Invocando o aoCausarDano");
        if (BalanceamentoDificuldade.Impossivel &&
            jogadorPosition.GetComponentInParent<PlayerResources>() is PlayerResources recursos && recursos.EvitouDanoDeChefe())
            return;

        aoCausarDano.Invoke(dano * BalanceamentoDificuldade.Atual.Dano);
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

    internal bool HitboxAtiva(HitboxParte.ParteCorpo parte)
    {
        if (!isActiveAndEnabled || IsDead)
            return false;

        float tempoCauda = (Time.time - inicioAtaqueCauda) / EscalaDoTempoDaCauda;
        return parte switch
        {
            HitboxParte.ParteCorpo.Frente => estado == State.jumpAttack || estado == State.groundAttack,
            HitboxParte.ParteCorpo.Barbatana => estado != State.groundAttack,
            HitboxParte.ParteCorpo.Cauda => estado == State.tailAttack && alvoCaudaTravado &&
                tempoCauda >= inicioDanoCauda && tempoCauda < fimDanoCauda,
            _ => false
        };
    }

    internal void AtingirPilar(HitboxParte.ParteCorpo parte, PillarHealth pilar, Vector3 ponto)
    {
        switch (parte)
        {
            case HitboxParte.ParteCorpo.Frente:
                if (!pilaresAtingidosPelaFrente.Contains(pilar) && pilar.TakeDamage(danoFrente, ponto))
                    pilaresAtingidosPelaFrente.Add(pilar);
                break;
            case HitboxParte.ParteCorpo.Cauda:
                if (!pilaresAtingidosPelaCauda.Contains(pilar) && pilar.TakeDamage(danoCauda, ponto))
                    pilaresAtingidosPelaCauda.Add(pilar);
                break;
            case HitboxParte.ParteCorpo.Barbatana:
                if ((!proximoDanoNosPilares.TryGetValue(pilar, out float proximo) || Time.time >= proximo) &&
                    pilar.TakeDamage(danoBarbatana, ponto))
                    proximoDanoNosPilares[pilar] = Time.time + intervaloDanoBarbatana;
                break;
        }
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
        distanciaParaAtaqueCauda = Mathf.Clamp(distanciaParaAtaqueCauda,
            distanciaParada + 0.05f, distanciaParaAtaquePulo - 0.05f);
        duracaoAtaqueCauda = Mathf.Max(0.01f, duracaoAtaqueCauda);
        tempoTravarAlvoCauda = Mathf.Clamp(tempoTravarAlvoCauda, 0f, duracaoAtaqueCauda);
        inicioDanoCauda = Mathf.Clamp(inicioDanoCauda, tempoTravarAlvoCauda, duracaoAtaqueCauda);
        fimDanoCauda = Mathf.Clamp(fimDanoCauda, inicioDanoCauda, duracaoAtaqueCauda);
        velocidadeAvancoCauda = Mathf.Max(0f, velocidadeAvancoCauda);
        distanciaAvancoCauda = Mathf.Max(0f, distanciaAvancoCauda);
        velocidadeRotacaoCauda = Mathf.Max(0f, velocidadeRotacaoCauda);
        descansoAposAtaqueCauda = Mathf.Max(0f, descansoAposAtaqueCauda);
    }
}
