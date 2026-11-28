using UnityEngine;

public sealed partial class BossTungTungTungSahur
{
    [SerializeField] private MeshFilter modelo;

    private Mesh malhaArticulada;
    private Transform bracoDoTaco;
    private Transform pernaEsquerda;
    private Transform pernaDireita;
    private Renderer visualDoCorpo;
    private Vector3 posicaoOriginalDoModelo;
    private float velocidadeDaCaminhada;
    private float cicloDaCaminhada;
    private float pesoDaCaminhada;

    private const float ComprimentoDoCiclo = 2.1f;
    private const float AnguloDoPasso = 23f;
    private const float AlturaDoPasso = 0.18f;

    // Pontos na malha original, usados para articular o braço e o taco que vieram unidos ao corpo.
    private static readonly Vector3 Ombro = new Vector3(0.46f, 2.45f, -0.26f);
    private static readonly Vector3 CaboDoTaco = new Vector3(0.43f, 1.75f, -0.86f);
    private static readonly Vector3 PontaDoTaco = new Vector3(0.95f, 0.38f, 0.93f);
    private static readonly Vector3 QuadrilEsquerdo = new Vector3(-0.22f, 1.33f, -0.25f);
    private static readonly Vector3 QuadrilDireito = new Vector3(0.26f, 1.33f, -0.25f);

    private void PrepararArticulacaoDoModelo()
    {
        if (modelo == null || !modelo.TryGetComponent(out MeshRenderer visualOriginal))
            return;

        visualDoCorpo = visualOriginal;
        if (modelo.sharedMesh == null || !modelo.sharedMesh.isReadable)
            return;

        Transform raiz = modelo.transform;
        posicaoOriginalDoModelo = raiz.localPosition;
        bracoDoTaco = CriarFilho("Braço do taco", raiz);
        bracoDoTaco.localPosition = Ombro;
        pernaEsquerda = CriarFilho("Perna esquerda", raiz);
        pernaEsquerda.localPosition = QuadrilEsquerdo;
        pernaDireita = CriarFilho("Perna direita", raiz);
        pernaDireita.localPosition = QuadrilDireito;

        var visual = CriarFilho("Modelo articulado", raiz).gameObject.AddComponent<SkinnedMeshRenderer>();
        malhaArticulada = Instantiate(modelo.sharedMesh);
        Vector3[] vertices = malhaArticulada.vertices;
        var pesos = new BoneWeight[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertice = vertices[i];
            float peso = PesoDoBraco(vertice);
            int osso = 1;
            if (peso == 0f)
            {
                peso = PesoDaPerna(vertice);
                osso = vertice.x < 0f ? 2 : 3;
            }
            pesos[i] = peso >= 0.5f
                ? new BoneWeight { boneIndex0 = osso, weight0 = peso, boneIndex1 = 0, weight1 = 1f - peso }
                : new BoneWeight { boneIndex0 = 0, weight0 = 1f - peso, boneIndex1 = osso, weight1 = peso };
        }

        malhaArticulada.bindposes = new[]
        {
            Matrix4x4.identity,
            Matrix4x4.Translate(-Ombro),
            Matrix4x4.Translate(-QuadrilEsquerdo),
            Matrix4x4.Translate(-QuadrilDireito)
        };
        malhaArticulada.boneWeights = pesos;
        visual.sharedMesh = malhaArticulada;
        visual.sharedMaterials = visualOriginal.sharedMaterials;
        visual.rootBone = raiz;
        visual.bones = new[] { raiz, bracoDoTaco, pernaEsquerda, pernaDireita };
        Bounds limites = malhaArticulada.bounds;
        limites.Expand(3f);
        visual.localBounds = limites;
        visualOriginal.enabled = false;
        visualDoCorpo = visual;
    }

