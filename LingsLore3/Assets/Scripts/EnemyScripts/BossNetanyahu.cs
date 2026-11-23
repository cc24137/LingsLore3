using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class BossNetanyahu : MonoBehaviour
{
    private enum Estado { Descansando, Aproximando, Preparando, Atacando, Encerrado, Transicao }
    private enum Ataque { Nenhum, Bombas, Combo, Investida, Pisada, BombaIncendiaria }

    [Header("Cena de teste")]
    [SerializeField] private PlayerResources ling;
    [SerializeField] private Collider piso;
    [SerializeField] private LayerMask obstaculos;
    [SerializeField] private Shader shaderDosAvisos;

    [Header("Bombas")]
    [SerializeField] private Bomba prefabBomba;
    [SerializeField, Min(0.1f)] private float distanciaParaBombas = 13f;
    [SerializeField, Min(0f)] private float tempoDeRecuo = 0.65f;
    [SerializeField, Min(0f)] private float velocidadeDeRecuo = 3f;

    [Header("Segunda fase")]
    [SerializeField] private PocaDeFogo prefabPocaDeFogo;
    [SerializeField, Min(0f)] private float duracaoDaTransicao = 1.4f;
    [SerializeField, Min(1f)] private float multiplicadorDeTamanhoNaFase2 = 1.15f;

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
    [SerializeField, Min(0.1f)] private float velocidadeDaInvestida = 32f;
    [SerializeField, Min(0f)] private float danoDaInvestida = 20f;

    [Header("Pisada")]
    [SerializeField, Min(0.1f)] private float distanciaParaPisada = 4.5f;
    [SerializeField, Min(0.1f)] private float avisoDaPisada = 0.7f;
    [SerializeField, Range(0.1f, 1f)] private float fracaoAteBordaMaisDistante = 0.95f;
    [SerializeField, Min(0.1f)] private float velocidadeDaOnda = 18f;
    [SerializeField, Min(0.1f)] private float alturaDaOnda = 0.45f;
    [SerializeField, Min(0f)] private float danoDaPisada = 16f;

    [Header("Intervalo entre ataques")]
    [SerializeField, Min(0.1f)] private float recuperacao = 1.1f;

    [Header("Estado atual (controlado pelo código)")]
    [SerializeField] private Estado estado;
    [SerializeField] private Ataque ataqueEscolhido;

    private readonly WaitForFixedUpdate passoFisico = new WaitForFixedUpdate();
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
    private bool emInvestida;
    private bool ignorandoColisaoComLing;

    private const float FolgaDeColisao = 0.04f;
    private const float LarguraDaOnda = 0.55f;
    private bool LutaEncerrada => estado == Estado.Encerrado;
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
    }

    private void OnEnable()
    {
        vida.Died += AoDerrotarBoss;
        vida.HealthBarChanged += AoTrocarBarra;
        ling.Died += EncerrarLuta;
        if (!LutaEncerrada || (!vida.IsDead && !ling.IsDead))
        {
            AjustarTamanho(vida.CurrentBar == 2 ? multiplicadorDeTamanhoNaFase2 : 1f);
            colisaoDoBoss.enabled = true;
            estado = Estado.Descansando;
            StartCoroutine(Combater(0.9f));
        }
    }

    private void FixedUpdate()
    {
        if (ignorandoColisaoComLing && !emInvestida && !SobrepostoAoLing())
            IgnorarColisaoComLing(false);
    }

    private IEnumerator Combater(float espera)
    {
        ataqueEscolhido = Ataque.Nenhum;
        if (estado == Estado.Transicao)
            yield return CrescerNaTransicao(espera);
        else
            yield return new WaitForSeconds(espera);
        estado = Estado.Descansando;
        while (!LutaEncerrada)
        {
            float margemParaRecuo = tempoDeRecuo * velocidadeDeRecuo;
            float distanciaAntesDoRecuo = Mathf.Max(distanciaParaBombas - margemParaRecuo, distanciaParaBombas * 0.5f);
            if (vida.CurrentBar == 2)
            {
                ataqueEscolhido = Ataque.BombaIncendiaria;
                yield return ExecutarQuandoPerto(LancarBombas(incendiaria: true), distanciaAntesDoRecuo);
                continue;
            }

            ataqueEscolhido = (Ataque)Random.Range((int)Ataque.Bombas, (int)Ataque.Pisada + 1);
            switch (ataqueEscolhido)
            {
                case Ataque.Bombas:
                    yield return ExecutarQuandoPerto(LancarBombas(incendiaria: false), distanciaAntesDoRecuo);
                    break;
                case Ataque.Combo:
                    float distanciaDeParada = DistanciaDeParadaDoCombo;
                    yield return ExecutarQuandoPerto(ExecutarCombo(), alcanceDoDash + distanciaDeParada, distanciaDeParada);
                    break;
                case Ataque.Investida:
                    yield return ExecutarQuandoPerto(ExecutarInvestida(), distanciaParaInvestida);
                    break;
                case Ataque.Pisada:
                    yield return ExecutarQuandoPerto(ExecutarPisada(), distanciaParaPisada);
                    break;
            }
        }
    }

    private IEnumerator CrescerNaTransicao(float duracao)
    {
        float tamanhoInicial = multiplicadorAtual;
        float inicio = Time.time;
        while (Time.time - inicio < duracao)
        {
            yield return passoFisico;
            float progresso = (Time.time - inicio) / duracao;
            AjustarTamanho(Mathf.SmoothStep(tamanhoInicial, multiplicadorDeTamanhoNaFase2, progresso));
        }

        AjustarTamanho(multiplicadorDeTamanhoNaFase2);
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
        estado = Estado.Descansando;
        ataqueEscolhido = Ataque.Nenhum;
        yield return new WaitForSeconds(recuperacao);
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

        Vector3 origem = corpo.position + Vector3.up * (0.65f * transform.lossyScale.y);
        Vector3 ateLing = VetorHorizontal(corpoDoLing.position - origem);
        float distancia = Mathf.Max(ateLing.magnitude, 0.01f);
        float abertura = Mathf.Asin(Mathf.Clamp(2.15f * prefabBomba.RaioDaExplosao / distancia, 0.28f, 0.85f)) * Mathf.Rad2Deg;
        var bombas = new Bomba[incendiaria ? 1 : 3];
        var alvosDaSalva = incendiaria ? null : new HashSet<PlayerResources>();
        estado = Estado.Atacando;
        CriarEfeitos("Bombas do boss");

        for (int i = 0; i < bombas.Length; i++)
        {
            float angulo = (i - (bombas.Length - 1) * 0.5f) * abertura;
            Vector3 deslocamento = Quaternion.Euler(0f, angulo, 0f) * ateLing;
            Vector3 destino = LimitarAoPiso(origem + deslocamento, 0f);
            destino.y = piso.bounds.max.y;
            bombas[i] = Instantiate(prefabBomba, origem, Quaternion.identity, efeitosDoAtaque.transform);
            if (incendiaria)
                bombas[i].Explodiu += CriarPocaDeFogo;
            bombas[i].Lancar(destino, ling, obstaculos, alvosDaSalva);
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

    private IEnumerator ExecutarCombo()
    {
        estado = Estado.Atacando;
        Vector3 frente = DirecaoDoLing();
        float distanciaAoLing = VetorHorizontal(corpoDoLing.position - corpo.position).magnitude;
        float distanciaDoDash = Mathf.Clamp(distanciaAoLing - DistanciaDeParadaDoCombo, 0f, alcanceDoDash);
        Vector3 destino = corpo.position + frente * distanciaDoDash;

        while ((destino - corpo.position).sqrMagnitude > 0.0025f)
        {
            yield return passoFisico;
            corpo.MoveRotation(Quaternion.LookRotation(frente));
            Vector3 proximo = Vector3.MoveTowards(corpo.position, destino, velocidadeDoDash * Time.fixedDeltaTime);
            if (!Mover(proximo))
                break;
            if (proximo == destino)
                break;
        }

        yield return passoFisico;
        for (int i = 0; i < 3; i++)
        {
            bool ultimo = i == 2;
            float alcance = alcanceDoCombo + (ultimo ? 0.45f : 0f);
            float aviso = i == 0 ? avisoDoPrimeiroGolpe : avisoDoGolpe;
            float semiAngulo = ultimo ? 70f : 50f;
            frente = DirecaoDoLing();
            corpo.rotation = Quaternion.LookRotation(frente);
            Vector3 origem = corpo.position;
            origem.y = piso.bounds.max.y;
            estado = Estado.Preparando;
            CriarEfeitos("Golpe do boss");
            LineRenderer marcador = MarcarArea(origem, alcance, frente, semiAngulo);

            yield return new WaitForSeconds(aviso + (ultimo ? 0.2f : 0f));
            estado = Estado.Atacando;
            MostrarImpacto(marcador);
            TentarAtingirLing(origem, alcance, frente, semiAngulo, ultimo ? danoDoUltimoGolpe : danoDoGolpe);
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
        estado = Estado.Preparando;
        Vector3 origem = corpo.position;
        Vector3 frente = DirecaoDoLing();
        float distancia = DistanciaAteBorda(origem, frente);
        if (distancia < 3f)
        {
            // Se a mira sair pela borda mais próxima, a investida segue pelo centro.
            frente = VetorHorizontal(piso.bounds.center - origem).normalized;
            distancia = DistanciaAteBorda(origem, frente);
        }
        corpo.rotation = Quaternion.LookRotation(frente);

        CriarEfeitos("Investida do boss");
        Vector3 inicioDaFaixa = new Vector3(origem.x, piso.bounds.max.y, origem.z);
        LineRenderer faixa = MarcarTrajeto(inicioDaFaixa, frente, distancia);
        yield return new WaitForSeconds(avisoDaInvestida);

        estado = Estado.Atacando;
        MostrarImpacto(faixa);
        emInvestida = true;
        IgnorarColisaoComLing(true);
        bool passouPeloLing = false;
        for (float avancou = 0f; avancou < distancia;)
        {
            yield return passoFisico;
            float proximo = Mathf.Min(distancia, avancou + velocidadeDaInvestida * Time.fixedDeltaTime);
            if (!passouPeloLing && (SobrepostoAoLing() || VarrerCapsula(corpo.position, frente,
                proximo - avancou, 1 << ling.gameObject.layer, out _)))
            {
                passouPeloLing = true;
                ling.TakeDamage(danoDaInvestida, corpo.position - frente * RaioDoBoss);
                if (LutaEncerrada)
                    yield break;
            }

            corpo.MovePosition(origem + frente * proximo);
            avancou = proximo;
        }

        yield return passoFisico;
        emInvestida = false;
        LimparEfeitos();
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
        yield return new WaitForSeconds(avisoDaPisada);

        estado = Estado.Atacando;
        aviso.enabled = false;
        limiteDaOnda.enabled = false;
        LineRenderer onda = CriarLinha("Onda da pisada", 96);
        Colorir(onda, Vermelho);
        onda.widthMultiplier = LarguraDaOnda;
        bool atingiuLing = false;
        for (float raio = 0f; raio < alcance;)
        {
            yield return passoFisico;
            float proximoRaio = Mathf.Min(alcance, raio + velocidadeDaOnda * Time.fixedDeltaTime);
            PosicionarOndaNoPiso(onda, centro, proximoRaio, ateCentroDoPiso, raioDoPiso);

            Bounds alvo = colisaoDoLing.bounds;
            float distancia = VetorHorizontal(alvo.center - centro).magnitude;
            float margem = Mathf.Max(alvo.extents.x, alvo.extents.z) + LarguraDaOnda * 0.5f;
            if (!atingiuLing && alvo.min.y <= centro.y + alturaDaOnda && alvo.max.y >= centro.y
                && distancia >= raio - margem && distancia <= proximoRaio + margem
                && VetorHorizontal(alvo.center - limites.center).sqrMagnitude <= raioDoPiso * raioDoPiso)
            {
                atingiuLing = ling.TakeDamage(danoDaPisada, centro);
                if (LutaEncerrada)
                    yield break;
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
        Vector3 escala = transform.lossyScale;
        Vector3 centro = origem + corpo.rotation * Vector3.Scale(colisaoDoBoss.center, escala);
        Vector3 eixo = Vector3.up * Mathf.Max(0f, colisaoDoBoss.height * Mathf.Abs(escala.y) * 0.5f - raio);
        return Physics.CapsuleCast(centro - eixo, centro + eixo, raio, direcao, out hit,
            distancia, mascara, QueryTriggerInteraction.Ignore);
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

    private float DistanciaAteBorda(Vector3 origem, Vector3 direcao)
    {
        Bounds limites = piso.bounds;
        float raio = Mathf.Max(0f, RaioDoPiso(limites) - RaioDoBoss - 0.1f);
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

        return ling.TakeDamage(dano, centro);
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

    private LineRenderer MarcarTrajeto(Vector3 inicio, Vector3 frente, float distancia)
    {
        const int segmentosPorPonta = 12;
        LineRenderer linha = CriarLinha("Trajeto da investida", 2 * (segmentosPorPonta + 1));
        inicio.y += 0.05f;
        Vector3 fim = inicio + frente * distancia;
        float raio = RaioDoBoss;
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

    private void AoTrocarBarra(int barra)
    {
        if (ling.IsDead)
            return;

        StopAllCoroutines();
        emInvestida = false;
        LimparEfeitos();
        LimparPocaDeFogo();
        colisaoDoBoss.enabled = true;
        estado = barra == 2 ? Estado.Transicao : Estado.Descansando;
        float espera = barra == 2 ? duracaoDaTransicao : 0.9f;
        if (barra == 2)
            vida.GrantInvulnerability(espera);
        else
            AjustarTamanho(1f);
        StartCoroutine(Combater(espera));
    }

    private void EncerrarLuta()
    {
        estado = Estado.Encerrado;
        ataqueEscolhido = Ataque.Nenhum;
        emInvestida = false;
        StopAllCoroutines();
        LimparEfeitos();
        LimparPocaDeFogo();
    }

    private void LimparEfeitos()
    {
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
        emInvestida = false;
        IgnorarColisaoComLing(false);
        StopAllCoroutines();
        LimparEfeitos();
        LimparPocaDeFogo();
    }

    private void OnDestroy() => Destroy(materialDosAvisos);

    private void OnGUI()
    {
        GUI.Box(new Rect(16f, 16f, 225f, 66f), GUIContent.none);
        string textoVida = vida.IsDead ? "Boss derrotado" : $"Fase {vida.CurrentBar}    HP {Mathf.CeilToInt(vida.CurrentHealth)} / {Mathf.CeilToInt(vida.MaxHealth)}";
        GUI.Label(new Rect(25f, 21f, 207f, 20f), textoVida);
        GUI.Label(new Rect(25f, 54f, 207f, 20f), "Vida do Ling: " + Mathf.CeilToInt(ling.CurrentHealth));

        Color corAnterior = GUI.color;
        GUI.color = new Color(0.18f, 0.18f, 0.18f);
        GUI.DrawTexture(new Rect(25f, 43f, 207f, 7f), Texture2D.whiteTexture);
        GUI.color = vida.CurrentBar == 2 ? Vermelho : Amarelo;
        float proporcao = Mathf.Clamp01(vida.CurrentHealth / vida.MaxHealth);
        GUI.DrawTexture(new Rect(25f, 43f, 207f * proporcao, 7f), Texture2D.whiteTexture);
        GUI.color = corAnterior;

        if (LutaEncerrada)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), ling.IsDead ? "Ling caiu" : "Vitória");
        else if (estado == Estado.Transicao)
            GUI.Box(new Rect(16f, 87f, 225f, 28f), "Início da fase 2");
    }
}
