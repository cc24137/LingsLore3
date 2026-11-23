using System.Collections;
using UnityEngine;

public sealed partial class BossNetanyahu
{
    private enum EtapaDoVoo { NoChao, Decolagem, Cruzeiro, Rasante, Caindo }

    [Header("Modo avião")]
    [SerializeField] private AviaoBigYahu prefabAviao;
    [SerializeField, Min(0f)] private float esperaAntesDoPrimeiroVoo = 6f;
    [SerializeField, Min(0f)] private float intervaloEntreVoos = 12f;
    [SerializeField, Min(0.1f)] private float alturaDeVoo = 15.5f;
    [SerializeField, Min(0.1f)] private float raioDaOrbita = 14.7f;
    [SerializeField, Min(0.1f)] private float velocidadeDeVoo = 12f;
    [SerializeField, Min(0.1f)] private float alturaMinimaDoGarfo = 10f;
    [SerializeField] private MetralhadoraBigYahu prefabMetralhadora;

    [Header("Rasante")]
    [SerializeField, Min(0.1f)] private float avisoDoRasante = 1.1f;
    [SerializeField, Min(0.1f)] private float velocidadeDoRasante = 42f;
    [SerializeField, Min(0.1f)] private float raioDoRasante = 2.2f;
    [SerializeField, Min(0f)] private float danoDoRasante = 24f;

    private const int HashisParaDerrubar = 2;
    private const float DuracaoDoStun = 3f;

    private AviaoBigYahu aviao;
    private EtapaDoVoo etapaDoVoo;
    private PlayerMelee garfoDoLing;
    private PlayerHashi armaDeHashi;
    private Vector3 posicaoAntesDeVoar;
    private Vector3 centroDaOrbita;
    private float raioAtualDaOrbita;
    private float anguloDaOrbita;
    private float podeVoarApos;
    private int hashisNoAviao;
    private LineRenderer espiralDoStun;
    private Camera cameraDaCena;

    private void ConectarAcertosNoAviao()
    {
        if (ling.TryGetComponent(out garfoDoLing))
            garfoDoLing.HitLanded += AoAcertarAviaoComGarfo;
        if (ling.TryGetComponent(out armaDeHashi))
            armaDeHashi.TargetHit += AoAcertarAviaoComHashi;
    }

    private void DesconectarAcertosNoAviao()
    {
        if (garfoDoLing != null)
            garfoDoLing.HitLanded -= AoAcertarAviaoComGarfo;
        if (armaDeHashi != null)
            armaDeHashi.TargetHit -= AoAcertarAviaoComHashi;
    }

    private void AoAcertarAviaoComGarfo(EnemyHealth alvo, PlayerMelee.AttackType ataque)
    {
        if (aviao != null && alvo == aviao && corpoDoLing.position.y >= piso.bounds.max.y + alturaMinimaDoGarfo)
            DerrubarAviao();
    }

    private void AoAcertarAviaoComHashi(EnemyHealth alvo)
    {
        if (aviao != null && alvo == aviao && ++hashisNoAviao >= HashisParaDerrubar)
            DerrubarAviao();
    }

    private IEnumerator ExecutarModoAviao()
    {
        ataqueEscolhido = Ataque.ModoAviao;
        estado = Estado.Preparando;
        posicaoAntesDeVoar = corpo.position;
        float pisoY = piso.bounds.max.y;
        var origem = new Vector3(corpo.position.x, pisoY + 1.1f, corpo.position.z);
        aviao = Instantiate(prefabAviao, origem, Quaternion.identity);
        if (!aviao.EstaMontado)
        {
            Debug.LogError("O modo avião precisa do modelo do Bombombini Gusini.", this);
            LimparModoAviao(voltarAoChao: false);
            podeVoarApos = Time.time + intervaloEntreVoos;
            yield return new WaitForSeconds(recuperacao);
            yield break;
        }

        etapaDoVoo = EtapaDoVoo.Decolagem;
        colisaoDoBoss.enabled = false;
        IgnorarColisaoComLing(false);
        Bounds arena = piso.bounds;
        centroDaOrbita = new Vector3(arena.center.x, pisoY + alturaDeVoo, arena.center.z);
        raioAtualDaOrbita = Mathf.Clamp(raioDaOrbita, 0.1f, Mathf.Max(0.1f, RaioDoPiso(arena) - 3.5f));

        Vector3 radial = VetorHorizontal(origem - centroDaOrbita);
        anguloDaOrbita = radial.sqrMagnitude > 0.001f ? Mathf.Atan2(radial.z, radial.x) : 0f;
        Vector3 acima = new Vector3(origem.x, centroDaOrbita.y, origem.z);
        yield return TransladarAviao(acima, aviao.transform.rotation, 1.25f, embarcando: true);
        yield return TransladarAviao(PosicaoDaOrbita(), RotacaoDaOrbita(), 0.75f);
        etapaDoVoo = EtapaDoVoo.Cruzeiro;
        aviao.Hitbox.enabled = true;

        while (!LutaEncerrada)
        {
            if (prefabMetralhadora != null)
            {
                ataqueEscolhido = Ataque.Rajada;
                yield return ExecutarRajada();
                yield return new WaitForSeconds(recuperacao);
            }

            if (PodeUsarBombaMassiva)
            {
                ataqueEscolhido = Ataque.BombaMassiva;
                yield return ExecutarBombaMassiva();
            }
            else if (prefabBomba != null && prefabPocaDeFogo != null)
            {
                ataqueEscolhido = Ataque.BombaIncendiaria;
                yield return ArremessarBombas(incendiaria: true);
            }
            yield return new WaitForSeconds(recuperacao);

            ataqueEscolhido = Ataque.Rasante;
            yield return ExecutarRasante();
            yield return new WaitForSeconds(recuperacao);

            minis.RemoveAll(mini => mini == null || !mini.EstaAtivo);
            if (prefabMiniTralalero != null && minis.Count == 0 && Time.time >= proximaInvocacao)
            {
                ataqueEscolhido = Ataque.ComandarMinis;
                yield return ComandarMinis();
                yield return new WaitForSeconds(recuperacao);
            }
        }
    }

