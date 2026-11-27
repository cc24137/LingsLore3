using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class BossNetanyahu
{
    private SkinnedMeshRenderer visualHumano;
    private GameObject robo;
    private Mesh malhaDaCabeca;
    private Material blindagem;
    private Material metalEscuro;
    private Material metalClaro;
    private Material energia;
    private Material materialDoOlho;
    private Material[] materiaisDaCabeca;
    private MaterialPropertyBlock corDaFormaHumana;
    private Transform suporteDaArmaRobo;
    private Renderer olhoDoRobo;
    private Light luzDoOlho;
    private readonly List<Transform> rodasDoRobo = new List<Transform>(6);
    private Vector3 posicaoDoOlho;
    private Vector3 posicaoAnteriorDoRobo;
    private Quaternion rotacaoAnteriorDoRobo;
    private float faseDasRodas;
    private float inicioDoOlho;

    internal SkinnedMeshRenderer ModeloNormal => visualHumano;
    internal Bounds LimitesDoPiso => piso.bounds;

    private void PrepararRobo()
    {
        visualHumano = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (visualHumano == null || visualHumano.sharedMesh == null)
            return;

        materiaisDaCabeca = visualHumano.sharedMaterials;
        malhaDaCabeca = RecortarCabeca(visualHumano);
        if (malhaDaCabeca == null)
            return;

        blindagem = CriarMetal(new Color(0.16f, 0.23f, 0.30f), 0.72f);
        metalEscuro = CriarMetal(new Color(0.047f, 0.063f, 0.08f), 0.3f);
        metalClaro = CriarMetal(new Color(0.56f, 0.65f, 0.70f), 0.78f);
        energia = new Material(shaderDosAvisos);
        energia.SetColor(CorBase, new Color(0.07f, 1.15f, 1.6f));
        materialDoOlho = new Material(shaderDosAvisos);

        robo = MontarCorpoRobo(transform, mini: false).gameObject;
        robo.SetActive(false);
        ApagarOlhoDoCombo();
    }

    private Mesh RecortarCabeca(SkinnedMeshRenderer original)
    {
        // A malha importada une a cabeça e o corpo. O recorte usa a altura do pescoço no modelo.
        var bake = new Mesh();
        original.BakeMesh(bake);
        Vector3[] posicoes = bake.vertices;
        if (posicoes.Length == 0)
        {
            Destroy(bake);
            return null;
        }

        Matrix4x4 conversao = transform.worldToLocalMatrix * original.transform.localToWorldMatrix;
        Bounds limites = new Bounds(conversao.MultiplyPoint3x4(posicoes[0]), Vector3.zero);
        for (int i = 0; i < posicoes.Length; i++)
        {
            posicoes[i] = conversao.MultiplyPoint3x4(posicoes[i]);
            limites.Encapsulate(posicoes[i]);
        }

        float alturaDoCorte = Mathf.Lerp(limites.min.y, limites.max.y, 0.76f);
        Vector3[] normais = bake.normals;
        Vector2[] uvs = bake.uv;
        var verticesDaCabeca = new List<Vector3>();
        var normaisDaCabeca = new List<Vector3>();
        var uvsDaCabeca = new List<Vector2>();
        var mapeamento = new Dictionary<int, int>();
        var submalhas = new List<int>[bake.subMeshCount];
        Matrix4x4 conversaoDosVetores = conversao.inverse.transpose;
        bool invertido = conversao.determinant < 0f;
        for (int s = 0; s < submalhas.Length; s++)
        {
            List<int> destino = submalhas[s] = new List<int>();
            int[] origem = bake.GetTriangles(s);
            for (int i = 0; i < origem.Length; i += 3)
            {
                int a = origem[i];
                int b = origem[i + 1];
                int c = origem[i + 2];
                if (posicoes[a].y < alturaDoCorte || posicoes[b].y < alturaDoCorte || posicoes[c].y < alturaDoCorte)
                    continue;

                destino.Add(CopiarVertice(a));
                destino.Add(CopiarVertice(invertido ? c : b));
                destino.Add(CopiarVertice(invertido ? b : c));
            }
        }
        Destroy(bake);
        if (verticesDaCabeca.Count == 0)
            return null;

        Bounds cabecaOriginal = new Bounds(verticesDaCabeca[0], Vector3.zero);
        foreach (Vector3 vertice in verticesDaCabeca)
            cabecaOriginal.Encapsulate(vertice);
        Vector3 baseDaCabeca = new Vector3(cabecaOriginal.center.x, cabecaOriginal.min.y, cabecaOriginal.center.z);
        float escala = 0.44f / Mathf.Max(0.01f, cabecaOriginal.size.y);
        for (int i = 0; i < verticesDaCabeca.Count; i++)
            verticesDaCabeca[i] = (verticesDaCabeca[i] - baseDaCabeca) * escala;

        var recorte = new Mesh { name = "Cabeça do Netanyahu", subMeshCount = submalhas.Length };
        if (verticesDaCabeca.Count > ushort.MaxValue)
            recorte.indexFormat = IndexFormat.UInt32;
        recorte.SetVertices(verticesDaCabeca);
        recorte.SetNormals(normaisDaCabeca);
        recorte.SetUVs(0, uvsDaCabeca);
        for (int s = 0; s < submalhas.Length; s++)
            recorte.SetTriangles(submalhas[s], s, false);
        recorte.RecalculateBounds();
        recorte.RecalculateTangents();
        posicaoDoOlho = LocalizarOlho(recorte.bounds, verticesDaCabeca, submalhas);
        return recorte;

        int CopiarVertice(int origem)
        {
            if (mapeamento.TryGetValue(origem, out int indice))
                return indice;
            indice = verticesDaCabeca.Count;
            verticesDaCabeca.Add(posicoes[origem]);
            normaisDaCabeca.Add(normais.Length > origem ? conversaoDosVetores.MultiplyVector(normais[origem]).normalized : Vector3.up);
            uvsDaCabeca.Add(uvs.Length > origem ? uvs[origem] : Vector2.zero);
            mapeamento.Add(origem, indice);
            return indice;
        }
    }

    private static Vector3 LocalizarOlho(Bounds limites, List<Vector3> vertices, List<int>[] submalhas)
    {
        float altura = Mathf.Lerp(limites.min.y, limites.max.y, 0.56f);
        float esquerda = float.PositiveInfinity;
        float direita = float.NegativeInfinity;
        foreach (Vector3 vertice in vertices)
        {
            if (Mathf.Abs(vertice.y - altura) > 0.025f)
                continue;
            esquerda = Mathf.Min(esquerda, vertice.x);
            direita = Mathf.Max(direita, vertice.x);
        }
        float lateral = esquerda <= direita ? Mathf.Lerp(esquerda, direita, 0.72f) : limites.center.x;
        Vector3 olho = new Vector3(lateral, altura, limites.max.z);
        float frente = float.NegativeInfinity;
        Vector2 ponto = olho;
        foreach (List<int> triangulos in submalhas)
        {
            for (int i = 0; i < triangulos.Count; i += 3)
            {
                Vector3 a = vertices[triangulos[i]];
                Vector3 ab = vertices[triangulos[i + 1]] - a;
                Vector3 ac = vertices[triangulos[i + 2]] - a;
                Vector2 delta = ponto - (Vector2)a;
                float det = ab.x * ac.y - ab.y * ac.x;
                if (Mathf.Abs(det) < 0.00000001f)
                    continue;
                float u = (delta.x * ac.y - delta.y * ac.x) / det;
                float v = (ab.x * delta.y - ab.y * delta.x) / det;
                if (u >= 0f && v >= 0f && u + v <= 1f)
                    frente = Mathf.Max(frente, a.z + ab.z * u + ac.z * v);
            }
        }
        olho.z = (float.IsNegativeInfinity(frente) ? limites.max.z : frente) + 0.015f;
        return olho;
    }

    private Transform MontarCorpoRobo(Transform pai, bool mini)
    {
        var corpoVisual = new GameObject(mini ? "Mini Netanyahu" : "Chassi do Robô Netanyahu");
        Transform raiz = corpoVisual.transform;
        raiz.SetParent(pai, false);
        if (mini)
        {
            raiz.localPosition = Vector3.up * 0.65f;
            raiz.localScale = Vector3.one * 0.67f;
        }

        Peca("Chassi", raiz, PrimitiveType.Cube, new Vector3(0f, -0.53f, 0f),
            new Vector3(1.04f, 0.33f, 0.77f), metalEscuro);
        Peca("Torso blindado", raiz, PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
            new Vector3(0.94f, 0.80f, 0.68f), blindagem);
        Peca("Placa frontal", raiz, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.355f),
            new Vector3(0.68f, 0.48f, 0.10f), metalClaro);
        Peca("Reator", raiz, PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0.437f),
            new Vector3(0.23f, 0.23f, 0.11f), energia);
        Peca("Colar mecânico", raiz, PrimitiveType.Cylinder, Vector3.up * 0.44f,
            new Vector3(0.46f, 0.075f, 0.46f), metalEscuro);

        for (int lado = -1; lado <= 1; lado += 2)
        {
            Peca("Esteira", raiz, PrimitiveType.Cube, new Vector3(lado * 0.52f, -0.72f, 0f),
                new Vector3(0.29f, 0.44f, 1.31f), metalEscuro);
            for (int i = -1; i <= 1; i++)
            {
                Transform roda = Peca("Roda motriz", raiz, PrimitiveType.Cylinder,
                    new Vector3(lado * 0.67f, -0.72f, i * 0.42f),
                    new Vector3(0.41f, 0.06f, 0.41f), blindagem, Quaternion.Euler(0f, 0f, 90f));
                Peca("Marca da roda", roda, PrimitiveType.Cube, new Vector3(0f, -lado * 1.1f, 0.30f),
                    new Vector3(0.12f, 0.32f, 0.32f), metalClaro);
                if (!mini)
                    rodasDoRobo.Add(roda);
            }
            Peca("Ombro articulado", raiz, PrimitiveType.Sphere, new Vector3(lado * 0.56f, 0.24f, 0f),
                new Vector3(0.36f, 0.37f, 0.40f), metalClaro);
            Peca("Braço mecânico", raiz, PrimitiveType.Cube, new Vector3(lado * 0.65f, -0.04f, 0.13f),
                new Vector3(0.27f, 0.50f, 0.32f), blindagem, Quaternion.Euler(-24f, 0f, lado * 8f));
            Peca("Porta-míssil", raiz, PrimitiveType.Cube, new Vector3(lado * 0.48f, 0.22f, 0.36f),
                new Vector3(0.20f, 0.18f, 0.15f), metalEscuro);
        }

        var cabecaHumana = new GameObject("Cabeça humana");
        cabecaHumana.transform.SetParent(raiz, false);
        cabecaHumana.transform.localPosition = Vector3.up * 0.50f;
        cabecaHumana.AddComponent<MeshFilter>().sharedMesh = malhaDaCabeca;
        cabecaHumana.AddComponent<MeshRenderer>().sharedMaterials = materiaisDaCabeca;
        if (!mini)
        {
            suporteDaArmaRobo = new GameObject("Suporte da metralhadora").transform;
            suporteDaArmaRobo.SetParent(raiz, false);
            suporteDaArmaRobo.localPosition = new Vector3(0.68f, 0.13f, 0.47f);
            Transform lente = Peca("Olho vermelho", cabecaHumana.transform, PrimitiveType.Sphere, posicaoDoOlho,
                new Vector3(0.047f, 0.037f, 0.030f), materialDoOlho);
            olhoDoRobo = lente.GetComponent<Renderer>();
            luzDoOlho = lente.gameObject.AddComponent<Light>();
            luzDoOlho.type = LightType.Point;
            luzDoOlho.color = new Color(1f, 0.035f, 0.02f);
            luzDoOlho.range = 6f;
            luzDoOlho.shadows = LightShadows.None;
        }
        return raiz;
    }

    private static Transform Peca(string nome, Transform pai, PrimitiveType tipo, Vector3 posicao, Vector3 escala,
        Material material, Quaternion? rotacao = null)
    {
        GameObject objeto = GameObject.CreatePrimitive(tipo);
        objeto.name = nome;
        Collider colisor = objeto.GetComponent<Collider>();
        colisor.enabled = false;
        Destroy(colisor);
        objeto.GetComponent<MeshRenderer>().sharedMaterial = material;
        Transform peca = objeto.transform;
        peca.SetParent(pai, false);
        peca.SetLocalPositionAndRotation(posicao, rotacao ?? Quaternion.identity);
        peca.localScale = escala;
        return peca;
    }

    private Material CriarMetal(Color cor, float brilho)
    {
        Material original = visualHumano.sharedMaterial;
        Material material = original != null ? new Material(original) : new Material(shaderDosAvisos);
        material.mainTexture = null;
        material.DisableKeyword("_NORMALMAP");
        material.SetColor(CorBase, cor);
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.63f);
            material.SetFloat("_Smoothness", brilho);
        }
        return material;
    }

    private void MontarMiniRobo(Transform mini)
    {
        if (malhaDaCabeca != null)
            MontarCorpoRobo(mini, mini: true);
    }

    private void ExibirForma(bool robotica)
    {
        if (visualHumano != null)
            visualHumano.enabled = !robotica || robo == null;
        if (robo == null)
            return;
        if (robotica && !robo.activeSelf)
        {
            posicaoAnteriorDoRobo = transform.position;
            rotacaoAnteriorDoRobo = transform.rotation;
        }
        robo.SetActive(robotica);
    }

    private void ColorirFormaHumana(float intensidade)
    {
        Material material = visualHumano != null ? visualHumano.sharedMaterial : null;
        if (material == null || !material.HasColor(CorBase))
            return;

        corDaFormaHumana ??= new MaterialPropertyBlock();
        visualHumano.GetPropertyBlock(corDaFormaHumana);
        Color cor = material.GetColor(CorBase);
        cor.g *= Mathf.Lerp(1f, 0.52f, intensidade);
        cor.b *= Mathf.Lerp(1f, 0.48f, intensidade);
        corDaFormaHumana.SetColor(CorBase, cor);
        visualHumano.SetPropertyBlock(corDaFormaHumana);
    }

    private void AnimarRobo()
    {
        float deslocamento = VetorHorizontal(transform.position - posicaoAnteriorDoRobo).magnitude;
        float giro = Quaternion.Angle(transform.rotation, rotacaoAnteriorDoRobo);
        faseDasRodas = Mathf.Repeat(faseDasRodas + deslocamento * 72f + giro * 0.48f, 360f);
        foreach (Transform roda in rodasDoRobo)
            roda.localRotation = Quaternion.Euler(faseDasRodas, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);
        posicaoAnteriorDoRobo = transform.position;
        rotacaoAnteriorDoRobo = transform.rotation;

        float inclinacao = estado == Estado.Atordoado ? 13f : estado == Estado.Aproximando ? -3f : 0f;
        robo.transform.localRotation = Quaternion.Slerp(robo.transform.localRotation,
            Quaternion.Euler(inclinacao, 0f, 0f), 1f - Mathf.Exp(-10f * Time.deltaTime));
        if (luzDoOlho.enabled)
            luzDoOlho.intensity = Mathf.Lerp(6f, 17f, Mathf.Clamp01((Time.time - inicioDoOlho) / 0.85f));
    }

    private void AcenderOlhoDoCombo()
    {
        if (olhoDoRobo == null)
            return;
        inicioDoOlho = Time.time;
        luzDoOlho.enabled = true;
        Colorir(olhoDoRobo, new Color(9f, 0.018f, 0.008f));
    }

    private void ApagarOlhoDoCombo()
    {
        if (olhoDoRobo == null)
            return;
        luzDoOlho.enabled = false;
        Colorir(olhoDoRobo, new Color(0.38f, 0.018f, 0.015f));
    }

    private void DestruirVisuaisDoRobo()
    {
        Destroy(malhaDaCabeca);
        Destroy(blindagem);
        Destroy(metalEscuro);
        Destroy(metalClaro);
        Destroy(energia);
        Destroy(materialDoOlho);
    }
}
