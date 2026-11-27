using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed partial class BossTungTungTungSahur : MonoBehaviour
{
    private readonly HashSet<PillarHealth> pilaresAtingidosNaTacada = new HashSet<PillarHealth>();

    private enum Estado
    {
        Aproximando, PreparandoTacada, Tacando, RecuperandoDaTacada,
        PreparandoSalto, Subindo, Cruzando, Caindo, Impacto, RecuperandoDoSalto, Encerrado,
        Transformando, PreparandoArmas, LancandoArmas, SubindoMeteoro, ForaDaArena, MirandoMeteoro, CaindoMeteoro,
        EsperandoExplosaoDaTacada, ElevandoMeteoro
    }

    [Header("Arena")]
    [SerializeField, Tooltip("Arraste o Ling da cena para definir o alvo do chefe.")] private PlayerResources ling;
    [SerializeField, Tooltip("Collider do piso circular da arena deste chefe.")] private Collider piso;
    [SerializeField] private LayerMask obstaculos;
    [SerializeField] private Shader shaderDosAvisos;
    [SerializeField] private ExplosaoVfx prefabExplosao;
    [SerializeField] private bool mostrarHudDeTeste;

    [Header("Perseguição")]
    [SerializeField, Min(0.1f)] private float velocidade = 4.25f;
    [SerializeField, Min(0.1f)] private float distanciaParaGolpear = 5.8f;

    [Header("Tacada")]
    [SerializeField, Min(0.1f)] private float alcanceDoGolpe = 6.8f;
    [SerializeField, Range(1f, 179f)] private float semiAnguloDoGolpe = 82f;
    [SerializeField, Min(0.1f)] private float preparacao = 0.75f;
    [SerializeField, Min(0.1f)] private float duracaoDaTacada = 0.24f;
    [SerializeField, Min(0.1f)] private float recuperacao = 1.15f;
    [SerializeField, Min(0f)] private float danoDoGolpe = 16f;
    [SerializeField, Min(0.1f)] private float alturaDoGolpe = 2.4f;
    [SerializeField, Min(0.1f)] private float raioDaExplosaoDaTacada = 3.65f;
    [SerializeField, Min(0f)] private float danoDaExplosaoDaTacada = 12f;
    [SerializeField, Min(0.1f)] private float tempoAteExplodirATacada = 0.55f;
    [SerializeField, Min(0.1f)] private float alcanceDoGiro = 8f;

    [Header("Salto de impacto")]
    [SerializeField, Min(0.1f)] private float alcanceDoSalto = 20f;
    [SerializeField, Min(0.1f)] private float raioDoImpacto = 6.5f;
    [SerializeField, Min(0.1f)] private float alturaDoSalto = 11f;
    [SerializeField, Min(0.1f)] private float avisoDoSalto = 0.55f;
    [SerializeField, Min(0.1f)] private float intervaloEntreSaltos = 7f;
    [SerializeField, Min(0f)] private float danoDoImpacto = 28f;
    [SerializeField, Min(0.1f)] private float alturaDoImpacto = 1.75f;
    [SerializeField, Min(0.1f)] private float recuperacaoDoSalto = 1.55f;

    [Header("Estado atual")]
    [SerializeField] private Estado estado;

    private const float FolgaDeColisao = 0.04f;
    private const float DuracaoDoIndicadorDeImpacto = 0.12f;
    private const float TempoDeSubida = 0.36f;
    private const float TempoDeTravessia = 0.38f;
    private const float TempoDeQueda = 0.26f;
    private const float DuracaoDoGiroAngelical = 0.29f;
    private const float AvisoExtraDoGiroAngelical = 0.18f;
    private const float EsperaExtraParaSaltarDePerto = 1.5f;
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private static readonly Color Amarelo = new Color(1f, 0.72f, 0.12f);
    private static readonly Color Vermelho = new Color(1f, 0.22f, 0.06f);

    private EnemyHealth vida;
    private Rigidbody corpo;
    private CapsuleCollider colisor;
    private CapsuleCollider colisorDoLing;
    private LineRenderer aviso;
    private Material materialDoAviso;
    private Vector3 centroDoGolpe;
    private Vector3 frenteDoGolpe;
    private Vector3 centroDaExplosaoDaTacada;
    private Vector3 saidaDoSalto;
    private Vector3 pousoDoSalto;
    private float alturaDoTrajeto;
    private float inicioDoEstado;
    private float fimDoEstado;
    private float proximoSalto;
    private float anguloAnteriorDaTacada;
    private bool tacadaJaResolveuContato;
    private bool ignorandoColisaoComLing;

    private bool NoAr => estado is Estado.Subindo or Estado.Cruzando or Estado.Caindo or
        Estado.ElevandoMeteoro or Estado.SubindoMeteoro or Estado.ForaDaArena or Estado.MirandoMeteoro or Estado.CaindoMeteoro;
    private float ProgressoDoEstado => Mathf.InverseLerp(inicioDoEstado, fimDoEstado, Time.fixedTime);
    private bool TacadaGiratoria => vida.CurrentBar == 3;
    private float AlcanceAtivoDaTacada => TacadaGiratoria ? alcanceDoGiro : alcanceDoGolpe;

    private float RaioDoCorpo => colisor.radius *
        Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        vida = GetComponent<EnemyHealth>();
        colisor = GetComponent<CapsuleCollider>();
        if (ling == null || !ling.TryGetComponent(out colisorDoLing) || piso == null || shaderDosAvisos == null)
        {
            Debug.LogError("Configure Ling, o piso e o shader de aviso do Tung Tung Tung Sahur.", this);
            enabled = false;
            return;
        }

        float escalaVertical = transform.lossyScale.y;
        float meiaAlturaDaCapsula = Mathf.Max(colisor.height * Mathf.Abs(escalaVertical) * 0.5f, RaioDoCorpo);
        Vector3 posicao = corpo.position;
        posicao.y = piso.bounds.max.y + meiaAlturaDaCapsula - colisor.center.y * escalaVertical;
        corpo.position = posicao;

        materialDoAviso = new Material(shaderDosAvisos);
        GameObject objetoDoAviso = new GameObject("Aviso dos ataques");
        objetoDoAviso.transform.SetParent(transform, false);
        aviso = objetoDoAviso.AddComponent<LineRenderer>();
        aviso.sharedMaterial = materialDoAviso;
        aviso.useWorldSpace = true;
        aviso.loop = true;
        aviso.shadowCastingMode = ShadowCastingMode.Off;
        aviso.receiveShadows = false;
        aviso.enabled = false;
        PrepararArticulacaoDoTaco();
        PrepararVisuaisDasFases();
    }

    private void OnEnable()
    {
        vida.HealthBarChanged += AoMudarDeFase;
        if (faseVisual != vida.CurrentBar)
            AplicarForma(vida.CurrentBar);
        proximaInvocacao = Time.fixedTime + 4f;
        proximoMeteoro = Time.fixedTime + 5f;
    }

    private void FixedUpdate()
    {
        if (vida.IsDead || ling == null || ling.IsDead || !ling.isActiveAndEnabled)
        {
            if (estado != Estado.Encerrado)
                CancelarAtaque();
            estado = Estado.Encerrado;
            if (vida.IsDead && colisor.enabled)
                colisor.enabled = false;
            return;
        }

        if (estado == Estado.Encerrado)
        {
            colisor.enabled = true;
            if (faseVisual != vida.CurrentBar)
                AplicarForma(vida.CurrentBar);
            estado = Estado.Aproximando;
        }

        if (ignorandoColisaoComLing && !NoAr && !SobrepostoAoLing())
            IgnorarColisaoComLing(false);

        switch (estado)
        {
            case Estado.Aproximando:
                AproximarDoLing();
                break;
            case Estado.PreparandoTacada when Time.fixedTime >= fimDoEstado:
                EfeitosSonoros.Tocar(Som.Bonk, corpo.position);
                MarcarImpacto();
                anguloAnteriorDaTacada = TacadaGiratoria ? 0f : semiAnguloDoGolpe;
                pilaresAtingidosNaTacada.Clear();
                tacadaJaResolveuContato = false;
                EntrarNoEstado(Estado.Tacando, BalanceamentoDificuldade.Atual.Duracao(
                    TacadaGiratoria ? DuracaoDoGiroAngelical : duracaoDaTacada));
                AtualizarTacada();
                break;
            case Estado.Tacando:
                AtualizarTacada();
                break;
            case Estado.EsperandoExplosaoDaTacada when Time.fixedTime >= fimDoEstado:
                ExplodirTacada();
                break;
            case Estado.PreparandoSalto when Time.fixedTime >= fimDoEstado:
                EfeitosSonoros.Tocar(Som.TungTung, corpo.position);
                IgnorarColisaoComLing(true);
                EntrarNoEstado(Estado.Subindo, TempoDeSubida);
                break;
            case Estado.Subindo:
            case Estado.Cruzando:
            case Estado.Caindo:
                AtualizarSalto();
                break;
            case Estado.Impacto when Time.fixedTime >= fimDoEstado:
                FinalizarAtaque(Estado.RecuperandoDoSalto, recuperacaoDoSalto);
                break;
            case Estado.RecuperandoDaTacada or Estado.RecuperandoDoSalto when Time.fixedTime >= fimDoEstado:
                estado = Estado.Aproximando;
                break;
            case Estado.Transformando:
                AtualizarTransformacao();
                break;
            case Estado.PreparandoArmas or Estado.LancandoArmas:
                AtualizarInvocacao();
                break;
            case Estado.ElevandoMeteoro or Estado.SubindoMeteoro or Estado.ForaDaArena or
                Estado.MirandoMeteoro or Estado.CaindoMeteoro:
                AtualizarMeteoro();
                break;
        }
    }

    private void AproximarDoLing()
    {
        Bounds arena = piso.bounds;
        Vector3 centro = corpo.position;
        centro.y = arena.max.y;
        Vector3 ateLing = Horizontal(colisorDoLing.bounds.center - centro);
        float distancia = ateLing.magnitude;
        Vector3 frente = distancia > 0.001f ? ateLing / distancia : corpo.rotation * Vector3.forward;
        float distanciaDoGolpe = Mathf.Min(distanciaParaGolpear + (TacadaGiratoria ? 0.8f : 0f), AlcanceAtivoDaTacada);

        if (TentarIniciarAtaqueEspecial(arena, centro, frente, distancia))
            return;

        float esperaParaSaltar = distancia <= distanciaDoGolpe ? EsperaExtraParaSaltarDePerto : 0f;
        if (distancia <= alcanceDoSalto && Time.fixedTime >= proximoSalto + esperaParaSaltar &&
            TentarPrepararSalto(arena, centro, frente))
            return;

        if (distancia <= distanciaDoGolpe && !Physics.Linecast(centro + Vector3.up * 0.75f,
                colisorDoLing.bounds.center, obstaculos, QueryTriggerInteraction.Ignore))
        {
            centroDoGolpe = centro;
            frenteDoGolpe = frente;
            corpo.rotation = Quaternion.LookRotation(frente);
            float avisoDaTacada = preparacao + (TacadaGiratoria ? AvisoExtraDoGiroAngelical : 0f);
            EntrarNoEstado(Estado.PreparandoTacada, BalanceamentoDificuldade.Atual.Aviso(avisoDaTacada));
            MarcarArea(centro, AlcanceAtivoDaTacada, frente, TacadaGiratoria ? 180f : semiAnguloDoGolpe);
            if (TacadaGiratoria)
                aviso.widthMultiplier = 0.24f;
            return;
        }

        float passo = Mathf.Min(velocidade * (1f + 0.1f * (vida.CurrentBar - 1)) * Time.fixedDeltaTime, distancia);
        Vector3 destino = LimitarAoPiso(corpo.position + frente * passo, arena);
        Vector3 deslocamento = destino - corpo.position;
        if (deslocamento.sqrMagnitude < 0.000001f)
            return;

        int mascara = obstaculos.value | (1 << ling.gameObject.layer);
        if (VarrerCorpo(deslocamento, mascara, out RaycastHit hit))
        {
            Vector3 normal = Horizontal(hit.normal).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, normal);
            if (Vector3.Dot(lado, frente) < 0f)
                lado = -lado;

            destino = LimitarAoPiso(corpo.position + (lado + normal * 0.15f).normalized * passo, arena);
            deslocamento = destino - corpo.position;
            if (deslocamento.sqrMagnitude < 0.000001f)
                return;

            if (VarrerCorpo(deslocamento, mascara, out hit))
                deslocamento = deslocamento.normalized * Mathf.Max(0f, hit.distance - FolgaDeColisao);
        }

        if (deslocamento.sqrMagnitude < 0.000001f)
            return;

        corpo.MoveRotation(Quaternion.LookRotation(deslocamento));
        corpo.MovePosition(corpo.position + deslocamento);
    }

    private bool VarrerCorpo(Vector3 deslocamento, int mascara, out RaycastHit hit)
    {
        ExtremosDaCapsula(corpo.position, out Vector3 inferior, out Vector3 superior);
        return Physics.CapsuleCast(inferior, superior, RaioDoCorpo, deslocamento.normalized, out hit,
            deslocamento.magnitude + FolgaDeColisao, mascara, QueryTriggerInteraction.Ignore);
    }

    private void ExtremosDaCapsula(Vector3 origem, out Vector3 inferior, out Vector3 superior)
    {
        Vector3 escala = transform.lossyScale;
        Vector3 centro = origem + corpo.rotation * Vector3.Scale(colisor.center, escala);
        float meiaAltura = Mathf.Max(0f, colisor.height * Mathf.Abs(escala.y) * 0.5f - RaioDoCorpo);
        Vector3 eixo = Vector3.up * meiaAltura;
        inferior = centro - eixo;
        superior = centro + eixo;
    }

    private Vector3 LimitarAoPiso(Vector3 destino, Bounds arena)
    {
        float raio = Mathf.Max(0f, Mathf.Min(arena.extents.x, arena.extents.z) - RaioDoCorpo - FolgaDeColisao);
        Vector3 noPiso = arena.center + Vector3.ClampMagnitude(Horizontal(destino - arena.center), raio);
        return new Vector3(noPiso.x, destino.y, noPiso.z);
    }

    private bool TentarPrepararSalto(Bounds arena, Vector3 origem, Vector3 frente)
    {
        if (!TentarEscolherPouso(arena, origem, alcanceDoSalto, out Vector3 alvo))
            return false;

        saidaDoSalto = origem;
        pousoDoSalto = alvo;
        alturaDoTrajeto = origem.y + alturaDoSalto;
        corpo.rotation = Quaternion.LookRotation(frente);
        proximoSalto = Time.fixedTime + intervaloEntreSaltos;
        EntrarNoEstado(Estado.PreparandoSalto, BalanceamentoDificuldade.Atual.Aviso(avisoDoSalto));
        MarcarArea(alvo, raioDoImpacto, frente, 180f);
        return true;
    }

    private bool TentarEscolherPouso(Bounds arena, Vector3 origem, float alcance, out Vector3 alvo)
    {
        alvo = colisorDoLing.bounds.center;
        alvo.y = arena.max.y;
        alvo = LimitarAoPiso(alvo, arena);
        if (Horizontal(alvo - origem).sqrMagnitude <= alcance * alcance && PousoLivre(alvo))
            return true;

        Vector3 paraOCentro = Horizontal(arena.center - alvo).normalized;
        if (paraOCentro == Vector3.zero)
            paraOCentro = Vector3.forward;
        for (int i = 0; i < 8; i++)
        {
            Vector3 desvio = Quaternion.AngleAxis(i * 45f, Vector3.up) * paraOCentro;
            Vector3 alternativa = LimitarAoPiso(alvo + desvio * (RaioDoCorpo + 2f), arena);
            if (Horizontal(alternativa - origem).sqrMagnitude > alcance * alcance || !PousoLivre(alternativa))
                continue;

            alvo = alternativa;
            return true;
        }

        return false;
    }

    private bool PousoLivre(Vector3 alvo)
    {
        int mascara = obstaculos.value & ~(1 << piso.gameObject.layer);
        ExtremosDaCapsula(alvo, out Vector3 inferior, out Vector3 superior);
        return !Physics.CheckCapsule(inferior, superior, RaioDoCorpo + FolgaDeColisao,
            mascara, QueryTriggerInteraction.Ignore);
    }

    private void AtualizarSalto()
    {
        float t = ProgressoDoEstado;
        Vector3 posicao = saidaDoSalto;
        switch (estado)
        {
            case Estado.Subindo:
                posicao.y = Mathf.Lerp(saidaDoSalto.y, alturaDoTrajeto, 1f - (1f - t) * (1f - t));
                if (t >= 1f)
                    EntrarNoEstado(Estado.Cruzando, BalanceamentoDificuldade.Atual.Duracao(TempoDeTravessia));
                break;
            case Estado.Cruzando:
                posicao = Vector3.Lerp(saidaDoSalto, pousoDoSalto, Mathf.SmoothStep(0f, 1f, t));
                posicao.y = alturaDoTrajeto;
                if (t >= 1f)
                    EntrarNoEstado(Estado.Caindo, BalanceamentoDificuldade.Atual.Duracao(TempoDeQueda));
                break;
            case Estado.Caindo:
                posicao = pousoDoSalto;
                posicao.y = Mathf.Lerp(alturaDoTrajeto, pousoDoSalto.y, t * t);
                if (t >= 1f)
                {
                    EntrarNoEstado(Estado.Impacto, DuracaoDoIndicadorDeImpacto);
                    MarcarImpacto();
                    ReproduzirExplosao(pousoDoSalto, raioDoImpacto, Amarelo, 0.65f);
                    if (LingNaArea(pousoDoSalto, raioDoImpacto, alturaDoImpacto, out _))
                        ling.TakeBossDamage(danoDoImpacto, pousoDoSalto);
                    PillarHealth.HitArea(pousoDoSalto, raioDoImpacto, alturaDoImpacto, danoDoImpacto, obstaculos);
                }
                break;
        }

        corpo.MovePosition(posicao);
    }

    private void MarcarArea(Vector3 centro, float raio, Vector3 frente, float semiAngulo)
    {
        PosicionarContorno(aviso, centro, raio, frente, semiAngulo);
        materialDoAviso.SetColor(CorBase, Amarelo);
        aviso.widthMultiplier = 0.1f;
        aviso.enabled = true;
    }

    private static void PosicionarContorno(LineRenderer linha, Vector3 centro, float raio, Vector3 frente, float semiAngulo)
    {
        const int segmentos = 48;
        bool circulo = semiAngulo == 180f;
        Vector3 noChao = centro + Vector3.up * 0.05f;
        linha.positionCount = circulo ? segmentos : segmentos + 2;
        if (!circulo)
            linha.SetPosition(0, noChao);

        for (int i = circulo ? 0 : 1; i < linha.positionCount; i++)
        {
            float t = (circulo ? i : i - 1) / (float)segmentos;
            float angulo = Mathf.Lerp(-semiAngulo, semiAngulo, t);
            linha.SetPosition(i, noChao + Quaternion.AngleAxis(angulo, Vector3.up) * frente * raio);
        }
    }

    private void MarcarImpacto()
    {
        materialDoAviso.SetColor(CorBase, Vermelho);
        aviso.widthMultiplier = 0.2f;
    }

    private void AtualizarTacada()
    {
        float progresso = ProgressoDoEstado;
        float angulo = TacadaGiratoria ? -360f * progresso : Mathf.Lerp(semiAnguloDoGolpe, -semiAnguloDoGolpe, progresso);
        if (TacadaGiratoria)
        {
            Vector3 frenteDaTacada = Quaternion.AngleAxis(angulo, Vector3.up) * frenteDoGolpe;
            corpo.MoveRotation(Quaternion.LookRotation(frenteDaTacada));
        }

        if (!tacadaJaResolveuContato && LingNaArea(centroDoGolpe, AlcanceAtivoDaTacada, alturaDoGolpe, out Vector3 delta))
        {
            if (TacadaPassouPeloAlvo(delta, colisorDoLing.radius, angulo))
            {
                tacadaJaResolveuContato = true;
                ling.TakeBossDamage(danoDoGolpe, centroDoGolpe);
            }
        }

        foreach (PillarHealth pilar in PillarHealth.ActivePillars)
        {
            if (!pilaresAtingidosNaTacada.Contains(pilar) &&
                pilar.TryGetAreaHit(centroDoGolpe, AlcanceAtivoDaTacada, alturaDoGolpe, obstaculos, out Vector3 ponto))
            {
                Bounds coluna = pilar.Shaft.bounds;
                float raioDoPilar = Mathf.Max(coluna.extents.x, coluna.extents.z);
                if (TacadaPassouPeloAlvo(Horizontal(coluna.center - centroDoGolpe), raioDoPilar, angulo) &&
                    pilar.TakeDamage(danoDoGolpe, ponto))
                    pilaresAtingidosNaTacada.Add(pilar);
            }
        }

        anguloAnteriorDaTacada = angulo;
        if (progresso < 1f)
            return;

        if (vida.CurrentBar == 2)
        {
            Bounds arena = piso.bounds;
            centroDaExplosaoDaTacada = LimitarAoPiso(centroDoGolpe + frenteDoGolpe * (alcanceDoGolpe * 0.68f), arena);
            MarcarArea(centroDaExplosaoDaTacada, raioDaExplosaoDaTacada, frenteDoGolpe, 180f);
            materialDoAviso.SetColor(CorBase, Carmesim);
            aviso.widthMultiplier = 0.22f;
            EntrarNoEstado(Estado.EsperandoExplosaoDaTacada, BalanceamentoDificuldade.Atual.Aviso(tempoAteExplodirATacada, 0.85f));
        }
        else
            FinalizarAtaque(Estado.RecuperandoDaTacada, recuperacao);
    }

    private bool TacadaPassouPeloAlvo(Vector3 delta, float raioDoAlvo, float angulo)
    {
        float anguloDoAlvo = Vector3.SignedAngle(frenteDoGolpe, delta, Vector3.up);
        float margem = Mathf.Asin(Mathf.Clamp01(raioDoAlvo / Mathf.Max(0.01f, delta.magnitude))) * Mathf.Rad2Deg;
        if (TacadaGiratoria)
        {
            float distanciaAngular = Mathf.Repeat(anguloAnteriorDaTacada - anguloDoAlvo, 360f);
            return distanciaAngular <= anguloAnteriorDaTacada - angulo + margem || distanciaAngular >= 360f - margem;
        }

        return Mathf.Abs(anguloDoAlvo) <= semiAnguloDoGolpe &&
            anguloDoAlvo >= angulo - margem && anguloDoAlvo <= anguloAnteriorDaTacada + margem;
    }

    private void ExplodirTacada()
    {
        EfeitosSonoros.Tocar(Som.Fogo, centroDaExplosaoDaTacada);
        ReproduzirExplosao(centroDaExplosaoDaTacada, raioDaExplosaoDaTacada, Carmesim);
        if (LingNaArea(centroDaExplosaoDaTacada, raioDaExplosaoDaTacada, 1.8f, out _))
            ling.TakeBossDamage(danoDaExplosaoDaTacada, centroDaExplosaoDaTacada);
        PillarHealth.HitArea(centroDaExplosaoDaTacada, raioDaExplosaoDaTacada, 1.8f, danoDaExplosaoDaTacada, obstaculos);
        FinalizarAtaque(Estado.RecuperandoDaTacada, recuperacao);
    }

    private void ReproduzirExplosao(Vector3 centro, float raio, Color cor, float intensidade = 1f)
    {
        EfeitosSonoros.Tocar(Som.Explosao, centro);
        if (prefabExplosao == null)
            return;

        ExplosaoVfx efeito = Instantiate(prefabExplosao, centro, Quaternion.identity);
        efeito.Configurar(raio, cor, intensidade);
    }

    private bool LingNaArea(Vector3 centro, float raio, float altura, out Vector3 delta)
    {
        Vector3 origem = centro + Vector3.up * Mathf.Min(0.8f, altura * 0.5f);
        Vector3 alvo = colisorDoLing.ClosestPoint(origem);
        delta = Horizontal(alvo - centro);
        return delta.sqrMagnitude <= raio * raio && alvo.y >= centro.y && alvo.y <= centro.y + altura &&
            !Physics.Linecast(origem, alvo, obstaculos, QueryTriggerInteraction.Ignore);
    }

    private void EntrarNoEstado(Estado novo, float duracao)
    {
        estado = novo;
        inicioDoEstado = Time.fixedTime;
        fimDoEstado = inicioDoEstado + duracao;
    }

    private void FinalizarAtaque(Estado recuperando, float descanso)
    {
        aviso.enabled = false;
        float tempo = BalanceamentoDificuldade.Atual.Descanso(descanso, Mathf.Min(descanso, 0.75f));
        EntrarNoEstado(recuperando, tempo * 1.265f);
    }

    private bool SobrepostoAoLing() => Physics.ComputePenetration(colisor, corpo.position, corpo.rotation,
        colisorDoLing, colisorDoLing.transform.position, colisorDoLing.transform.rotation, out _, out _);

    private void IgnorarColisaoComLing(bool ignorar)
    {
        if (ignorandoColisaoComLing == ignorar)
            return;

        ignorandoColisaoComLing = ignorar;
        if (colisorDoLing != null && colisorDoLing.gameObject.activeInHierarchy && gameObject.activeInHierarchy)
            Physics.IgnoreCollision(colisor, colisorDoLing, ignorar);
    }

    private static Vector3 Horizontal(Vector3 vetor) => new Vector3(vetor.x, 0f, vetor.z);

    private void CancelarAtaque()
    {
        if (aviso != null)
            aviso.enabled = false;
        if (NoAr)
            corpo.position = saidaDoSalto;
        LimparArmas();
        EncerrarVisualDoMeteoro();
        IgnorarColisaoComLing(false);
    }

    private void OnDisable()
    {
        if (vida != null)
            vida.HealthBarChanged -= AoMudarDeFase;
        CancelarAtaque();
        ReiniciarPoseDoTaco();
        estado = Estado.Encerrado;
    }

    private void OnDestroy()
    {
        Destroy(materialDoAviso);
        Destroy(malhaArticulada);
        DestruirRecursosVisuais();
        if (aviso != null)
            Destroy(aviso.gameObject);
    }

    private void OnGUI()
    {
        if (!mostrarHudDeTeste || ling == null)
            return;

        string nomeDaForma = vida.ObterNomeDaFase("Tung Tung Tung Sahur");
        string textoBoss = vida.IsDead ? "Boss derrotado" :
            $"{nomeDaForma}   {Mathf.CeilToInt(vida.CurrentHealth)} / {Mathf.CeilToInt(vida.MaxHealth)} HP";
        string textoLing = ling.IsDead ? "Ling caiu" : $"Ling   HP {Mathf.CeilToInt(ling.CurrentHealth)}";
        if (estado == Estado.Transformando)
            textoLing += "   Transformação";
        GUI.Box(new Rect(16f, 16f, 310f, 58f), GUIContent.none);
        GUI.Label(new Rect(25f, 22f, 292f, 20f), textoBoss);
        GUI.Label(new Rect(25f, 43f, 207f, 20f), textoLing);
    }
}