    private IEnumerator ExecutarRajada()
    {
        estado = Estado.Preparando;
        CriarEfeitos("Rajada do Big Yahu");
        MetralhadoraBigYahu arma = Instantiate(prefabMetralhadora, efeitosDoAtaque.transform);
        LayerMask barreiras = obstaculos.value | (1 << piso.gameObject.layer);
        yield return arma.SacarEMirar(transform, ling, colisaoDoLing, barreiras);

        estado = Estado.Atacando;
        yield return arma.Disparar();
        LimparEfeitos();
    }

    private Vector3 PosicaoDaOrbita() => centroDaOrbita +
        new Vector3(Mathf.Cos(anguloDaOrbita), 0f, Mathf.Sin(anguloDaOrbita)) * raioAtualDaOrbita;

    private Quaternion RotacaoDaOrbita() =>
        Quaternion.LookRotation(new Vector3(-Mathf.Sin(anguloDaOrbita), 0f, Mathf.Cos(anguloDaOrbita)));

    private void AtualizarOrbita()
    {
        anguloDaOrbita = Mathf.Repeat(anguloDaOrbita + velocidadeDeVoo / raioAtualDaOrbita * Time.fixedDeltaTime, 2f * Mathf.PI);
        PosicionarAviao(PosicaoDaOrbita(), RotacaoDaOrbita());
    }

    private void PosicionarAviao(Vector3 posicao, Quaternion rotacao, float embarque = 1f)
    {
        aviao.transform.SetPositionAndRotation(posicao, rotacao);
        float assento = alturaInicialDaBaseAoPivo * multiplicadorAtual + aviao.Hitbox.size.y * 0.35f;
        Vector3 posicaoNoAssento = posicao + Vector3.up * assento;
        corpo.MovePosition(Vector3.Lerp(posicaoAntesDeVoar, posicaoNoAssento, embarque));
        corpo.MoveRotation(Quaternion.LookRotation(rotacao * Vector3.forward));
    }

    private IEnumerator TransladarAviao(Vector3 destino, Quaternion rotacao, float duracao, bool embarcando = false)
    {
        Vector3 origem = aviao.transform.position;
        Quaternion orientacaoInicial = aviao.transform.rotation;
        for (float tempo = 0f; tempo < duracao;)
        {
            yield return passoFisico;
            tempo += Time.fixedDeltaTime;
            float progresso = Mathf.SmoothStep(0f, 1f, tempo / duracao);
            float embarque = embarcando ? Mathf.Clamp01(tempo / 0.45f) : 1f;
            PosicionarAviao(Vector3.Lerp(origem, destino, progresso),
                Quaternion.Slerp(orientacaoInicial, rotacao, progresso), embarque);
        }
    }

