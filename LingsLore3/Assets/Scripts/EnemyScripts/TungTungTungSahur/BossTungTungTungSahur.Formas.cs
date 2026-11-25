using System.Collections.Generic;
using UnityEngine;

public sealed partial class BossTungTungTungSahur
{
    private const float DuracaoDaTransformacao = 1.7f;
    private static readonly Color Carmesim = new Color(1f, 0.20f, 0.10f);
    private static readonly Color Dourado = new Color(1f, 0.72f, 0.25f);
    private static readonly int Emissao = Shader.PropertyToID("_EmissionColor");
    private static readonly int TexturaDaEmissao = Shader.PropertyToID("_EmissionMap");

    private readonly List<Object> recursosVisuais = new List<Object>();
    private int faseVisual = 1;
    private Material madeiraOriginal;
    private Material corpoDemonio;
    private Material corpoAnjo;
    private Material madeiraEscura;
    private Material madeiraClara;
    private Mesh malhaCurvada;
    private GameObject formaDemonio;
    private GameObject formaAnjo;
    private Transform asaEsquerda;
    private Transform asaDireita;

    private void AoMudarDeFase(int novaFase)
    {
        CancelarAtaque();
        proximaInvocacao = Time.fixedTime + 2.5f;
        proximoMeteoro = Time.fixedTime + 5f;
        colisor.enabled = true;
        if (novaFase == 1 || ling == null || ling.IsDead || !ling.isActiveAndEnabled)
        {
            AplicarForma(novaFase);
            estado = Estado.Aproximando;
            return;
        }

        vida.GrantInvulnerability(DuracaoDaTransformacao + Time.fixedDeltaTime);
        EntrarNoEstado(Estado.Transformando, DuracaoDaTransformacao);
        EfeitosSonoros.Tocar(novaFase == 3 ? Som.SomSagrado : Som.Fogo, corpo.position);
        AtualizarTransformacao();
    }

    private void AtualizarTransformacao()
    {
        float progresso = ProgressoDoEstado;
        if (progresso >= 0.4f && faseVisual != vida.CurrentBar)
            AplicarForma(vida.CurrentBar);

        Vector3 centro = corpo.position;
        centro.y = piso.bounds.max.y;
        bool bencao = vida.CurrentBar == 3;
        if (bencao)
            centro.y += Mathf.Lerp(10f, 0.15f, Mathf.SmoothStep(0f, 1f, progresso));
        float raio = bencao ? Mathf.Lerp(4.5f, 1.6f, progresso) : Mathf.Lerp(1.6f, 6f, progresso);
        PosicionarContorno(aviso, centro, raio, Vector3.forward, 180f);
        materialDoAviso.SetColor(CorBase, bencao ? Dourado : Carmesim);
        aviso.widthMultiplier = 0.18f;
        aviso.enabled = true;
        if (progresso >= 1f)
            FinalizarAtaque(Estado.RecuperandoDoSalto, 0.35f);
    }

    private void AplicarForma(int fase)
    {
        faseVisual = fase;
        if (TryGetComponent(out DamageFlash flash))
            flash.Clear();
        if (visualDoCorpo == null || madeiraOriginal == null)
            return;

        visualDoCorpo.sharedMaterial = fase switch
        {
            2 => corpoDemonio,
            3 => corpoAnjo,
            _ => madeiraOriginal
        };
        formaDemonio.SetActive(fase == 2);
        formaAnjo.SetActive(fase == 3);
    }

    private void PrepararVisuaisDasFases()
    {
        if (visualDoCorpo == null || visualDoCorpo.sharedMaterial == null)
            return;

        madeiraOriginal = visualDoCorpo.sharedMaterial;
        corpoDemonio = CriarMaterial("Madeira rubra", new Color(1f, 0.23f, 0.20f), manterTextura: true);
        corpoAnjo = CriarMaterial("Madeira marfim", new Color(0.85f, 0.91f, 1f),
            new Color(0.52f, 0.51f, 0.48f), manterTextura: true);
        madeiraEscura = CriarMaterial("Cerne da arma", new Color(0.22f, 0.07f, 0.025f));
        madeiraClara = CriarMaterial("Madeira talhada", new Color(0.57f, 0.30f, 0.12f));
        Material chifre = CriarMaterial("Chifres rubros", new Color(0.74f, 0.035f, 0.035f),
            new Color(0.2f, 0.012f, 0.008f));
        Material penas = CriarMaterial("Penas marfim", new Color(1f, 0.96f, 0.86f),
            new Color(0.14f, 0.13f, 0.11f));
        Material ouro = CriarMaterial("Ouro das asas", new Color(0.93f, 0.60f, 0.18f),
            new Color(0.23f, 0.11f, 0.016f));
        Material luzDoHalo = CriarMaterial("Luz do halo", Dourado, new Color(1.4f, 0.65f, 0.12f));

        malhaCurvada = CriarMalhaDeAneis("Chifre curvo", 12, 16, (s, a) =>
        {
            float raio = 0.16f * Mathf.Pow(1f - s, 0.85f);
            return new Vector3(0.24f * s * s + raio * Mathf.Cos(a), 0.75f * s, -0.30f * s * s + raio * Mathf.Sin(a));
        }, fechado: false);

        formaDemonio = CriarFilho("Forma Demônio", modelo.transform).gameObject;
        for (int lado = -1; lado <= 1; lado += 2)
        {
            Transform peca = AdicionarMalha("Chifre", formaDemonio.transform, malhaCurvada, chifre);
            peca.localPosition = new Vector3(lado * 0.29f, 3.52f, -0.14f);
            peca.localRotation = Quaternion.Euler(-10f, 0f, -lado * 13f);
            peca.localScale = new Vector3(lado, 1f, 1f);
        }
        formaDemonio.SetActive(false);

        formaAnjo = CriarFilho("Forma God", modelo.transform).gameObject;
        Mesh halo = CriarMalhaDeAneis("Halo", 48, 12, (s, a) =>
        {
            float angulo = s * Mathf.PI * 2f;
            float raio = 0.62f + 0.045f * Mathf.Cos(a);
            return new Vector3(raio * Mathf.Cos(angulo), -0.045f * Mathf.Sin(a), raio * Mathf.Sin(angulo));
        }, fechado: true);
        Transform aro = AdicionarMalha("Halo dourado", formaAnjo.transform, halo, luzDoHalo);
        aro.localPosition = new Vector3(0f, 4.0f, -0.1f);
        aro.localRotation = Quaternion.Euler(10f, 0f, 0f);

        Mesh pena = CriarMalhaDeAneis("Pena", 10, 8, (s, a) =>
        {
            float perfil = Mathf.Sin(s * Mathf.PI);
            return new Vector3(Mathf.Cos(a) * perfil * 0.5f, s, Mathf.Sin(a) * perfil * 0.14f);
        }, fechado: false);
        asaEsquerda = CriarAsa(-1, pena, penas, ouro);
        asaDireita = CriarAsa(1, pena, penas, ouro);
        formaAnjo.SetActive(false);
    }