    private static float PesoDoBraco(Vector3 vertice)
    {
        float pesoDoBraco = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.41f, 0.54f, vertice.x)) *
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.52f, 2.18f, vertice.y)) *
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.10f, 1.20f, vertice.y));

        Vector3 eixoDoTaco = PontaDoTaco - CaboDoTaco;
        float t = Mathf.Clamp01(Vector3.Dot(vertice - CaboDoTaco, eixoDoTaco) / eixoDoTaco.sqrMagnitude);
        float distancia = (vertice - (CaboDoTaco + eixoDoTaco * t)).magnitude;
        float raioDoTaco = Mathf.Lerp(0.14f, 0.26f, t);
        float pesoDoTaco = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioDoTaco + 0.05f, raioDoTaco, distancia));
        return Mathf.Max(pesoDoBraco, pesoDoTaco);
    }

    private static float PesoDaPerna(Vector3 vertice)
    {
        // Na malha, os pés são mais largos que as pernas. O limite de largura preserva as mãos e o taco.
        float limiteLateral = Mathf.Lerp(0.75f, 0.39f, Mathf.InverseLerp(0.13f, 0.30f, vertice.y));
        float largura = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(limiteLateral + 0.07f, limiteLateral, Mathf.Abs(vertice.x)));
        float altura = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.42f, 1.05f, vertice.y));
        return largura * altura;
    }

    private void AtualizarCaminhada(out float balanco, out float oscilacao)
    {
        float rapidez = estado == Estado.Aproximando ? velocidadeDaCaminhada : 0f;
        float pesoAlvo = Mathf.Clamp01(rapidez / Mathf.Max(0.01f, velocidade * 0.4f));
        pesoDaCaminhada = Mathf.MoveTowards(pesoDaCaminhada, pesoAlvo, Time.deltaTime * 9f);
        float escala = Mathf.Max(0.01f, Mathf.Abs(modelo.transform.lossyScale.z));
        cicloDaCaminhada = Mathf.Repeat(cicloDaCaminhada + rapidez * Time.deltaTime / (escala * ComprimentoDoCiclo), 1f);
        float fase = cicloDaCaminhada * 2f * Mathf.PI;
        balanco = Mathf.Cos(fase) * pesoDaCaminhada;
        oscilacao = Mathf.Sin(fase) * pesoDaCaminhada;

        float elevacaoDoCorpo = 0.035f * (1f - Mathf.Abs(Mathf.Cos(fase))) * pesoDaCaminhada;
        modelo.transform.localPosition = posicaoOriginalDoModelo +
            Vector3.up * (elevacaoDoCorpo * modelo.transform.localScale.y);
        PosicionarPerna(pernaEsquerda, QuadrilEsquerdo, -balanco, -oscilacao, elevacaoDoCorpo);
        PosicionarPerna(pernaDireita, QuadrilDireito, balanco, oscilacao, elevacaoDoCorpo);
    }

    private static void PosicionarPerna(Transform perna, Vector3 quadril, float balanco, float oscilacao, float elevacaoDoCorpo)
    {
        float angulo = balanco * AnguloDoPasso;
        Quaternion rotacao = Quaternion.Euler(angulo, 0f, 0f);
        // Compensa a inclinação da sola para manter o pé de apoio na altura de repouso.
        float bordaDaSola = angulo >= 0f ? 0.34f : -0.43f;
        Vector3 sola = new Vector3(quadril.x, 0f, bordaDaSola);
        float alturaDaSola = (quadril + rotacao * (sola - quadril)).y;
        float elevacao = Mathf.Max(0f, oscilacao) * AlturaDoPasso;
        perna.localPosition = quadril + Vector3.up * (elevacao - alturaDaSola - elevacaoDoCorpo);
        perna.localRotation = rotacao;
    }

    private void LateUpdate()
    {
        AnimarAsas();
        if (bracoDoTaco == null)
            return;

        AtualizarCaminhada(out float balanco, out float oscilacao);
        float progresso = Mathf.InverseLerp(inicioDoEstado, fimDoEstado, Time.time - Time.fixedDeltaTime);
        float inclinacao = 0f;
        float giroDoCorpo = 0f;
        Quaternion pose = Quaternion.identity;
        switch (estado)
        {
            case Estado.Aproximando:
                pose = Quaternion.Euler(4f * balanco, 0f, 0f);
                giroDoCorpo = 3f * oscilacao;
                break;
            case Estado.PreparandoTacada:
                float preparacaoDoBraco = Mathf.SmoothStep(0f, 1f, TacadaGiratoria ? progresso / 0.28f : progresso);
                pose = Quaternion.Slerp(Quaternion.identity,
                    TacadaGiratoria ? PoseDaTacada(0f, -62f) : PoseDaTacada(semiAnguloDoGolpe),
                    preparacaoDoBraco);
                inclinacao = -7f * preparacaoDoBraco;
                break;
            case Estado.Tacando:
                pose = TacadaGiratoria
                    ? PoseDaTacada(0f, Mathf.SmoothStep(-62f, 18f, progresso / 0.24f))
                    : PoseDaTacada(Mathf.Lerp(semiAnguloDoGolpe, -semiAnguloDoGolpe, progresso));
                inclinacao = Mathf.Lerp(-7f, 9f, progresso);
                break;
            case Estado.EsperandoExplosaoDaTacada:
            case Estado.RecuperandoDaTacada:
                float retorno = estado == Estado.EsperandoExplosaoDaTacada ? 0f : Mathf.SmoothStep(0f, 1f, progresso * 3f);
                pose = Quaternion.Slerp(PoseDaTacada(TacadaGiratoria ? 0f : -semiAnguloDoGolpe), Quaternion.identity,
                    retorno);
                inclinacao = Mathf.Lerp(9f, 0f, retorno);
                break;
            case Estado.PreparandoSalto:
                inclinacao = 12f * Mathf.SmoothStep(0f, 1f, progresso);
                break;
            case Estado.Subindo:
                inclinacao = Mathf.Lerp(12f, -12f, progresso);
                break;
            case Estado.Cruzando:
                inclinacao = Mathf.Lerp(-12f, 10f, progresso);
                break;
            case Estado.Caindo:
                inclinacao = Mathf.Lerp(10f, 21f, progresso);
                break;
            case Estado.Impacto:
                inclinacao = Mathf.Lerp(21f, 0f, progresso);
                break;
            case Estado.Transformando:
                inclinacao = 12f * Mathf.Sin(Mathf.PI * progresso * 2f);
                pose = Quaternion.Slerp(Quaternion.identity, PoseDaTacada(72f), Mathf.Sin(Mathf.PI * progresso));
                break;
            case Estado.PreparandoArmas:
                inclinacao = -6f * Mathf.SmoothStep(0f, 1f, progresso);
                pose = Quaternion.Slerp(Quaternion.identity, PoseDaTacada(55f), progresso);
                break;
            case Estado.LancandoArmas:
                inclinacao = Mathf.Lerp(-6f, 0f, progresso);
                pose = Quaternion.Slerp(PoseDaTacada(55f), Quaternion.identity, progresso);
                break;
            case Estado.SubindoMeteoro:
                inclinacao = -18f * progresso;
                break;
            case Estado.ElevandoMeteoro:
                inclinacao = -7f * Mathf.Sin(progresso * Mathf.PI);
                pose = Quaternion.Slerp(Quaternion.identity, PoseDaTacada(68f), progresso);
                break;
            case Estado.CaindoMeteoro:
                inclinacao = Mathf.Lerp(46f, 24f, progresso);
                break;
        }

        bracoDoTaco.localRotation = pose;
        modelo.transform.localRotation = Quaternion.Euler(inclinacao, giroDoCorpo, 0f);
    }

    private static Quaternion PoseDaTacada(float angulo, float inclinacao = 18f)
    {
        Vector3 direcaoDoTaco = PontaDoTaco - CaboDoTaco;
        Vector3 destino = Quaternion.Euler(inclinacao, angulo, 0f) * Vector3.forward;
        return Quaternion.FromToRotation(direcaoDoTaco, destino);
    }

    private void ReiniciarPoseDoModelo()
    {
        if (bracoDoTaco == null)
            return;

        bracoDoTaco.localRotation = Quaternion.identity;
        pernaEsquerda.localRotation = Quaternion.identity;
        pernaDireita.localRotation = Quaternion.identity;
        pernaEsquerda.localPosition = QuadrilEsquerdo;
        pernaDireita.localPosition = QuadrilDireito;
        modelo.transform.localPosition = posicaoOriginalDoModelo;
        modelo.transform.localRotation = Quaternion.identity;
        pesoDaCaminhada = 0f;
        cicloDaCaminhada = 0f;
        velocidadeDaCaminhada = 0f;
    }
}
