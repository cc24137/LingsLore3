using System.Collections.Generic;
using UnityEngine;

public sealed partial class BossNetanyahu
{
    private OssoAnimado peito;
    private OssoAnimado cabeca;
    private OssoAnimado bracoEsquerdo;
    private OssoAnimado bracoDireito;
    private OssoAnimado antebracoEsquerdo;
    private OssoAnimado antebracoDireito;
    private OssoAnimado coxaEsquerda;
    private OssoAnimado coxaDireita;
    private OssoAnimado pernaEsquerda;
    private OssoAnimado pernaDireita;
    private Transform quadril;
    private Vector3 posicaoOriginalDoQuadril;
    private Vector3 subidaDoQuadril;
    private float alturaDoQuadril;
    private float cicloDaCorrida;
    private Estado estadoVisual;
    private Ataque ataqueVisual;
    private float inicioDaPose;
    private int golpeDoCombo = -1;
    private int golpeVisual = -1;
    private Transform maoEsquerda;
    private Transform maoDireita;
    private Bomba bombaNaMao;
    private Transform tralaleroNasMaos;
    private Vector3 pegadaDoTralalero;
    private float preparacaoVisualDoAtaque;
    private float momentoDaInvocacaoVisual = float.NegativeInfinity;
    private float embarqueVisual;
    private const float InicioDoLancamentoMassivo = 0.94f;

    private void PrepararAnimacao()
    {
        SkinnedMeshRenderer malha = GetComponentInChildren<SkinnedMeshRenderer>();
        if (malha == null)
            return;

        var ossos = new Dictionary<string, Transform>();
        foreach (Transform osso in malha.bones)
        {
            if (osso != null)
                ossos[osso.name] = osso;
        }

        if (!ossos.TryGetValue("Hips", out quadril) || quadril.parent == null)
            return;

        ossos.TryGetValue("LeftHand", out maoEsquerda);
        ossos.TryGetValue("RightHand", out maoDireita);
        posicaoOriginalDoQuadril = quadril.localPosition;
        subidaDoQuadril = quadril.parent.InverseTransformVector(transform.TransformVector(Vector3.up));
        alturaDoQuadril = Mathf.Abs(transform.InverseTransformVector(quadril.position - quadril.parent.position).y);
        peito = new OssoAnimado(ossos, "Chest", transform);
        cabeca = new OssoAnimado(ossos, "Head", transform);
        bracoEsquerdo = new OssoAnimado(ossos, "LeftUpperArm", transform, "LeftLowerArm");
        bracoDireito = new OssoAnimado(ossos, "RightUpperArm", transform, "RightLowerArm");
        antebracoEsquerdo = new OssoAnimado(ossos, "LeftLowerArm", transform);
        antebracoDireito = new OssoAnimado(ossos, "RightLowerArm", transform);
        coxaEsquerda = new OssoAnimado(ossos, "LeftUpperLeg", transform);
        coxaDireita = new OssoAnimado(ossos, "RightUpperLeg", transform);
        pernaEsquerda = new OssoAnimado(ossos, "LeftLowerLeg", transform);
        pernaDireita = new OssoAnimado(ossos, "RightLowerLeg", transform);
    }