    private Transform CriarAsa(int lado, Mesh pena, Material penas, Material ouro)
    {
        Transform asa = CriarFilho(lado < 0 ? "Asa esquerda" : "Asa direita", formaAnjo.transform);
        asa.localPosition = new Vector3(lado * 0.29f, 2.55f, -0.58f);
        Transform longarina = AdicionarMalha("Arco dourado", asa, malhaCurvada, ouro);
        longarina.localRotation = Quaternion.Euler(8f, 0f, -lado * 51f);
        longarina.localScale = new Vector3(1.8f, 2.2f, 1.1f);
        for (int i = 0; i < 8; i++)
        {
            Transform pluma = AdicionarMalha("Pena longa", asa, pena, penas);
            pluma.localPosition = new Vector3(lado * (0.14f + 0.16f * i), 0.14f + 0.10f * i, -0.14f);
            pluma.localRotation = Quaternion.Euler(0f, 0f, -lado * (143f - 11f * i));
            pluma.localScale = new Vector3(0.34f, 0.9f + 0.13f * i, 0.44f);
        }
        for (int i = 0; i < 5; i++)
        {
            Transform pluma = AdicionarMalha("Pena dourada", asa, pena, ouro);
            pluma.localPosition = new Vector3(lado * (0.08f + 0.17f * i), 0.20f + 0.13f * i, -0.06f);
            pluma.localRotation = Quaternion.Euler(0f, 0f, -lado * (112f - 11f * i));
            pluma.localScale = new Vector3(0.27f, 0.6f + 0.08f * i, 0.24f);
        }
        return asa;
    }

    private void AnimarAsas()
    {
        if (faseVisual != 3 || asaEsquerda == null)
            return;

        float batida = 8f + Mathf.Sin(Time.time * (NoAr ? 12f : 4f)) * (NoAr ? 24f : 9f);
        asaEsquerda.localRotation = Quaternion.Euler(0f, batida, 0f);
        asaDireita.localRotation = Quaternion.Euler(0f, -batida, 0f);
    }

    private Material CriarMaterial(string nome, Color cor, Color emissao = default, bool manterTextura = false)
    {
        var material = new Material(madeiraOriginal) { name = nome };
        if (!manterTextura)
        {
            material.mainTexture = null;
            material.SetFloat("_Cull", 0f);
        }
        material.SetColor(CorBase, cor);
        if (emissao.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture(TexturaDaEmissao, null);
            material.SetColor(Emissao, emissao);
        }
        recursosVisuais.Add(material);
        return material;
    }

    private Mesh CriarMalhaDeAneis(string nome, int aneis, int lados,
        System.Func<float, float, Vector3> ponto, bool fechado)
    {
        int faixas = fechado ? aneis : aneis - 1;
        var vertices = new Vector3[aneis * lados];
        var triangulos = new int[faixas * lados * 6];
        for (int i = 0; i < aneis; i++)
        for (int j = 0; j < lados; j++)
            vertices[i * lados + j] = ponto(i / (float)faixas, j * Mathf.PI * 2f / lados);

        int t = 0;
        for (int i = 0; i < faixas; i++)
        for (int j = 0; j < lados; j++)
        {
            int a = i * lados + j;
            int b = i * lados + (j + 1) % lados;
            int c = (i + 1) % aneis * lados + j;
            int d = (i + 1) % aneis * lados + (j + 1) % lados;
            triangulos[t++] = a;
            triangulos[t++] = c;
            triangulos[t++] = b;
            triangulos[t++] = b;
            triangulos[t++] = c;
            triangulos[t++] = d;
        }

        var malha = new Mesh { name = nome, vertices = vertices, triangles = triangulos };
        malha.RecalculateNormals();
        malha.RecalculateBounds();
        recursosVisuais.Add(malha);
        return malha;
    }

    private static Transform AdicionarMalha(string nome, Transform pai, Mesh malha, Material material)
    {
        Transform peca = CriarFilho(nome, pai);
        peca.gameObject.AddComponent<MeshFilter>().sharedMesh = malha;
        peca.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return peca;
    }

    private static Transform CriarFilho(string nome, Transform pai)
    {
        var filho = new GameObject(nome);
        if (pai != null)
        {
            filho.layer = pai.gameObject.layer;
            filho.transform.SetParent(pai, false);
        }
        return filho.transform;
    }

    private void DestruirRecursosVisuais()
    {
        foreach (Object recurso in recursosVisuais)
            Destroy(recurso);
    }
}
