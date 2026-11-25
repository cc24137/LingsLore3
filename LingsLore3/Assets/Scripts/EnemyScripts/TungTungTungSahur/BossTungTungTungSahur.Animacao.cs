using UnityEngine;

public sealed partial class BossTungTungTungSahur
{
    [SerializeField] private MeshFilter modelo;

    private Mesh malhaArticulada;
    private Transform bracoDoTaco;
    private Renderer visualDoCorpo;

    // Pontos na malha original, usados para articular o braço e o taco que vieram unidos ao corpo.
    private static readonly Vector3 Ombro = new Vector3(0.46f, 2.45f, -0.26f);
    private static readonly Vector3 CaboDoTaco = new Vector3(0.43f, 1.75f, -0.86f);
    private static readonly Vector3 PontaDoTaco = new Vector3(0.95f, 0.38f, 0.93f);

    private void PrepararArticulacaoDoTaco()
    {
        if (modelo == null || !modelo.TryGetComponent(out MeshRenderer visualOriginal))
            return;

        visualDoCorpo = visualOriginal;
        if (modelo.sharedMesh == null || !modelo.sharedMesh.isReadable)
            return;

        Transform raiz = modelo.transform;
        bracoDoTaco = new GameObject("Braço do taco").transform;
        bracoDoTaco.SetParent(raiz, false);
        bracoDoTaco.localPosition = Ombro;

        var objeto = new GameObject("Modelo articulado") { layer = modelo.gameObject.layer };
        objeto.transform.SetParent(raiz, false);
        var visual = objeto.AddComponent<SkinnedMeshRenderer>();
        malhaArticulada = Instantiate(modelo.sharedMesh);
        Vector3[] vertices = malhaArticulada.vertices;
        var pesos = new BoneWeight[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            float peso = PesoDoBraco(vertices[i]);
            pesos[i] = peso >= 0.5f
                ? new BoneWeight { boneIndex0 = 1, weight0 = peso, boneIndex1 = 0, weight1 = 1f - peso }
                : new BoneWeight { boneIndex0 = 0, weight0 = 1f - peso, boneIndex1 = 1, weight1 = peso };
        }

        malhaArticulada.bindposes = new[]
        {
            Matrix4x4.identity,
            Matrix4x4.Translate(-Ombro)
        };
        malhaArticulada.boneWeights = pesos;
        visual.sharedMesh = malhaArticulada;
        visual.sharedMaterials = visualOriginal.sharedMaterials;
        visual.rootBone = raiz;
        visual.bones = new[] { raiz, bracoDoTaco };
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

    private void LateUpdate()
    {
        AnimarAsas();
        if (bracoDoTaco == null)
            return;

        float progresso = Mathf.InverseLerp(inicioDoEstado, fimDoEstado, Time.time - Time.fixedDeltaTime);
        float inclinacao = 0f;
        Quaternion pose = Quaternion.identity;
        switch (estado)
        {
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
        modelo.transform.localRotation = Quaternion.Euler(inclinacao, 0f, 0f);
    }

    private static Quaternion PoseDaTacada(float angulo, float inclinacao = 18f)
    {
        Vector3 direcaoDoTaco = PontaDoTaco - CaboDoTaco;
        Vector3 destino = Quaternion.Euler(inclinacao, angulo, 0f) * Vector3.forward;
        return Quaternion.FromToRotation(direcaoDoTaco, destino);
    }

    private void ReiniciarPoseDoTaco()
    {
        if (bracoDoTaco == null)
            return;

        bracoDoTaco.localRotation = Quaternion.identity;
        modelo.transform.localRotation = Quaternion.identity;
    }
}
