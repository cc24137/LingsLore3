using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed partial class BossNetanyahu : MonoBehaviour
{
    private enum Estado { Descansando, Aproximando, Preparando, Atacando, Encerrado, Transicao, Atordoado }
    private enum Ataque
    {
        Nenhum, Bombas, Combo, Investida, Pisada, BombaIncendiaria, ComandarMinis, ModoAviao,
        Rasante, Rajada, GiroTralalero, BombaMassiva, Laser, RajadaRobo, MinisRobo, ComboRobo, GiroTung
    }

    [Header("Arena")]
    [SerializeField, Tooltip("Arraste o Ling da cena para definir o alvo do chefe.")] private PlayerResources ling;
    [SerializeField, Tooltip("Collider do piso circular da arena deste chefe.")] private Collider piso;
    [SerializeField] private LayerMask obstaculos;
    [SerializeField] private Shader shaderDosAvisos;
    [SerializeField] private bool mostrarHudDeTeste;

    [Header("Bombas")]
    [SerializeField] private Bomba prefabBomba;
    [SerializeField, Min(0.1f)] private float distanciaParaBombas = 13f;
    [SerializeField, Min(0f)] private float tempoDeRecuo = 0.65f;
    [SerializeField, Min(0f)] private float velocidadeDeRecuo = 3f;

    [Header("Fase humana - a partir de 50% de vida")]
    [SerializeField] private PocaDeFogo prefabPocaDeFogo;
    [SerializeField, Min(0f)] private float duracaoDaTransicao = 1.4f;
    [FormerlySerializedAs("multiplicadorDeTamanhoNaFase2")]
    [SerializeField, Min(1f)] private float multiplicadorDeTamanhoEnfurecido = 1.15f;

    [Header("Minis do Tralalero")]
    [SerializeField] private MiniTralalero prefabMiniTralalero;
    [SerializeField, Min(1)] private int quantidadeDeMinis = 4;
    [SerializeField, Min(0.1f)] private float distanciaParaInvocar = 16f;
    [SerializeField, Min(0.1f)] private float raioDaInvocacao = 4.25f;
    [SerializeField, Min(0f)] private float avisoDaInvocacao = 0.75f;
    [SerializeField, Min(0f)] private float tempoAntesDaOrdem = 1.3f;
    [SerializeField, Min(0f)] private float avisoDaOrdem = 0.65f;
    [SerializeField, Min(0f)] private float intervaloEntreInvocacoes = 8f;

    [Header("Corrida de aproximação")]
    [SerializeField, Min(0.1f)] private float velocidadeInicialDaCorrida = 7f;
    [SerializeField, Min(0.1f)] private float velocidadeMaximaDaCorrida = 13f;
    [SerializeField, Min(0.1f)] private float aceleracaoDaCorrida = 5f;
    [SerializeField, Min(0.1f)] private float tempoMaximoDeCorrida = 7f;

    [Header("Dash e combo")]
    [SerializeField, Min(0.1f)] private float velocidadeDoDash = 28f;
    [SerializeField, Min(0.1f)] private float alcanceDoDash = 7f;
    [SerializeField, Min(0.1f)] private float alcanceDoCombo = 3.2f;
    [SerializeField, Min(0.1f)] private float avisoDoPrimeiroGolpe = 0.6f;
    [SerializeField, Min(0.1f)] private float avisoDoGolpe = 0.3f;
    [SerializeField, Min(0f)] private float danoDoGolpe = 8f;
    [SerializeField, Min(0f)] private float danoDoUltimoGolpe = 15f;

    [Header("Investida")]
    [SerializeField, Min(0.1f)] private float distanciaParaInvestida = 11f;
    [SerializeField, Min(0.1f)] private float avisoDaInvestida = 0.65f;
    [SerializeField, Min(1f)] private float multiplicadorDeLarguraDaInvestida = 1.5f;
    [SerializeField, Min(0.1f)] private float velocidadeDaInvestida = 32f;
    [SerializeField, Min(0f)] private float danoDaInvestida = 20f;

    [Header("Pisada")]
    [SerializeField, Min(0.1f)] private float distanciaParaPisada = 4.5f;
    [SerializeField, Min(0.1f)] private float avisoDaPisada = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float fracaoAteBordaMaisDistante = 0.95f;
    [SerializeField, Min(0.1f)] private float velocidadeDaOnda = 18f;
    [SerializeField, Min(0.1f)] private float alturaDaOnda = 0.45f;
    [SerializeField, Min(0.1f)] private float larguraDaOnda = 0.55f;
    [SerializeField, Min(0f)] private float danoDaPisada = 16f;

    [Header("Intervalo entre ataques")]
    [SerializeField, Min(0.1f)] private float recuperacao = 1.1f;

    [Header("Estado atual (controlado pelo código)")]
    [SerializeField] private Estado estado;
    [SerializeField] private Ataque ataqueEscolhido;

    private readonly WaitForFixedUpdate passoFisico = new WaitForFixedUpdate();
    private readonly List<MiniTralalero> minis = new List<MiniTralalero>();
    private EnemyHealth vida;
    private Rigidbody corpo;
    private Rigidbody corpoDoLing;
    private CapsuleCollider colisaoDoBoss;
    private CapsuleCollider colisaoDoLing;
    private Material materialDosAvisos;
    private GameObject efeitosDoAtaque;
    private PocaDeFogo pocaAtiva;
    private Vector3 escalaInicial;
    private float alturaInicialDaBaseAoPivo;
    private float multiplicadorAtual = 1f;
    private bool enfurecido;
    private float proximaInvocacao;
    private readonly List<Ataque> ataquesDisponiveis = new List<Ataque>(5);
    private Ataque ultimoAtaqueNoChao;
    private Ataque ultimoAtaqueNoAr;
    private bool emInvestida;
    private bool ignorandoColisaoComLing;

    private const float FolgaDeColisao = 0.04f;
    private const float IntervaloAtePisada = 0.15f;
    private bool LutaEncerrada => estado == Estado.Encerrado;
    private bool FaseRobo => vida.CurrentBar == 2;
    private bool NaMetadeDaVida => vida.CurrentHealth <= vida.MaxHealth * 0.5f;
    private float RaioDoBoss => colisaoDoBoss.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
    private float DistanciaDeParadaDoCombo => RaioDoBoss + colisaoDoLing.bounds.extents.x + 0.35f;

    private static readonly Color Amarelo = new Color(1f, 0.72f, 0.12f);
    private static readonly Color Vermelho = new Color(1f, 0.22f, 0.06f);
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        vida = GetComponent<EnemyHealth>();
        corpo = GetComponent<Rigidbody>();
        colisaoDoBoss = GetComponent<CapsuleCollider>();
        if (ling == null || !ling.TryGetComponent(out colisaoDoLing) || !ling.TryGetComponent(out corpoDoLing)
            || piso == null || shaderDosAvisos == null)
        {
            Debug.LogError("Configure Ling, o piso e o shader de avisos do boss.", this);
            enabled = false;
            return;
        }

        materialDosAvisos = new Material(shaderDosAvisos);
        escalaInicial = transform.localScale;
        float escalaVertical = transform.lossyScale.y;
        float meiaAlturaDaCapsula = Mathf.Max(colisaoDoBoss.height * Mathf.Abs(escalaVertical) * 0.5f, RaioDoBoss);
        alturaInicialDaBaseAoPivo = meiaAlturaDaCapsula - colisaoDoBoss.center.y * escalaVertical;
        Vector3 posicao = corpo.position;
        posicao.y = piso.bounds.max.y + alturaInicialDaBaseAoPivo;
        corpo.position = posicao;
        PrepararAnimacao();
        PrepararRobo();
    }

    private void OnEnable()
    {
        vida.Died += AoDerrotarBoss;
        vida.HealthBarChanged += AoTrocarBarra;
        ling.Died += EncerrarLuta;
        ConectarAcertosNoAviao();
        if (!LutaEncerrada || (!vida.IsDead && !ling.IsDead))
        {
            enfurecido = !FaseRobo && NaMetadeDaVida;
            AjustarTamanho(FaseRobo || enfurecido ? multiplicadorDeTamanhoEnfurecido : 1f);
            ColorirFormaHumana(enfurecido ? 1f : 0f);
            ExibirForma(FaseRobo);
            colisaoDoBoss.enabled = true;
            estado = Estado.Descansando;
            StartCoroutine(Combater(0.9f));
        }
    }

    private void FixedUpdate()
    {
        if (!LutaEncerrada && !vida.IsDead && !FaseRobo && enfurecido != NaMetadeDaVida)
        {
            enfurecido = NaMetadeDaVida;
            ReiniciarCombate(enfurecido ? duracaoDaTransicao : 0.9f, transicao: enfurecido);
        }

        if (ignorandoColisaoComLing && !emInvestida && !SobrepostoAoLing())
            IgnorarColisaoComLing(false);

        if (etapaDoVoo == EtapaDoVoo.Cruzeiro)
            AtualizarOrbita();
    }

    private IEnumerator Combater(float espera)
    {
        ataqueEscolhido = Ataque.Nenhum;
        if (estado == Estado.Transicao && FaseRobo)
            yield return RenascerComoRobo();
        else if (estado == Estado.Transicao)
            yield return CrescerNaTransicao(espera);
        else
            yield return new WaitForSeconds(espera);
        estado = Estado.Descansando;
        while (!LutaEncerrada)
        {
            if (FaseRobo)
                yield return ExecutarRodadaRobo();
            else
                yield return ExecutarRodadaHumana();
        }
    }

    private IEnumerator ExecutarRodadaHumana()
    {
        ataquesDisponiveis.Clear();
        if (prefabBomba != null)
            ataquesDisponiveis.Add(Ataque.Bombas);
        ataquesDisponiveis.Add(Ataque.Combo);
        ataquesDisponiveis.Add(Ataque.Investida);
        ataquesDisponiveis.Add(Ataque.Pisada);
        if (modeloTralaleroDoGiro != null)
            ataquesDisponiveis.Add(Ataque.GiroTralalero);
        if (enfurecido)
        {
            if (prefabAviao != null && Time.time >= podeVoarApos)
                ataquesDisponiveis.Add(Ataque.ModoAviao);
            if (PodeInvocarMinis(prefabMiniTralalero))
                ataquesDisponiveis.Add(Ataque.ComandarMinis);
            if (PodeUsarBombaMassiva)
                ataquesDisponiveis.Add(Ataque.BombaMassiva);
            if (prefabBomba != null && prefabPocaDeFogo != null)
                ataquesDisponiveis.Add(Ataque.BombaIncendiaria);
        }

        ataqueEscolhido = SortearAtaque(ref ultimoAtaqueNoChao);
        switch (ataqueEscolhido)
        {
            case Ataque.ModoAviao:
                yield return ExecutarModoAviao();
                break;
            case Ataque.ComandarMinis:
                yield return ExecutarQuandoPerto(ComandarMinis(prefabMiniTralalero), distanciaParaInvocar);
                break;
            case Ataque.Combo:
                float distanciaDeParada = DistanciaDeParadaDoCombo;
                yield return ExecutarQuandoPerto(ExecutarCombo(), alcanceDoDash + distanciaDeParada, distanciaDeParada);
                break;
            case Ataque.Investida:
                yield return ExecutarQuandoPerto(ExecutarInvestida(), distanciaParaInvestida);
                break;
            case Ataque.BombaMassiva:
                yield return ExecutarBombaMassiva();
                yield return DescansarAposAtaque();
                break;
            case Ataque.GiroTralalero:
                yield return ExecutarQuandoPerto(ExecutarGiroComModelo(), raioDoGiro);
                break;
            case Ataque.Bombas:
            case Ataque.BombaIncendiaria:
                float margemParaRecuo = tempoDeRecuo * velocidadeDeRecuo;
                float distanciaAntesDoRecuo = Mathf.Max(distanciaParaBombas - margemParaRecuo, distanciaParaBombas * 0.5f);
                yield return ExecutarQuandoPerto(LancarBombas(ataqueEscolhido == Ataque.BombaIncendiaria), distanciaAntesDoRecuo);
                break;
            case Ataque.Pisada:
                yield return ExecutarQuandoPerto(ExecutarPisada(), distanciaParaPisada);
                break;
        }
    }

    private Ataque SortearAtaque(ref Ataque ultimo)
    {
        if (ataquesDisponiveis.Count > 1)
            ataquesDisponiveis.Remove(ultimo);

        ultimo = ataquesDisponiveis[Random.Range(0, ataquesDisponiveis.Count)];
        return ultimo;
    }

    private bool PodeInvocarMinis(MiniTralalero prefab)
    {
        minis.RemoveAll(mini => mini == null || !mini.EstaAtivo);
        return prefab != null && minis.Count == 0 && Time.time >= proximaInvocacao;
    }

    private IEnumerator ComandarMinis(MiniTralalero prefab, bool roboticos = false)
    {
        List<Vector3> pontos = EscolherPontosDosMinis();
        if (pontos.Count == 0)
        {
            proximaInvocacao = Time.time + recuperacao * 2f;
            yield break;
        }

        estado = Estado.Preparando;
        if (etapaDoVoo == EtapaDoVoo.NoChao)
            corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());
        CriarEfeitos("Aviso de invocação");
        foreach (Vector3 ponto in pontos)
            MarcarArea(ponto, 0.85f, Vector3.forward, 180f);

        momentoDaInvocacaoVisual = Time.time + avisoDaInvocacao;
        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(avisoDaInvocacao));
        LimparEfeitos();

        momentoDaInvocacaoVisual = Time.time;
        estado = Estado.Atacando;
        proximaInvocacao = Time.time + intervaloEntreInvocacoes;
        Bounds arena = piso.bounds;
        foreach (Vector3 ponto in pontos)
        {
            Vector3 direcao = VetorHorizontal(corpoDoLing.position - ponto);
            Quaternion rotacao = direcao.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direcao) : corpo.rotation;
            MiniTralalero mini = Instantiate(prefab, ponto, rotacao);
            if (roboticos)
                MontarMiniRobo(mini.transform);
            minis.Add(mini);
            mini.Iniciar(ling, corpoDoLing, colisaoDoLing, obstaculos, arena, minis);
        }

        yield return new WaitForSeconds(tempoAntesDaOrdem);
        estado = Estado.Preparando;
        CriarEfeitos("Ordem de ataque dos minis");
        foreach (MiniTralalero mini in minis)
        {
            if (mini == null || !mini.EstaAtivo)
                continue;

            mini.PrepararInvestida();
            Vector3 ponto = mini.transform.position;
            ponto.y = arena.max.y;
            MarcarArea(ponto, 0.85f, Vector3.forward, 180f);
        }

        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(avisoDaOrdem));
        estado = Estado.Atacando;
        foreach (MiniTralalero mini in minis)
        {
            if (mini != null && mini.EstaAtivo)
                mini.Investir();
        }

        LimparEfeitos();
    }

    private List<Vector3> EscolherPontosDosMinis()
    {
        const int tentativasPorSetor = 6;
        var pontos = new List<Vector3>(quantidadeDeMinis);
        int mascara = obstaculos.value | (1 << gameObject.layer);
        float alturaDoPiso = piso.bounds.max.y;
        for (int i = 0; i < quantidadeDeMinis * tentativasPorSetor && pontos.Count < quantidadeDeMinis; i++)
        {
            float setor = i % quantidadeDeMinis + i / quantidadeDeMinis / (float)tentativasPorSetor;
            Vector3 direcao = Quaternion.Euler(0f, setor * 360f / quantidadeDeMinis, 0f) * transform.forward;
            Vector3 ponto = LimitarAoPiso(corpo.position + direcao * raioDaInvocacao, 1f);
            ponto.y = alturaDoPiso;
            if (VetorHorizontal(ponto - corpoDoLing.position).sqrMagnitude < 4f
                || Physics.CheckSphere(ponto + Vector3.up * 0.65f, 0.8f, mascara, QueryTriggerInteraction.Ignore)
                || pontos.Exists(anterior => (anterior - ponto).sqrMagnitude < 2.25f))
                continue;

            pontos.Add(ponto);
        }

        return pontos;
    }

    private IEnumerator CrescerNaTransicao(float duracao)
    {
        float tamanhoInicial = multiplicadorAtual;
        float inicio = Time.time;
        while (Time.time - inicio < duracao)
        {
            yield return passoFisico;
            float progresso = (Time.time - inicio) / duracao;
            AjustarTamanho(Mathf.SmoothStep(tamanhoInicial, multiplicadorDeTamanhoEnfurecido, progresso));
            ColorirFormaHumana(Mathf.SmoothStep(0f, 1f, progresso));
        }

        AjustarTamanho(multiplicadorDeTamanhoEnfurecido);
        ColorirFormaHumana(1f);
    }

    private void AjustarTamanho(float multiplicador)
    {
        if (multiplicador == multiplicadorAtual)
            return;

        Vector3 posicao = corpo.position;
        posicao.y += alturaInicialDaBaseAoPivo * (multiplicador - multiplicadorAtual);
        transform.localScale = escalaInicial * multiplicador;
        corpo.position = LimitarAoPiso(posicao, RaioDoBoss + 0.1f);
        multiplicadorAtual = multiplicador;
    }

    private IEnumerator ExecutarQuandoPerto(IEnumerator ataque, float distanciaMaxima, float distanciaDeParadaDoDash = 0f)
    {
        estado = Estado.Aproximando;
        float prazo = Time.fixedTime + tempoMaximoDeCorrida;
        float velocidade = velocidadeInicialDaCorrida;
        float ladoDoDesvio = 0f;
        while (!PodeIniciarAtaque(distanciaMaxima, distanciaDeParadaDoDash))
        {
            if (Time.fixedTime >= prazo)
                break;

            Vector3 ateLing = VetorHorizontal(corpoDoLing.position - corpo.position);
            Vector3 direcao = DirecaoDaCorrida(ateLing.normalized, ateLing.magnitude, ref ladoDoDesvio);
            corpo.MoveRotation(Quaternion.LookRotation(direcao));
            Mover(corpo.position + direcao * (velocidade * Time.fixedDeltaTime));
            velocidade = Mathf.MoveTowards(velocidade, velocidadeMaximaDaCorrida, aceleracaoDaCorrida * Time.fixedDeltaTime);
            yield return passoFisico;
        }

        if (PodeIniciarAtaque(distanciaMaxima, distanciaDeParadaDoDash))
            yield return ataque;

        if (LutaEncerrada)
            yield break;

        yield return DescansarAposAtaque();
    }

    private WaitForSeconds DescansarAposAtaque()
    {
        float tempo = BalanceamentoDificuldade.Atual.Descanso(recuperacao);
        if (estado == Estado.Atacando && ataqueEscolhido == Ataque.Pisada)
            tempo *= 0.5f;

        estado = Estado.Descansando;
        ataqueEscolhido = Ataque.Nenhum;
        return new WaitForSeconds(tempo);
    }

    private bool PodeIniciarAtaque(float distanciaMaxima, float distanciaDeParadaDoDash = 0f)
    {
        Vector3 ateLing = VetorHorizontal(corpoDoLing.position - corpo.position);
        float distancia = ateLing.magnitude;
        if (distancia > distanciaMaxima)
            return false;
        if (distanciaDeParadaDoDash <= 0f || distancia <= distanciaDeParadaDoDash)
            return true;

        float percurso = distancia - distanciaDeParadaDoDash;
        return DistanciaLivre(corpo.position, ateLing / distancia, percurso, obstaculos) >= percurso - 0.01f;
    }

    private IEnumerator LancarBombas(bool incendiaria)
    {
        if (prefabBomba == null || (incendiaria && prefabPocaDeFogo == null))
        {
            Debug.LogError("Configure os prefabs da bomba e da poça de fogo do Big Yahu.", this);
            yield break;
        }

        estado = Estado.Preparando;
        for (float tempo = 0f; tempo < tempoDeRecuo; tempo += Time.fixedDeltaTime)
        {
            yield return passoFisico;
            float distanciaAoLing = VetorHorizontal(corpoDoLing.position - corpo.position).magnitude;
            float passo = Mathf.Min(velocidadeDeRecuo * Time.fixedDeltaTime, distanciaParaBombas - distanciaAoLing);
            if (passo <= 0f)
                break;

            Vector3 frente = DirecaoDoLing();
            corpo.MoveRotation(Quaternion.LookRotation(frente));
            Mover(corpo.position - frente * passo);
        }

        yield return passoFisico;
        if (!PodeIniciarAtaque(distanciaParaBombas))
            yield break;

        yield return ArremessarBombas(incendiaria);
        if (incendiaria && !LutaEncerrada)
            yield return ExecutarPisadaEmSeguida();
    }

    private IEnumerator ArremessarBombas(bool incendiaria)
    {
        float duracaoDoArremesso = BalanceamentoDificuldade.Atual.Aviso(0.34f, 0.25f);
        estado = Estado.Preparando;
        if (aviao == null)
            corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());

        CriarEfeitos("Bombas do boss");
        Vector3 origem = aviao != null ? aviao.transform.position : corpo.position + Vector3.up * (0.65f * transform.lossyScale.y);
        var bombas = new Bomba[incendiaria ? 1 : 3];
        bombaNaMao = bombas[0] = Instantiate(prefabBomba, origem, Quaternion.identity, efeitosDoAtaque.transform);
        if (incendiaria)
            bombaNaMao.Redimensionar(fatorVisual: 1.5f, fatorDoRaio: 3f);
        float inicio = Time.time;
        for (float tempo = 0f; tempo < duracaoDoArremesso; tempo = Time.time - inicio)
        {
            preparacaoVisualDoAtaque = tempo / duracaoDoArremesso;
            yield return null;
        }

        preparacaoVisualDoAtaque = 1f;
        origem = aviao != null ? aviao.transform.position : corpo.position + Vector3.up * (0.65f * transform.lossyScale.y);
        Vector3 soltura = maoDireita != null ? bombaNaMao.transform.position : origem;
        bombaNaMao.transform.position = soltura;
        bombaNaMao = null;
        Vector3 ateLing = VetorHorizontal(corpoDoLing.position - origem);
        float distancia = Mathf.Max(ateLing.magnitude, 0.01f);
        float abertura = Mathf.Asin(Mathf.Clamp(2.15f * prefabBomba.RaioDaExplosao / distancia, 0.28f, 0.85f)) * Mathf.Rad2Deg;
        var alvosDaSalva = incendiaria ? null : new HashSet<PlayerResources>();
        var pilaresDaSalva = incendiaria ? null : new HashSet<PillarHealth>();
        estado = Estado.Atacando;

        for (int i = 0; i < bombas.Length; i++)
        {
            float angulo = (i - (bombas.Length - 1) * 0.5f) * abertura;
            Vector3 deslocamento = Quaternion.Euler(0f, angulo, 0f) * ateLing;
            Vector3 destino = LimitarAoPiso(origem + deslocamento, 0f);
            destino.y = piso.bounds.max.y;
            if (i > 0)
                bombas[i] = Instantiate(prefabBomba, soltura, Quaternion.identity, efeitosDoAtaque.transform);
            if (incendiaria)
                bombas[i].Explodiu += CriarPocaDeFogo;
            bombas[i].Lancar(destino, ling, obstaculos, alvosDaSalva, pilaresDaSalva);
        }

        foreach (Bomba bomba in bombas)
        {
            while (bomba != null)
                yield return null;
        }

        LimparEfeitos();
    }

    private void CriarPocaDeFogo(Vector3 destino)
    {
        LimparPocaDeFogo();
        pocaAtiva = Instantiate(prefabPocaDeFogo, destino, Quaternion.identity);
        pocaAtiva.Iniciar(ling);
    }

    private void LimparPocaDeFogo()
    {
        if (pocaAtiva == null)
            return;

        pocaAtiva.gameObject.SetActive(false);
        Destroy(pocaAtiva.gameObject);
        pocaAtiva = null;
    }

    private void LimparMinis()
    {
        foreach (MiniTralalero mini in minis)
        {
            if (mini == null)
                continue;

            mini.gameObject.SetActive(false);
            Destroy(mini.gameObject);
        }

        minis.Clear();
        proximaInvocacao = 0f;
    }

    private float AvisoDoSoco(int golpe)
    {
        var balanceamento = BalanceamentoDificuldade.Atual;
        if (golpe != 0)
            return balanceamento.Aviso(avisoDoGolpe + (golpe == 2 ? 0.2f : 0f), 0.68f);

        bool dificuldadeAlta = SelecaoDificuldade.DificuldadeEscolhida is Dificuldade.Dificil or Dificuldade.Impossivel;
        return balanceamento.Aviso(avisoDoPrimeiroGolpe * (dificuldadeAlta ? 0.83f : 1f));
    }

    private IEnumerator ExecutarCombo()
    {
        golpeDoCombo = -1;
        estado = Estado.Atacando;
        Vector3 frente = DirecaoDoLing();
        float distanciaAoLing = VetorHorizontal(corpoDoLing.position - corpo.position).magnitude;
        float distanciaDoDash = Mathf.Clamp(distanciaAoLing - DistanciaDeParadaDoCombo, 0f, alcanceDoDash);
        Vector3 destino = corpo.position + frente * distanciaDoDash;

        while ((destino - corpo.position).sqrMagnitude > 0.0025f)
        {
            yield return passoFisico;
            corpo.MoveRotation(Quaternion.LookRotation(frente));
            Vector3 proximo = Vector3.MoveTowards(corpo.position, destino, BalanceamentoDificuldade.Atual.Velocidade(velocidadeDoDash) * Time.fixedDeltaTime);
            if (!Mover(proximo))
                break;
            if (proximo == destino)
                break;
        }

        yield return passoFisico;
        for (int i = 0; i < 3; i++)
        {
            golpeDoCombo = i;
            bool ultimo = i == 2;
            float alcance = alcanceDoCombo * (ultimo ? 1.14f : 1f);
            float semiAngulo = ultimo ? 70f : 50f;
            frente = DirecaoDoLing();
            corpo.rotation = Quaternion.LookRotation(frente);
            Vector3 origem = corpo.position;
            origem.y = piso.bounds.max.y;
            estado = Estado.Preparando;
            CriarEfeitos("Golpe do boss");
            LineRenderer marcador = MarcarArea(origem, alcance, frente, semiAngulo);

            yield return new WaitForSeconds(AvisoDoSoco(i));
            estado = Estado.Atacando;
            MostrarImpacto(marcador);
            EfeitosSonoros.Tocar(Som.SocoDoYahu, origem);
            float dano = ultimo ? danoDoUltimoGolpe : danoDoGolpe;
            TentarAtingirLing(origem, alcance, frente, semiAngulo, dano);
            foreach (PillarHealth pilar in PillarHealth.ActivePillars)
            {
                if (pilar.TryGetAreaHit(origem, alcance, 1.5f, obstaculos, out Vector3 ponto) &&
                    Vector3.Angle(frente, VetorHorizontal(ponto - origem)) <= semiAngulo)
                    pilar.TakeDamage(dano, ponto);
            }
            yield return new WaitForSeconds(0.16f);
            LimparEfeitos();
        }
    }

    private Vector3 DirecaoDaCorrida(Vector3 frente, float distanciaAoLing, ref float ladoDoDesvio)
    {
        if (!VarrerCapsula(corpo.position, frente, Mathf.Min(distanciaAoLing, 3f), obstaculos, out RaycastHit obstaculo))
        {
            ladoDoDesvio = 0f;
            return frente;
        }

        Vector3 normal = VetorHorizontal(obstaculo.normal).normalized;
        if (normal.sqrMagnitude < 0.001f)
            return frente;

        Vector3 tangente = Vector3.Cross(Vector3.up, normal);
        if (ladoDoDesvio == 0f)
            ladoDoDesvio = Vector3.Dot(tangente, frente) >= 0f ? 1f : -1f;

        return (tangente * ladoDoDesvio + normal * 0.25f).normalized;
    }

    private IEnumerator ExecutarInvestida()
    {
        const float margemDaParede = 1.5f;
        estado = Estado.Preparando;
        Vector3 origem = corpo.position;
        Vector3 frente = DirecaoDoLing();
        float distancia = DistanciaAteBorda(origem, frente, margemDaParede);
        if (distancia < 3f)
        {
            // Se a mira sair pela borda mais próxima, a investida segue pelo centro.
            frente = VetorHorizontal(piso.bounds.center - origem).normalized;
            distancia = DistanciaAteBorda(origem, frente, margemDaParede);
        }
        corpo.rotation = Quaternion.LookRotation(frente);
        ObterExtremosDaCapsula(origem, RaioDoBoss, out Vector3 inferior, out Vector3 superior);
        foreach (RaycastHit hit in Physics.CapsuleCastAll(inferior, superior, RaioDoBoss, frente,
            distancia + margemDaParede, obstaculos, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != piso && hit.collider.GetComponentInParent<PillarHealth>() == null)
                distancia = Mathf.Min(distancia, Mathf.Max(0f, hit.distance - margemDaParede));
        }
        if (distancia <= 0f)
            yield break;

        CriarEfeitos("Investida do boss");
        Vector3 inicioDaFaixa = new Vector3(origem.x, piso.bounds.max.y, origem.z);
        float raioDaInvestida = RaioDoBoss * multiplicadorDeLarguraDaInvestida;
        LineRenderer faixa = MarcarTrajeto(inicioDaFaixa, frente, distancia, raioDaInvestida);
        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(avisoDaInvestida));

        estado = Estado.Atacando;
        MostrarImpacto(faixa);
        EfeitosSonoros.Tocar(Som.Bonk, corpo.position);
        emInvestida = true;
        IgnorarColisaoComLing(true);
        bool passouPeloLing = false;
        var pilaresAtingidos = new HashSet<PillarHealth>();
        for (float avancou = 0f; avancou < distancia;)
        {
            yield return passoFisico;
            float proximo = Mathf.Min(distancia, avancou + BalanceamentoDificuldade.Atual.Velocidade(velocidadeDaInvestida) * Time.fixedDeltaTime);
            if (!passouPeloLing && InvestidaAtingeLing(frente, proximo - avancou, raioDaInvestida))
            {
                passouPeloLing = true;
                ling.TakeBossDamage(danoDaInvestida, corpo.position - frente * RaioDoBoss);
                if (LutaEncerrada)
                    yield break;
            }

            AtingirPilaresNaInvestida(frente, proximo - avancou, raioDaInvestida, pilaresAtingidos);
            corpo.MovePosition(origem + frente * proximo);
            avancou = proximo;
        }

        yield return passoFisico;
        emInvestida = false;
        LimparEfeitos();
        yield return ExecutarPisadaEmSeguida();
    }

    private void AtingirPilaresNaInvestida(Vector3 frente, float distancia, float raio,
        HashSet<PillarHealth> pilaresAtingidos)
    {
        ObterExtremosDaCapsula(corpo.position, raio, out Vector3 inferior, out Vector3 superior);
        foreach (Collider col in Physics.OverlapCapsule(inferior, superior, raio, obstaculos, QueryTriggerInteraction.Ignore))
            PillarHealth.HitCollider(col, danoDaInvestida, col.ClosestPoint(corpo.position), pilaresAtingidos);
        foreach (RaycastHit hit in Physics.CapsuleCastAll(inferior, superior, raio, frente,
            distancia, obstaculos, QueryTriggerInteraction.Ignore))
            PillarHealth.HitCollider(hit.collider, danoDaInvestida, hit.point, pilaresAtingidos);
    }

    private bool InvestidaAtingeLing(Vector3 frente, float distancia, float raio)
    {
        ObterExtremosDaCapsula(corpo.position, raio, out Vector3 inferior, out Vector3 superior);
        int mascara = 1 << ling.gameObject.layer;
        return Physics.CheckCapsule(inferior, superior, raio, mascara, QueryTriggerInteraction.Ignore) ||
            Physics.CapsuleCast(inferior, superior, raio, frente, out _, distancia, mascara,
                QueryTriggerInteraction.Ignore);
    }

    private IEnumerator ExecutarPisadaEmSeguida()
    {
        if (!enfurecido || FaseRobo || LutaEncerrada)
            yield break;

        yield return new WaitForSeconds(IntervaloAtePisada);
        if (LutaEncerrada)
            yield break;

        ataqueEscolhido = Ataque.Pisada;
        yield return ExecutarPisada();
    }

    private IEnumerator ExecutarPisada()
    {
        estado = Estado.Preparando;
        Bounds limites = piso.bounds;
        Vector3 centro = corpo.position;
        centro.y = limites.max.y;
        Vector3 ateCentroDoPiso = VetorHorizontal(limites.center - centro);
        float raioDoPiso = RaioDoPiso(limites);
        float alcance = (raioDoPiso + ateCentroDoPiso.magnitude) * fracaoAteBordaMaisDistante;
        CriarEfeitos("Pisada do boss");
        LineRenderer aviso = CriarLinha("Aviso da pisada", 40);
        PosicionarOndaNoPiso(aviso, centro, RaioDoBoss + 1.2f, ateCentroDoPiso, raioDoPiso);
        LineRenderer limiteDaOnda = CriarLinha("Limite da onda", 96);
        PosicionarOndaNoPiso(limiteDaOnda, centro, alcance, ateCentroDoPiso, raioDoPiso);
        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(avisoDaPisada));

        estado = Estado.Atacando;
        EfeitosSonoros.Tocar(Som.Bonk, centro);
        aviso.enabled = false;
        limiteDaOnda.enabled = false;
        LineRenderer onda = CriarLinha("Onda da pisada", 96);
        Colorir(onda, Vermelho);
        onda.widthMultiplier = larguraDaOnda;
        bool atingiuLing = false;
        var pilaresAtingidos = new HashSet<PillarHealth>();
        for (float raio = 0f; raio < alcance;)
        {
            yield return passoFisico;
            float proximoRaio = Mathf.Min(alcance, raio + BalanceamentoDificuldade.Atual.Velocidade(velocidadeDaOnda) * Time.fixedDeltaTime);
            PosicionarOndaNoPiso(onda, centro, proximoRaio, ateCentroDoPiso, raioDoPiso);

            Bounds alvo = colisaoDoLing.bounds;
            float distancia = VetorHorizontal(alvo.center - centro).magnitude;
            float margem = Mathf.Max(alvo.extents.x, alvo.extents.z) + larguraDaOnda * 0.5f;
            if (!atingiuLing && alvo.min.y <= centro.y + alturaDaOnda && alvo.max.y >= centro.y
                && distancia >= raio - margem && distancia <= proximoRaio + margem
                && VetorHorizontal(alvo.center - limites.center).sqrMagnitude <= raioDoPiso * raioDoPiso)
            {
                atingiuLing = ling.TakeBossDamage(danoDaPisada, centro);
                if (LutaEncerrada)
                    yield break;
            }

            foreach (PillarHealth pilar in PillarHealth.ActivePillars)
            {
                if (!pilaresAtingidos.Contains(pilar) && pilar.TryGetAreaHit(centro,
                    proximoRaio + larguraDaOnda * 0.5f, alturaDaOnda, default, out Vector3 ponto) &&
                    VetorHorizontal(ponto - limites.center).sqrMagnitude <= raioDoPiso * raioDoPiso &&
                    pilar.TakeDamage(danoDaPisada, ponto))
                    pilaresAtingidos.Add(pilar);
            }

            raio = proximoRaio;
        }

        yield return passoFisico;
        LimparEfeitos();
    }

    private static void PosicionarOndaNoPiso(LineRenderer linha, Vector3 centro, float raio,
        Vector3 ateCentroDoPiso, float raioDoPiso)
    {
        float distancia = ateCentroDoPiso.magnitude;
        linha.loop = distancia + raio <= raioDoPiso;
        if (linha.loop)
        {
            PosicionarArco(linha, centro, raio, Vector3.forward, 180f, fecharSetor: false);
            return;
        }

        float cosseno = (raio * raio + distancia * distancia - raioDoPiso * raioDoPiso) / (2f * raio * distancia);
        float semiAngulo = Mathf.Acos(Mathf.Clamp(cosseno, -1f, 1f)) * Mathf.Rad2Deg;
        PosicionarArco(linha, centro, raio, ateCentroDoPiso / distancia, semiAngulo, fecharSetor: false);
    }

    private bool Mover(Vector3 destino)
    {
        Vector3 deslocamento = LimitarAoPiso(destino, RaioDoBoss + 0.1f) - corpo.position;
        float distancia = deslocamento.magnitude;
        if (distancia <= 0.001f)
            return false;

        Vector3 direcao = deslocamento / distancia;
        int mascara = obstaculos.value | (1 << ling.gameObject.layer);
        distancia = DistanciaLivre(corpo.position, direcao, distancia, mascara);
        corpo.MovePosition(corpo.position + direcao * distancia);
        return distancia > 0.001f;
    }

    private float DistanciaLivre(Vector3 origem, Vector3 direcao, float distancia, int mascara)
    {
        if (distancia <= 0f)
            return 0f;
        if (VarrerCapsula(origem, direcao, distancia + FolgaDeColisao, mascara, out RaycastHit hit))
            return Mathf.Clamp(hit.distance - FolgaDeColisao, 0f, distancia);
        return distancia;
    }

    private bool VarrerCapsula(Vector3 origem, Vector3 direcao, float distancia, int mascara, out RaycastHit hit)
    {
        float raio = RaioDoBoss;
        ObterExtremosDaCapsula(origem, raio, out Vector3 inferior, out Vector3 superior);
        return Physics.CapsuleCast(inferior, superior, raio, direcao, out hit,
            distancia, mascara, QueryTriggerInteraction.Ignore);
    }

    private void ObterExtremosDaCapsula(Vector3 origem, float raio, out Vector3 inferior, out Vector3 superior)
    {
        Vector3 escala = transform.lossyScale;
        Vector3 centro = origem + corpo.rotation * Vector3.Scale(colisaoDoBoss.center, escala);
        Vector3 eixo = Vector3.up * Mathf.Max(0f, colisaoDoBoss.height * Mathf.Abs(escala.y) * 0.5f - raio);
        inferior = centro - eixo;
        superior = centro + eixo;
    }

    private bool SobrepostoAoLing() => Physics.ComputePenetration(
        colisaoDoBoss, corpo.position, corpo.rotation, colisaoDoLing, corpoDoLing.position, corpoDoLing.rotation,
        out _, out _);

    private void IgnorarColisaoComLing(bool ignorar)
    {
        if (ignorandoColisaoComLing == ignorar)
            return;

        ignorandoColisaoComLing = ignorar;
        if (colisaoDoBoss != null && colisaoDoLing != null && gameObject.activeInHierarchy
            && ling.gameObject.activeInHierarchy)
            Physics.IgnoreCollision(colisaoDoBoss, colisaoDoLing, ignorar);
    }

    private float DistanciaAteBorda(Vector3 origem, Vector3 direcao, float margem = 0.1f)
    {
        Bounds limites = piso.bounds;
        float raio = Mathf.Max(0f, RaioDoPiso(limites) - RaioDoBoss - margem);
        Vector3 desvio = VetorHorizontal(origem - limites.center);
        // Interseção com a borda na direção travada da investida.
        float projecao = Vector3.Dot(desvio, direcao);
        return Mathf.Max(0f, -projecao + Mathf.Sqrt(Mathf.Max(0f, projecao * projecao + raio * raio - desvio.sqrMagnitude)));
    }

    private Vector3 LimitarAoPiso(Vector3 destino, float margem)
    {
        Bounds limites = piso.bounds;
        Vector3 desvio = VetorHorizontal(destino - limites.center);
        float raioUtil = Mathf.Max(0f, RaioDoPiso(limites) - margem);
        Vector3 noPiso = limites.center + Vector3.ClampMagnitude(desvio, raioUtil);
        return new Vector3(noPiso.x, destino.y, noPiso.z);
    }

    private static float RaioDoPiso(Bounds limites) => Mathf.Min(limites.extents.x, limites.extents.z);

    private Vector3 DirecaoDoLing()
    {
        Vector3 delta = VetorHorizontal(corpoDoLing.position - corpo.position);
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
    }

    private static Vector3 VetorHorizontal(Vector3 vetor) => new Vector3(vetor.x, 0f, vetor.z);

    private bool TentarAtingirLing(Vector3 centro, float raio, Vector3 frente, float semiAngulo, float dano)
    {
        Vector3 origem = centro + Vector3.up * 0.65f;
        Vector3 alvo = colisaoDoLing.ClosestPoint(origem);
        Vector3 delta = VetorHorizontal(alvo - centro);
        if (delta.sqrMagnitude > raio * raio || Mathf.Abs(alvo.y - origem.y) > 0.85f ||
            Vector3.Angle(frente, delta) > semiAngulo ||
            Physics.Linecast(origem, alvo, obstaculos, QueryTriggerInteraction.Ignore))
            return false;

        return ling.TakeBossDamage(dano, centro);
    }

    private void CriarEfeitos(string nome) => efeitosDoAtaque = new GameObject(nome);

    private LineRenderer MarcarArea(Vector3 centro, float raio, Vector3 frente, float semiAngulo)
    {
        const int segmentos = 40;
        bool fecharSetor = semiAngulo != 180f;
        LineRenderer linha = CriarLinha("Área de dano", segmentos + (fecharSetor ? 2 : 0));
        PosicionarArco(linha, centro, raio, frente, semiAngulo, fecharSetor);
        return linha;
    }

    private static void PosicionarArco(LineRenderer linha, Vector3 centro, float raio, Vector3 frente,
        float semiAngulo, bool fecharSetor)
    {
        int pontosDoArco = linha.positionCount - (fecharSetor ? 1 : 0);
        int segmentos = pontosDoArco - (semiAngulo == 180f ? 0 : 1);
        centro.y += 0.05f;
        for (int i = 0; i < pontosDoArco; i++)
        {
            float angulo = Mathf.Lerp(-semiAngulo, semiAngulo, i / (float)segmentos);
            linha.SetPosition(i, centro + (Quaternion.Euler(0f, angulo, 0f) * frente) * raio);
        }
        if (fecharSetor)
            linha.SetPosition(linha.positionCount - 1, centro);
    }

    private LineRenderer MarcarTrajeto(Vector3 inicio, Vector3 frente, float distancia, float raio)
    {
        const int segmentosPorPonta = 12;
        LineRenderer linha = CriarLinha("Trajeto da investida", 2 * (segmentosPorPonta + 1));
        inicio.y += 0.05f;
        Vector3 fim = inicio + frente * distancia;
        for (int i = 0; i <= segmentosPorPonta; i++)
        {
            float angulo = Mathf.Lerp(90f, 270f, i / (float)segmentosPorPonta);
            Vector3 borda = Quaternion.Euler(0f, angulo, 0f) * frente * raio;
            linha.SetPosition(i, inicio + borda);
            linha.SetPosition(segmentosPorPonta + 1 + i, fim - borda);
        }
        return linha;
    }

    private LineRenderer CriarLinha(string nome, int quantidadeDePontos)
    {
        GameObject aviso = new GameObject(nome);
        aviso.transform.SetParent(efeitosDoAtaque.transform);
        LineRenderer linha = aviso.AddComponent<LineRenderer>();
        linha.loop = true;
        linha.widthMultiplier = 0.1f;
        linha.positionCount = quantidadeDePontos;
        ConfigurarVisual(linha, Amarelo);
        return linha;
    }

    private void ConfigurarVisual(Renderer renderizador, Color cor)
    {
        renderizador.sharedMaterial = materialDosAvisos;
        renderizador.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderizador.receiveShadows = false;
        Colorir(renderizador, cor);
    }

    private static void Colorir(Renderer renderizador, Color cor)
    {
        var propriedades = new MaterialPropertyBlock();
        propriedades.SetColor(CorBase, cor);
        renderizador.SetPropertyBlock(propriedades);
    }

    private static void MostrarImpacto(LineRenderer linha)
    {
        linha.widthMultiplier = 0.22f;
        Colorir(linha, Vermelho);
    }

    private void AoDerrotarBoss(EnemyHealth boss)
    {
        EncerrarLuta();
        IgnorarColisaoComLing(false);
        colisaoDoBoss.enabled = false;
    }

    private void AoTrocarBarra(int _)
    {
        if (ling.IsDead)
            return;

        enfurecido = false;
        ReiniciarCombate(FaseRobo ? tempoParaRessurgir + 0.65f : 0.9f, transicao: FaseRobo);
    }

    private void ReiniciarCombate(float espera, bool transicao)
    {
        StopAllCoroutines();
        emInvestida = false;
        LimparModoAviao(voltarAoChao: true);
        IgnorarColisaoComLing(false);
        LimparEfeitos();
        LimparPocaDeFogo();
        LimparMinis();
        ApagarOlhoDoCombo();
        ultimoAtaqueNoChao = Ataque.Nenhum;
        ultimoAtaqueNoAr = Ataque.Nenhum;
        colisaoDoBoss.enabled = true;
        estado = transicao ? Estado.Transicao : Estado.Descansando;
        proximaBombaMassiva = Time.time + espera;
        podeVoarApos = Time.time + espera + esperaAntesDoPrimeiroVoo;
        proximoComboRobo = 0f;
        if (TryGetComponent<DamageFlash>(out var flash))
            flash.Clear();
        if (!enfurecido)
        {
            AjustarTamanho(FaseRobo ? multiplicadorDeTamanhoEnfurecido : 1f);
            ColorirFormaHumana(0f);
        }
        if (transicao)
            vida.GrantInvulnerability(espera);
        else
            ExibirForma(false);
        StartCoroutine(Combater(espera));
    }

    private void EncerrarLuta()
    {
        estado = Estado.Encerrado;
        ataqueEscolhido = Ataque.Nenhum;
        emInvestida = false;
        StopAllCoroutines();
        LimparModoAviao(voltarAoChao: true);
        LimparEfeitos();
        LimparPocaDeFogo();
        LimparMinis();
        ApagarOlhoDoCombo();
        ExibirForma(FaseRobo);
    }

    private void LimparEfeitos()
    {
        fimDoAvisoMassivo = 0f;
        bombaNaMao = null;
        tralaleroNasMaos = null;
        preparacaoVisualDoAtaque = 0f;
        momentoDaInvocacaoVisual = float.NegativeInfinity;
        projeteisLaserAtivos = 0;
        if (efeitosDoAtaque != null)
        {
            efeitosDoAtaque.SetActive(false);
            Destroy(efeitosDoAtaque);
        }
        efeitosDoAtaque = null;
    }

    private void OnDisable()
    {
        if (vida != null)
        {
            vida.Died -= AoDerrotarBoss;
            vida.HealthBarChanged -= AoTrocarBarra;
        }
        if (ling != null)
            ling.Died -= EncerrarLuta;
        DesconectarAcertosNoAviao();
        emInvestida = false;
        IgnorarColisaoComLing(false);
        StopAllCoroutines();
        LimparModoAviao(voltarAoChao: true);
        LimparEfeitos();
        LimparPocaDeFogo();
        LimparMinis();
        ApagarOlhoDoCombo();
    }

    private void OnDestroy()
    {
        Destroy(materialDosAvisos);
        Destroy(materialDoVolumeMassivo);
        DestruirVisuaisDoRobo();
    }

    private void OnGUI()
    {
        if (!mostrarHudDeTeste || ling == null)
            return;

        GUI.Box(new Rect(16f, 16f, 225f, 66f), GUIContent.none);
        string textoVida = vida.IsDead ? "Boss derrotado" : $"Fase {vida.CurrentBar}    HP {Mathf.CeilToInt(vida.CurrentHealth)} / {Mathf.CeilToInt(vida.MaxHealth)}";
        GUI.Label(new Rect(25f, 21f, 207f, 20f), textoVida);
        GUI.Label(new Rect(25f, 54f, 207f, 20f), "Vida do Ling: " + Mathf.CeilToInt(ling.CurrentHealth));

        Color corAnterior = GUI.color;
        GUI.color = new Color(0.18f, 0.18f, 0.18f);
        GUI.DrawTexture(new Rect(25f, 43f, 207f, 7f), Texture2D.whiteTexture);
        GUI.color = FaseRobo ? new Color(0.22f, 0.83f, 1f) : enfurecido ? Vermelho : Amarelo;
        float proporcao = Mathf.Clamp01(vida.CurrentHealth / vida.MaxHealth);
        GUI.DrawTexture(new Rect(25f, 43f, 207f * proporcao, 7f), Texture2D.whiteTexture);
        GUI.color = corAnterior;

        if (LutaEncerrada)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), ling.IsDead ? "Ling caiu" : "Vitória");
        else if (estado == Estado.Transicao)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), FaseRobo ? "Ressurgimento do robô" : "Big Yahu enfurecido");
        else if (estado == Estado.Atordoado)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), "Atordoado");
        else if (ataqueEscolhido == Ataque.BombaMassiva && fimDoAvisoMassivo > Time.time)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), $"Suba em um pilar   {Mathf.CeilToInt(fimDoAvisoMassivo - Time.time)} s");
        else if (AviaoPodeSerDerrubado)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), $"Avião   Hashis: {hashisNoAviao}/2");
    }
}