    private void AtualizarAnimacao()
    {
        if (peito == null)
            return;

        int golpe = ataqueEscolhido == Ataque.Combo ? golpeDoCombo : -1;
        if (estadoVisual != estado || ataqueVisual != ataqueEscolhido || golpeVisual != golpe)
        {
            inicioDaPose = Time.time;
            estadoVisual = estado;
            ataqueVisual = ataqueEscolhido;
            golpeVisual = golpe;
        }

        float tempo = Time.time - inicioDaPose;
        bool noChao = etapaDoVoo == EtapaDoVoo.NoChao;
        bool correndo = noChao && (estado == Estado.Aproximando || emInvestida
            || (ataqueEscolhido == Ataque.Combo && estado == Estado.Atacando && golpe < 0));
        if (correndo)
            cicloDaCorrida += Time.deltaTime * 17f;

        float passada = correndo ? Mathf.Sin(cicloDaCorrida) : 0f;
        float respiracao = Mathf.Sin(Time.time * 3f);
        float altura = correndo ? 0.025f - Mathf.Cos(cicloDaCorrida * 2f) * 0.035f : respiracao * 0.009f;
        float inclinacao = correndo ? 13f : respiracao * 1.8f;
        float torcao = passada * 9f;
        float bracoE = passada * 29f;
        float bracoD = -bracoE;
        float cotoveloE = correndo ? -48f : -12f;
        float cotoveloD = cotoveloE;
        float coxaE = -passada * 32f;
        float coxaD = -coxaE;
        float joelhoE = Mathf.Max(0f, -passada) * 51f;
        float joelhoD = Mathf.Max(0f, passada) * 51f;
        float mistura = 1f - Mathf.Exp(-15f * Time.deltaTime);
        float aducaoDosBracos = 0f;
        float rolagemDoTronco = 0f;

        if (aviao != null && etapaDoVoo != EtapaDoVoo.Caindo)
        {
            float sentado = Mathf.SmoothStep(0f, 1f, embarqueVisual);
            bool emCruzeiro = etapaDoVoo == EtapaDoVoo.Cruzeiro;
            bool emRasante = etapaDoVoo == EtapaDoVoo.Rasante;
            float ajusteDosBracos = emCruzeiro ? Mathf.Sin(anguloDaOrbita * 2f) * 5f : 0f;
            float inclinacaoDoAviao = Vector3.Dot(aviao.transform.forward, Vector3.up);
            altura = Mathf.Lerp(altura, -0.62f, sentado);
            inclinacao = Mathf.Lerp(inclinacao, (emRasante ? 26f : 12f) - inclinacaoDoAviao * 20f, sentado);
            rolagemDoTronco = emCruzeiro ? (8f + ajusteDosBracos * 0.45f) * sentado : 0f;
            bracoE = Mathf.Lerp(bracoE, -53f - ajusteDosBracos, sentado);
            bracoD = Mathf.Lerp(bracoD, -53f + ajusteDosBracos, sentado);
            cotoveloE = Mathf.Lerp(cotoveloE, -44f + ajusteDosBracos, sentado);
            cotoveloD = Mathf.Lerp(cotoveloD, -44f - ajusteDosBracos, sentado);
            aducaoDosBracos = 13f * sentado;
            coxaE = coxaD = -78f * sentado;
            joelhoE = joelhoD = 97f * sentado;
        }

        if (ataqueEscolhido == Ataque.Combo && golpe >= 0 &&
            (estado == Estado.Preparando || estado == Estado.Atacando))
        {
            bool direita = golpe != 1;
            float aviso = golpe == 0 ? avisoDoPrimeiroGolpe : avisoDoGolpe + (golpe == 2 ? 0.2f : 0f);
            float preparacao = Mathf.SmoothStep(0f, 1f, tempo / aviso);
            bool impacto = estado == Estado.Atacando;
            float forca = golpe == 2 ? 1.15f : 1f;
            float soco = impacto ? -83f * forca : Mathf.Lerp(-15f, 30f, preparacao);
            float cotovelo = impacto ? -6f : Mathf.Lerp(-30f, -88f, preparacao);
            bracoE = direita ? -27f : soco;
            bracoD = direita ? soco : -27f;
            cotoveloE = direita ? -62f : cotovelo;
            cotoveloD = direita ? cotovelo : -62f;
            torcao = (direita ? -1f : 1f) * (impacto ? 33f : -25f * preparacao);
            inclinacao = impacto ? 15f : -7f * preparacao;
            if (impacto)
                mistura = 1f - Mathf.Exp(-65f * Time.deltaTime);
        }
        else if (ataqueEscolhido == Ataque.Pisada && estado == Estado.Preparando)
        {
            float levantamento = Mathf.SmoothStep(0f, 1f, tempo / (avisoDaPisada * 0.75f));
            coxaD = -53f * levantamento;
            joelhoD = 68f * levantamento;
            inclinacao = -9f * levantamento;
            bracoE = -28f * levantamento;
            bracoD = 24f * levantamento;
        }
        else if (ataqueEscolhido == Ataque.Pisada && estado == Estado.Atacando)
        {
            float impacto = 1f - Mathf.SmoothStep(0f, 1f, tempo / 0.30f);
            coxaD = -9f * impacto;
            joelhoD = 14f * impacto;
            inclinacao = 20f * impacto;
            altura = -0.07f * impacto;
            mistura = 1f - Mathf.Exp(-65f * Time.deltaTime);
        }
        else if (bombaNaMao != null || (estado == Estado.Atacando &&
            (ataqueEscolhido == Ataque.Bombas || ataqueEscolhido == Ataque.BombaIncendiaria || ataqueEscolhido == Ataque.BombaMassiva)))
        {
            bool massiva = ataqueEscolhido == Ataque.BombaMassiva;
            bool soltou = bombaNaMao == null;
            float inicioDoLancamento = massiva ? InicioDoLancamentoMassivo : 0.58f;
            float lancamento = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inicioDoLancamento, 1f, preparacaoVisualDoAtaque));
            float retorno = soltou ? Mathf.SmoothStep(0f, 1f, tempo / 0.35f) : 0f;
            float preparo = Mathf.SmoothStep(0f, 1f, preparacaoVisualDoAtaque / (massiva ? 0.24f : 0.58f));
            float bracoEEmRepouso = bracoE;
            float bracoDEmRepouso = bracoD;
            float cotoveloEEmRepouso = cotoveloE;
            float cotoveloDEmRepouso = cotoveloD;
            float inclinacaoEmRepouso = inclinacao;
            bracoD = Mathf.Lerp(Mathf.Lerp(bracoDEmRepouso, massiva ? -145f : -119f, preparo), -74f, lancamento);
            cotoveloD = Mathf.Lerp(Mathf.Lerp(cotoveloDEmRepouso, massiva ? -25f : -71f, preparo), -8f, lancamento);
            if (massiva)
            {
                bracoE = bracoD;
                cotoveloE = cotoveloD;
            }
            else if (noChao)
            {
                bracoE = Mathf.Lerp(-28f * preparo, 14f, lancamento);
                cotoveloE = Mathf.Lerp(cotoveloEEmRepouso, -35f, preparo);
            }
            float mira = !noChao ? AnguloDaMiraDaBomba(massiva) : 0f;
            torcao = mira * preparo + (massiva ? 0f : Mathf.Lerp(24f * preparo, -22f, lancamento));
            inclinacao = Mathf.Lerp(Mathf.Lerp(inclinacaoEmRepouso, -12f, preparo), massiva ? 28f : 19f, lancamento);
            bracoE = Mathf.Lerp(bracoE, bracoEEmRepouso, retorno);
            bracoD = Mathf.Lerp(bracoD, bracoDEmRepouso, retorno);
            cotoveloE = Mathf.Lerp(cotoveloE, cotoveloEEmRepouso, retorno);
            cotoveloD = Mathf.Lerp(cotoveloD, cotoveloDEmRepouso, retorno);
            torcao *= 1f - retorno;
            inclinacao = Mathf.Lerp(inclinacao, inclinacaoEmRepouso, retorno);
            float pesoDoArremesso = preparo * (1f - retorno);
            aducaoDosBracos = Mathf.Lerp(aducaoDosBracos, massiva ? 15f : 0f, pesoDoArremesso);
            rolagemDoTronco *= 1f - pesoDoArremesso;
            mistura = 1f - Mathf.Exp(-42f * Time.deltaTime);
        }
        else if (tralaleroNasMaos != null)
        {
            bool girando = estado == Estado.Atacando;
            float saque = Mathf.SmoothStep(0f, 1f, preparacaoVisualDoAtaque);
            float giro = girando ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(tempo / duracaoDoGiro)) : 0f;
            bracoE = Mathf.Lerp(-26f, -76f, saque) - 9f * giro;
            bracoD = Mathf.Lerp(-40f, -76f, saque) - 9f * giro;
            cotoveloE = Mathf.Lerp(-72f, -23f, saque);
            cotoveloD = cotoveloE;
            aducaoDosBracos = Mathf.Lerp(25f, 12f, saque);
            inclinacao = Mathf.Lerp(16f, 8f, saque) + 8f * giro;
            torcao = girando ? Mathf.Lerp(-18f, 17f, Mathf.Clamp01(tempo / duracaoDoGiro)) : -18f * saque;
            altura = -0.04f * saque;
            coxaE = coxaD = -8f * saque;
            joelhoE = joelhoD = 16f * saque;
            mistura = 1f - Mathf.Exp(-35f * Time.deltaTime);
        }
        else if (ataqueEscolhido == Ataque.ComandarMinis && Time.time - momentoDaInvocacaoVisual < 0.65f)
        {
            float tempoDaInvocacao = Time.time - momentoDaInvocacaoVisual;
            float preparar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.50f, -0.18f, tempoDaInvocacao));
            float erguer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.18f, 0.12f, tempoDaInvocacao));
            float baixar = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.65f, tempoDaInvocacao));
            float gesto = preparar * (1f - baixar);
            bracoD = Mathf.Lerp(bracoD, Mathf.Lerp(-18f, -133f, erguer), gesto);
            cotoveloD = Mathf.Lerp(cotoveloD, Mathf.Lerp(-42f, -17f, erguer), gesto);
            inclinacao = Mathf.Lerp(inclinacao, Mathf.Lerp(13f, -9f, erguer), gesto);
            torcao = 12f * gesto;
            mistura = 1f - Mathf.Exp(-33f * Time.deltaTime);
        }

        Vector3 posicao = posicaoOriginalDoQuadril + subidaDoQuadril * (alturaDoQuadril * altura);
        quadril.localPosition = Vector3.Lerp(quadril.localPosition, posicao, mistura);
        peito.Posar(inclinacao, torcao, mistura, rolagemDoTronco);
        cabeca.Posar(-inclinacao * 0.44f, -torcao * 0.55f, mistura, -rolagemDoTronco * 0.5f);
        bracoEsquerdo.Posar(bracoE, -bracoEsquerdo.Lado * aducaoDosBracos, mistura);
        bracoDireito.Posar(bracoD, -bracoDireito.Lado * aducaoDosBracos, mistura);
        antebracoEsquerdo.Posar(cotoveloE, 0f, mistura);
        antebracoDireito.Posar(cotoveloD, 0f, mistura);
        coxaEsquerda.Posar(coxaE, 0f, mistura);
        coxaDireita.Posar(coxaD, 0f, mistura);
        pernaEsquerda.Posar(joelhoE, 0f, mistura);
        pernaDireita.Posar(joelhoD, 0f, mistura);
    }

    private bool TemDuasMaos => maoEsquerda != null && maoDireita != null;
    private Vector3 CentroDasMaos => (maoEsquerda.position + maoDireita.position) * 0.5f;

    private float AnguloDaMiraDaBomba(bool massiva)
    {
        Vector3 alvo = massiva ? piso.bounds.center : corpoDoLing.position;
        Vector3 direcao = VetorHorizontal(alvo - transform.position);
        return Mathf.Clamp(Vector3.SignedAngle(transform.forward, direcao, Vector3.up), -72f, 72f);
    }

    private void AtualizarObjetosSegurados()
    {
        if (bombaNaMao != null && maoDireita != null)
        {
            bool massiva = ataqueEscolhido == Ataque.BombaMassiva;
            if (massiva && TemDuasMaos)
            {
                float empurrao = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(InicioDoLancamentoMassivo, 1f, preparacaoVisualDoAtaque));
                Vector3 frente = etapaDoVoo == EtapaDoVoo.NoChao ? transform.forward
                    : Quaternion.AngleAxis(AnguloDaMiraDaBomba(true), Vector3.up) * transform.forward;
                Vector3 apoio = Vector3.Slerp(Vector3.up, frente, empurrao);
                bombaNaMao.transform.position = CentroDasMaos + apoio * (bombaNaMao.transform.localScale.x * 0.5f);
            }
            else if (!massiva)
            {
                Vector3 palma = maoDireita.position + (maoDireita.position - maoDireita.parent.position) * 0.28f;
                bombaNaMao.transform.position = palma;
            }
        }

        if (tralaleroNasMaos != null && TemDuasMaos && (robo == null || !robo.activeSelf))
        {
            tralaleroNasMaos.parent.rotation = transform.rotation;
            Vector3 origemDaPegada = tralaleroNasMaos.TransformPoint(pegadaDoTralalero);
            tralaleroNasMaos.position += CentroDasMaos - origemDaPegada;
        }
    }

    private sealed class OssoAnimado
    {
        private readonly Transform osso;
        private readonly Quaternion baseNoPai;
        private readonly Quaternion repouso;
        public float Lado { get; }

        public OssoAnimado(Dictionary<string, Transform> ossos, string nome, Transform referencia, string filho = null)
        {
            if (!ossos.TryGetValue(nome, out osso) || osso.parent == null)
            {
                osso = null;
                return;
            }

            Quaternion inversoDoPai = Quaternion.Inverse(osso.parent.rotation);
            baseNoPai = inversoDoPai * referencia.rotation;
            Lado = Mathf.Sign(Vector3.Dot(osso.position - referencia.position, referencia.right));
            repouso = osso.localRotation;
            if (filho != null && ossos.TryGetValue(filho, out Transform extremidade))
            {
                Vector3 atual = inversoDoPai * (extremidade.position - osso.position);
                Vector3 lateral = Vector3.Project(atual.normalized, baseNoPai * Vector3.right);
                Vector3 destino = baseNoPai * Vector3.down + lateral * 0.17f;
                repouso = Quaternion.FromToRotation(atual, destino) * repouso;
            }
        }

        public void Posar(float inclinacao, float torcao, float mistura, float rolagem = 0f)
        {
            if (osso == null)
                return;

            Quaternion giro = baseNoPai * Quaternion.Euler(inclinacao, torcao, rolagem) * Quaternion.Inverse(baseNoPai);
            osso.localRotation = Quaternion.Slerp(osso.localRotation, giro * repouso, mistura);
        }
    }
}
