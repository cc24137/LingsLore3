using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class BossTungTungTungSahur
{
    [Header("Armas de madeira")]
    [SerializeField, Min(0.3f)] private float avisoDasLancas = 0.9f;
    [SerializeField, Min(5f)] private float alcanceDasLancas = 22f;
    [SerializeField, Min(0f)] private float danoDaLanca = 18f;
    [SerializeField, Min(0.3f)] private float avisoDosMartelos = 1.05f;
    [SerializeField, Min(0f)] private float danoDoMartelo = 26f;
    [SerializeField, Min(1f)] private float multiplicadorDaLarguraDemonio = 1.45f;
    [SerializeField, Min(1f)] private float multiplicadorDaVelocidadeDemonio = 1.35f;

    private const float LarguraDaLanca = 0.52f;
    private const float AlturaDaLanca = 0.95f;
    private const float VelocidadeDaLanca = 31f;
    private const float RaioDoMartelo = 2.75f;
    private const float TempoDeQuedaDoMartelo = 0.36f;
    private const float AlturaDoMartelo = 7.2f;
    private const float TempoDeGuiagem = 0.38f;
    private const float AnguloMaximoDeGuiagem = 22f;
    private static readonly int[] OrdemDasArmas = { 0, -1, 1 };

    private sealed class ArmaInvocada
    {
        public Transform Visual;
        public LineRenderer Aviso;
        public Vector3 Origem;
        public Vector3 Destino;
        public Vector3 DestinoInicial;
        public Vector3 Frente;
        public Vector3 FrenteInicial;
        public Vector3 DesvioLateral;
        public float Percurso;
        public float Avanco;
    }

    private readonly List<ArmaInvocada> armas = new List<ArmaInvocada>(3);
    private Transform grupoDeArmas;
    private float proximaInvocacao;
    private bool martelos;
    private bool invocarMartelosNaProxima = true;
    private bool contatoDaInvocacaoResolvido;
    private MaterialPropertyBlock propriedadesDoAvisoDaArma;
    private bool ArmasGuiadas => vida.CurrentBar == 3;
    private float LarguraAtivaDaLanca => LarguraDaLanca * (vida.CurrentBar == 2 ? multiplicadorDaLarguraDemonio : 1f);
    private float VelocidadeAtivaDaLanca => BalanceamentoDificuldade.Atual.Velocidade(VelocidadeDaLanca) * (vida.CurrentBar == 2 ? multiplicadorDaVelocidadeDemonio : 1f);

    private bool TentarInvocarArmas(Bounds arena, Vector3 centro, Vector3 frente, float distancia)
    {
        if (madeiraEscura == null)
            return false;

        bool perto = distancia < 3.2f;
        martelos = vida.CurrentBar == 3 && (invocarMartelosNaProxima || perto);
        if (!martelos && perto)
            return false;

        grupoDeArmas = CriarFilho("Armas invocadas", transform.parent);
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        Vector3 alvo = colisorDoLing.bounds.center;
        alvo.y = arena.max.y;
        foreach (int i in OrdemDasArmas)
        {
            Vector3 desvio = lado * (i * (martelos ? 5.8f : 2.5f));
            if (martelos)
                InvocarMartelo(LimitarAoPiso(alvo + desvio, arena), frente, desvio);
            else
                InvocarLanca(LimitarAoPiso(centro + frente * 1.2f + desvio, arena), frente, arena, desvio);
        }

        if (armas.Count == 0)
        {
            LimparArmas();
            proximaInvocacao = Time.fixedTime + 1f;
            return false;
        }

        corpo.rotation = Quaternion.LookRotation(frente);
        contatoDaInvocacaoResolvido = false;
        invocarMartelosNaProxima = !martelos;
        proximaInvocacao = Time.fixedTime + (vida.CurrentBar == 3 ? 5.8f : 8.5f);
        EntrarNoEstado(Estado.PreparandoArmas, BalanceamentoDificuldade.Atual.Aviso(martelos ? avisoDosMartelos : avisoDasLancas, 0.60f));
        EfeitosSonoros.Tocar(Som.TungTung, corpo.position);
        return true;
    }

    private void InvocarLanca(Vector3 origem, Vector3 frente, Bounds arena, Vector3 desvioLateral)
    {
        float largura = LarguraAtivaDaLanca;
        Vector3 alturaDaPonta = origem + Vector3.up * AlturaDaLanca;
        float distancia = DistanciaLivreParaLanca(alturaDaPonta, frente, arena, largura, alcanceDasLancas);
        if (distancia < 2f)
            return;

        LineRenderer linha = CriarAvisoDaArma();
        PosicionarFaixaDaLanca(linha, origem, frente, largura, distancia);

        Transform visual = CriarFilho("Lança de madeira", grupoDeArmas);
        visual.SetPositionAndRotation(alturaDaPonta + Vector3.up * 2.6f, Quaternion.LookRotation(frente));
        float escalaDaLargura = largura / LarguraDaLanca;
        visual.localScale = new Vector3(escalaDaLargura, escalaDaLargura, 1f);
        CriarPecaDaArma("Haste", visual, PrimitiveType.Cylinder, new Vector3(0f, 0f, -1.15f),
            new Vector3(0.16f, 1.1f, 0.16f), Quaternion.Euler(90f, 0f, 0f), madeiraClara);
        Transform ponta = AdicionarMalha("Ponta talhada", visual, malhaCurvada, madeiraEscura);
        ponta.localPosition = Vector3.back * 0.84f;
        ponta.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ponta.localScale = new Vector3(1.2f, 1.1f, 1.2f);
        armas.Add(new ArmaInvocada
        {
            Visual = visual, Aviso = linha, Origem = alturaDaPonta, Frente = frente, FrenteInicial = frente,
            DesvioLateral = desvioLateral, Percurso = ArmasGuiadas ? alcanceDasLancas : distancia
        });
    }

    private void InvocarMartelo(Vector3 destino, Vector3 frente, Vector3 desvioLateral)
    {
        if (Physics.CheckSphere(destino + Vector3.up * 1.05f, 0.9f, obstaculos, QueryTriggerInteraction.Ignore))
            return;
        foreach (ArmaInvocada arma in armas)
        {
            if ((arma.Destino - destino).sqrMagnitude < 9f)
                return;
        }

        LineRenderer linha = CriarAvisoDaArma();
        PosicionarContorno(linha, destino, RaioDoMartelo, frente, 180f);
        Transform visual = CriarFilho("Martelo de madeira", grupoDeArmas);
        Vector3 topo = destino + Vector3.up * AlturaDoMartelo;
        visual.SetPositionAndRotation(topo, Quaternion.LookRotation(frente));
        CriarPecaDaArma("Cabo", visual, PrimitiveType.Cylinder, Vector3.up * 1.7f,
            new Vector3(0.26f, 1.35f, 0.26f), Quaternion.identity, madeiraClara);
        CriarPecaDaArma("Cabeça", visual, PrimitiveType.Cube, Vector3.up * 0.6f,
            new Vector3(2.35f, 1.2f, 1.15f), Quaternion.identity, madeiraEscura);
        for (int lado = -1; lado <= 1; lado += 2)
        {
            CriarPecaDaArma("Aro de madeira clara", visual, PrimitiveType.Cube, new Vector3(lado * 0.79f, 0.6f, 0f),
                new Vector3(0.20f, 1.24f, 1.19f), Quaternion.identity, madeiraClara);
        }
        armas.Add(new ArmaInvocada
        {
            Visual = visual, Aviso = linha, Origem = topo, Destino = destino, DestinoInicial = destino,
            Frente = frente, DesvioLateral = desvioLateral
        });
    }

    private void AtualizarInvocacao()
    {
        float progresso = ProgressoDoEstado;
        if (estado == Estado.PreparandoArmas)
        {
            foreach (ArmaInvocada arma in armas)
            {
                if (martelos && progresso < 0.56f)
                    GuiarMartelo(arma);
                float altura = martelos ? Mathf.Sin(progresso * Mathf.PI) * 0.65f : (1f - progresso) * 2.6f;
                arma.Visual.position = arma.Origem + Vector3.up * altura;
                arma.Aviso.widthMultiplier = Mathf.Lerp(0.10f, 0.22f, progresso);
            }
            if (progresso < 1f)
                return;

            foreach (ArmaInvocada arma in armas)
                ColorirAvisoDaArma(arma.Aviso, !martelos && ArmasGuiadas ? Dourado : Vermelho);
            if (martelos)
                EfeitosSonoros.Tocar(Som.SomSagrado, armas[0].Destino);
            EntrarNoEstado(Estado.LancandoArmas, martelos ? BalanceamentoDificuldade.Atual.Duracao(TempoDeQuedaDoMartelo) :
                alcanceDasLancas / VelocidadeAtivaDaLanca);
            return;
        }

        if (martelos)
        {
            foreach (ArmaInvocada arma in armas)
                arma.Visual.position = Vector3.Lerp(arma.Origem, arma.Destino, progresso * progresso);
            if (progresso < 1f)
                return;

            var pilaresAtingidos = new HashSet<PillarHealth>();
            foreach (ArmaInvocada arma in armas)
            {
                if (!contatoDaInvocacaoResolvido && LingNaArea(arma.Destino, RaioDoMartelo, 2.8f, out _))
                {
                    contatoDaInvocacaoResolvido = true;
                    ling.TakeBossDamage(danoDoMartelo, arma.Destino);
                }
                PillarHealth.HitArea(arma.Destino, RaioDoMartelo, 2.8f, danoDoMartelo, obstaculos, pilaresAtingidos);
            }
            LimparArmas();
        }
        else if (AvancarLancas())
            return;

        FinalizarAtaque(Estado.RecuperandoDoSalto, 0.8f);
    }

    private bool AvancarLancas()
    {
        Bounds arena = piso.bounds;
        float largura = LarguraAtivaDaLanca;
        bool guiando = ArmasGuiadas && Time.fixedTime - inicioDoEstado <= TempoDeGuiagem;
        bool emVoo = false;
        foreach (ArmaInvocada arma in armas)
        {
            if (arma.Avanco >= arma.Percurso)
                continue;

            Vector3 inicio = arma.Visual.position;
            if (guiando)
                GuiarLanca(arma, inicio);

            float livre = arma.Percurso - arma.Avanco;
            if (ArmasGuiadas)
                livre = DistanciaLivreParaLanca(inicio, arma.Frente, arena, largura, livre);
            float passo = Mathf.Min(livre, VelocidadeAtivaDaLanca * Time.fixedDeltaTime);
            if (passo > 0f && !contatoDaInvocacaoResolvido && LancaAtingiuLing(inicio, arma.Frente, passo, largura))
            {
                contatoDaInvocacaoResolvido = true;
                ling.TakeBossDamage(danoDaLanca, inicio);
            }

            bool continua = passo < livre - 0.001f;
            if (!continua && Physics.SphereCast(inicio, largura, arma.Frente, out RaycastHit acerto,
                passo + 0.02f, obstaculos, QueryTriggerInteraction.Ignore))
                PillarHealth.HitCollider(acerto.collider, danoDaLanca, acerto.point);
            arma.Avanco = continua ? arma.Avanco + passo : arma.Percurso;
            Vector3 posicao = inicio + arma.Frente * passo;
            arma.Visual.position = posicao;
            if (continua && ArmasGuiadas)
            {
                posicao.y = arena.max.y;
                PosicionarFaixaDaLanca(arma.Aviso, posicao, arma.Frente, largura, livre - passo);
                if (!guiando)
                    ColorirAvisoDaArma(arma.Aviso, Vermelho);
            }
            arma.Visual.gameObject.SetActive(continua);
            arma.Aviso.enabled = continua;
            emVoo |= continua;
        }

        if (!emVoo)
            LimparArmas();
        return emVoo;
    }

    private void GuiarLanca(ArmaInvocada arma, Vector3 inicio)
    {
        Vector3 alvo = colisorDoLing.bounds.center + arma.DesvioLateral * 0.72f;
        Vector3 direcao = Horizontal(alvo - inicio);
        if (Vector3.Dot(direcao, arma.FrenteInicial) <= 0.1f)
            return;

        Vector3 curva = Vector3.RotateTowards(arma.Frente, direcao.normalized, 62f * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        float angulo = Mathf.Clamp(Vector3.SignedAngle(arma.FrenteInicial, curva, Vector3.up),
            -AnguloMaximoDeGuiagem, AnguloMaximoDeGuiagem);
        arma.Frente = Quaternion.AngleAxis(angulo, Vector3.up) * arma.FrenteInicial;
        arma.Visual.rotation = Quaternion.LookRotation(arma.Frente);
    }

    private void GuiarMartelo(ArmaInvocada arma)
    {
        Bounds arena = piso.bounds;
        Vector3 alvo = colisorDoLing.bounds.center + arma.DesvioLateral;
        alvo.y = arena.max.y;
        alvo = arma.DestinoInicial + Vector3.ClampMagnitude(alvo - arma.DestinoInicial, 2.8f);
        Vector3 destino = LimitarAoPiso(Vector3.MoveTowards(arma.Destino, alvo, 6f * Time.fixedDeltaTime), arena);
        if (Physics.CheckSphere(destino + Vector3.up * 1.05f, 0.9f, obstaculos, QueryTriggerInteraction.Ignore))
            return;

        arma.Destino = destino;
        arma.Origem = destino + Vector3.up * AlturaDoMartelo;
        PosicionarContorno(arma.Aviso, destino, RaioDoMartelo, arma.Frente, 180f);
    }

    private float DistanciaLivreParaLanca(Vector3 origem, Vector3 frente, Bounds arena, float largura, float limite)
    {
        if (Physics.CheckSphere(origem, largura, obstaculos, QueryTriggerInteraction.Ignore))
            return 0f;

        Vector3 desvio = Horizontal(origem - arena.center);
        float raio = Mathf.Min(arena.extents.x, arena.extents.z) - largura;
        float projecao = Vector3.Dot(desvio, frente);
        float discriminante = projecao * projecao + raio * raio - desvio.sqrMagnitude;
        float distancia = Mathf.Clamp(-projecao + Mathf.Sqrt(Mathf.Max(0f, discriminante)), 0f, limite);
        if (distancia > 0f && Physics.SphereCast(origem, largura, frente, out RaycastHit parede,
                distancia, obstaculos, QueryTriggerInteraction.Ignore))
            return parede.distance;
        return distancia;
    }

    private static void PosicionarFaixaDaLanca(LineRenderer linha, Vector3 origem, Vector3 frente, float largura, float distancia)
    {
        linha.positionCount = 4;
        Vector3 lateral = Vector3.Cross(Vector3.up, frente) * largura;
        origem += Vector3.up * 0.06f;
        Vector3 fim = origem + frente * distancia;
        linha.SetPosition(0, origem + lateral);
        linha.SetPosition(1, fim + lateral);
        linha.SetPosition(2, fim - lateral);
        linha.SetPosition(3, origem - lateral);
    }

    private bool LancaAtingiuLing(Vector3 inicio, Vector3 frente, float passo, float largura)
    {
        Vector3 alvo = colisorDoLing.bounds.center;
        Vector3 pontoNaTrajetoria = inicio + frente * Mathf.Clamp(Vector3.Dot(alvo - inicio, frente), 0f, passo);
        return (colisorDoLing.ClosestPoint(pontoNaTrajetoria) - pontoNaTrajetoria).sqrMagnitude <=
            largura * largura;
    }

    private LineRenderer CriarAvisoDaArma()
    {
        var linha = CriarFilho("Alcance da arma", grupoDeArmas).gameObject.AddComponent<LineRenderer>();
        linha.sharedMaterial = materialDoAviso;
        linha.useWorldSpace = true;
        linha.loop = true;
        linha.widthMultiplier = 0.1f;
        linha.shadowCastingMode = ShadowCastingMode.Off;
        linha.receiveShadows = false;
        ColorirAvisoDaArma(linha, ArmasGuiadas ? Dourado : Amarelo);
        return linha;
    }

    private void ColorirAvisoDaArma(LineRenderer linha, Color cor)
    {
        propriedadesDoAvisoDaArma ??= new MaterialPropertyBlock();
        propriedadesDoAvisoDaArma.SetColor(CorBase, cor);
        linha.SetPropertyBlock(propriedadesDoAvisoDaArma);
    }

    private static void CriarPecaDaArma(string nome, Transform pai, PrimitiveType tipo, Vector3 posicao,
        Vector3 escala, Quaternion rotacao, Material material)
    {
        var peca = GameObject.CreatePrimitive(tipo);
        peca.name = nome;
        Collider colisao = peca.GetComponent<Collider>();
        colisao.enabled = false;
        Destroy(colisao);
        peca.GetComponent<MeshRenderer>().sharedMaterial = material;
        peca.transform.SetParent(pai, false);
        peca.transform.SetLocalPositionAndRotation(posicao, rotacao);
        peca.transform.localScale = escala;
    }

    private void LimparArmas()
    {
        armas.Clear();
        if (grupoDeArmas != null)
        {
            grupoDeArmas.gameObject.SetActive(false);
            Destroy(grupoDeArmas.gameObject);
        }
        grupoDeArmas = null;
    }
}