    private IEnumerator ExecutarRasante()
    {
        if (!EscolherTrajetoDoRasante(out Vector3 entrada, out Vector3 saida, out Vector3 frente))
            yield break;

        etapaDoVoo = EtapaDoVoo.Rasante;
        estado = Estado.Preparando;
        Quaternion rotacao = Quaternion.LookRotation(frente);
        CriarEfeitos("Aviso do rasante");
        Vector3 aviso = new Vector3(entrada.x, piso.bounds.max.y, entrada.z);
        LineRenderer faixa = MarcarTrajeto(aviso, frente, Vector3.Distance(entrada, saida), raioDoRasante);
        var entradaAlta = new Vector3(entrada.x, centroDaOrbita.y, entrada.z);
        yield return TransladarAviao(entradaAlta, rotacao, avisoDoRasante);
        yield return TransladarAviao(entrada, rotacao, 0.32f);

        estado = Estado.Atacando;
        MostrarImpacto(faixa);
        bool acertou = false;
        Vector3 atual = entrada;
        while ((saida - atual).sqrMagnitude > 0.0001f)
        {
            yield return passoFisico;
            Vector3 seguinte = Vector3.MoveTowards(atual, saida, velocidadeDoRasante * Time.fixedDeltaTime);
            if (!acertou)
            {
                Vector3 centroDoDano = atual + Vector3.down * 0.35f;
                Vector3 volume = new Vector3(raioDoRasante, 0.8f, 0.65f);
                bool acertouLing = Physics.BoxCast(centroDoDano, volume, frente, rotacao,
                    Vector3.Distance(atual, seguinte), 1 << ling.gameObject.layer, QueryTriggerInteraction.Ignore);
                Bounds alvo = colisaoDoLing.bounds;
                Vector3 delta = alvo.center - centroDoDano;
                Vector3 lateral = Vector3.Cross(Vector3.up, frente);
                acertouLing |= Mathf.Abs(delta.y) <= volume.y + alvo.extents.y &&
                    Mathf.Abs(Vector3.Dot(delta, lateral)) <= volume.x + alvo.extents.x &&
                    Mathf.Abs(Vector3.Dot(delta, frente)) <= volume.z + alvo.extents.z;
                if (acertouLing)
                    acertou = ling.TakeDamage(danoDoRasante, atual);
                if (LutaEncerrada)
                    yield break;
            }

            PosicionarAviao(seguinte, rotacao);
            atual = seguinte;
        }

        LimparEfeitos();
        var saidaAlta = new Vector3(saida.x, centroDaOrbita.y, saida.z);
        yield return TransladarAviao(saidaAlta, rotacao, 0.5f);
        Vector3 radial = VetorHorizontal(saida - centroDaOrbita);
        anguloDaOrbita = Mathf.Atan2(radial.z, radial.x);
        yield return TransladarAviao(PosicaoDaOrbita(), RotacaoDaOrbita(), 0.45f);
        etapaDoVoo = EtapaDoVoo.Cruzeiro;
    }

    private bool EscolherTrajetoDoRasante(out Vector3 entrada, out Vector3 saida, out Vector3 frente)
    {
        Vector3 alvo = LimitarAoPiso(corpoDoLing.position, 4f);
        float altura = piso.bounds.max.y + 1.55f;
        alvo.y = altura;
        Vector3 mira = VetorHorizontal(alvo - aviao.transform.position).normalized;
        if (mira.sqrMagnitude < 0.001f)
            mira = aviao.transform.forward;

        Vector3 paraOCentro = VetorHorizontal(piso.bounds.center - alvo);
        float raioLivre = Mathf.Max(raioDoRasante, aviao.Hitbox.size.x * 0.5f);
        for (int desvio = 0; desvio <= 6; desvio++)
        {
            Vector3 ponto = alvo + Vector3.ClampMagnitude(paraOCentro, desvio * 2f);
            for (int i = 0; i < 16; i++)
            {
                float angulo = (i % 2 == 0 ? -1f : 1f) * ((i + 1) / 2) * 22.5f;
                frente = Quaternion.Euler(0f, angulo, 0f) * mira;
                float antes = Mathf.Min(11f, DistanciaAteBorda(ponto, -frente) - raioDoRasante);
                float depois = Mathf.Min(11f, DistanciaAteBorda(ponto, frente) - raioDoRasante);
                if (antes < 2f || depois < 2f)
                    continue;

                entrada = ponto - frente * antes;
                saida = ponto + frente * depois;
                if (!Physics.CheckSphere(entrada, raioLivre, obstaculos, QueryTriggerInteraction.Ignore) &&
                    !Physics.SphereCast(entrada, raioLivre, frente, out _, antes + depois, obstaculos, QueryTriggerInteraction.Ignore))
                    return true;
            }
        }

        entrada = saida = frente = default;
        return false;
    }

    private void DerrubarAviao()
    {
        if (etapaDoVoo != EtapaDoVoo.Cruzeiro && etapaDoVoo != EtapaDoVoo.Rasante)
            return;

        etapaDoVoo = EtapaDoVoo.Caindo;
        aviao.Hitbox.enabled = false;
        StopAllCoroutines();
        LimparEfeitos();
        foreach (MiniTralalero mini in minis)
        {
            if (mini != null)
                mini.CancelarOrdem();
        }
        StartCoroutine(CairEAtordoar());
    }

    private IEnumerator CairEAtordoar()
    {
        Vector3 destino = EncontrarPouso(aviao.transform.position);
        Vector3 impacto = new Vector3(destino.x, piso.bounds.max.y + 0.85f, destino.z);
        Quaternion inclinacao = aviao.transform.rotation * Quaternion.Euler(23f, 0f, 28f);
        yield return TransladarAviao(impacto, inclinacao, 0.8f);
        yield return passoFisico;

        LimparModoAviao(voltarAoChao: false);
        corpo.position = destino;
        corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());
        colisaoDoBoss.enabled = true;
        estado = Estado.Atordoado;
        ataqueEscolhido = Ataque.Nenhum;
        CriarEspiralDoStun();
        yield return new WaitForSeconds(DuracaoDoStun);
        LimparEspiralDoStun();

        podeVoarApos = Time.time + intervaloEntreVoos;
        yield return Combater(0f);
    }

    private Vector3 EncontrarPouso(Vector3 pertoDe)
    {
        Vector3 baseDoPouso = LimitarAoPiso(pertoDe, RaioDoBoss + 0.1f);
        baseDoPouso.y = piso.bounds.max.y + alturaInicialDaBaseAoPivo * multiplicadorAtual;
        float raio = RaioDoBoss;
        float eixo = Mathf.Max(0f, colisaoDoBoss.height * transform.lossyScale.y * 0.5f - raio);
        int mascara = obstaculos.value | (1 << ling.gameObject.layer);
        for (int i = 0; i < 33; i++)
        {
            float distancia = i == 0 ? 0f : ((i - 1) / 8 + 1) * 1.5f;
            Vector3 desvio = Quaternion.Euler(0f, (i - 1) * 45f, 0f) * Vector3.forward * distancia;
            Vector3 ponto = LimitarAoPiso(baseDoPouso + desvio, raio + 0.1f);
            Vector3 centro = ponto + Vector3.Scale(colisaoDoBoss.center, transform.lossyScale);
            if (!Physics.CheckCapsule(centro - Vector3.up * eixo, centro + Vector3.up * eixo,
                raio, mascara, QueryTriggerInteraction.Ignore))
                return ponto;
        }

        return posicaoAntesDeVoar;
    }

    private void CriarEspiralDoStun()
    {
        var icone = new GameObject("Espiral de atordoamento");
        espiralDoStun = icone.AddComponent<LineRenderer>();
        espiralDoStun.useWorldSpace = false;
        espiralDoStun.positionCount = 48;
        espiralDoStun.widthMultiplier = 0.085f;
        espiralDoStun.numCapVertices = 4;
        ConfigurarVisual(espiralDoStun, new Color(0.72f, 0.23f, 1f));
        for (int i = 0; i < espiralDoStun.positionCount; i++)
        {
            float t = i / (float)(espiralDoStun.positionCount - 1);
            float angulo = t * 4.5f * Mathf.PI;
            espiralDoStun.SetPosition(i, new Vector3(Mathf.Cos(angulo), Mathf.Sin(angulo), 0f) * t * 0.47f);
        }

        cameraDaCena = Camera.main;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (espiralDoStun == null)
            return;

        Vector3 acimaDaCabeca = new Vector3(transform.position.x, colisaoDoBoss.bounds.max.y + 0.65f, transform.position.z);
        Quaternion rotacao = cameraDaCena != null ? cameraDaCena.transform.rotation : Quaternion.identity;
        rotacao *= Quaternion.Euler(0f, 0f, Time.time * 130f);
        espiralDoStun.transform.SetPositionAndRotation(acimaDaCabeca, rotacao);
    }

    private void LimparEspiralDoStun()
    {
        if (espiralDoStun == null)
            return;

        Destroy(espiralDoStun.gameObject);
        espiralDoStun = null;
    }

    private void LimparModoAviao(bool voltarAoChao)
    {
        if (aviao != null)
        {
            aviao.gameObject.SetActive(false);
            Destroy(aviao.gameObject);
            aviao = null;
        }

        if (voltarAoChao && etapaDoVoo != EtapaDoVoo.NoChao)
        {
            corpo.position = posicaoAntesDeVoar;
            colisaoDoBoss.enabled = true;
        }

        etapaDoVoo = EtapaDoVoo.NoChao;
        hashisNoAviao = 0;
        LimparEspiralDoStun();
    }
}
