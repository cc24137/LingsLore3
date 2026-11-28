using UnityEngine;
using UnityEngine.Rendering;

internal sealed class PreenchimentoAviso : MonoBehaviour
{
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private const float Opacidade = 0.2f;

    private LineRenderer contorno;
    private MeshRenderer visual;
    private Mesh malha;
    private Material material;
    private MaterialPropertyBlock propriedades;
    private Vector3[] pontos;
    private Vector3[] vertices;
    private int[] triangulos;
    private bool lequeNoUltimoPonto;
    private Vector3[] fechamento;
    private Color? corAnterior;

    internal static void Adicionar(LineRenderer contorno, bool lequeNoUltimoPonto = false, Vector3[] fechamento = null)
    {
        var objeto = new GameObject("Preenchimento do aviso");
        objeto.transform.SetParent(contorno.transform, false);
        var preenchimento = objeto.AddComponent<PreenchimentoAviso>();
        preenchimento.contorno = contorno;
        preenchimento.lequeNoUltimoPonto = lequeNoUltimoPonto;
        preenchimento.fechamento = fechamento;
        preenchimento.PrepararVisual();
        preenchimento.AtualizarVisual();
    }

    private void PrepararVisual()
    {
        malha = new Mesh { name = "Área do aviso" };
        malha.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = malha;

        material = new Material(contorno.sharedMaterial);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetFloat("_Surface", 1f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
        material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.renderQueue = (int)RenderQueue.Transparent;

        visual = gameObject.AddComponent<MeshRenderer>();
        visual.sharedMaterial = material;
        visual.shadowCastingMode = ShadowCastingMode.Off;
        visual.receiveShadows = false;
        propriedades = new MaterialPropertyBlock();
    }

    private void LateUpdate() => AtualizarVisual();

    private void AtualizarVisual()
    {
        visual.enabled = contorno != null && contorno.enabled && contorno.positionCount >= 3 &&
            (contorno.loop || fechamento != null);
        if (!visual.enabled)
            return;

        AtualizarCor();
        AtualizarMalha();
    }

    private void AtualizarCor()
    {
        contorno.GetPropertyBlock(propriedades);
        Color cor = propriedades.HasColor(CorBase) ? propriedades.GetColor(CorBase) : contorno.sharedMaterial.GetColor(CorBase);
        if (corAnterior == cor)
            return;

        corAnterior = cor;
        Color interior = Color.Lerp(cor, Color.white, 0.28f);
        interior.a = cor.a * Opacidade;
        propriedades.Clear();
        propriedades.SetColor(CorBase, interior);
        visual.SetPropertyBlock(propriedades);
    }

    private void AtualizarMalha()
    {
        int quantidadeDoContorno = contorno.positionCount;
        int quantidade = quantidadeDoContorno + (fechamento?.Length ?? 0);
        bool mudouTopologia = pontos == null || pontos.Length != quantidadeDoContorno;
        if (mudouTopologia)
        {
            pontos = new Vector3[quantidadeDoContorno];
            vertices = new Vector3[quantidade];
            triangulos = new int[(quantidade - 2) * 3];
            for (int i = 0; i < quantidade - 2; i++)
            {
                triangulos[i * 3] = lequeNoUltimoPonto ? quantidade - 1 : 0;
                triangulos[i * 3 + 1] = lequeNoUltimoPonto ? i : i + 1;
                triangulos[i * 3 + 2] = lequeNoUltimoPonto ? i + 1 : i + 2;
            }
        }

        contorno.GetPositions(pontos);
        Matrix4x4 mundoParaLocal = transform.worldToLocalMatrix;
        Matrix4x4 pontosParaMundo = contorno.useWorldSpace ? Matrix4x4.identity : contorno.transform.localToWorldMatrix;
        bool mudouPosicao = mudouTopologia;
        for (int i = 0; i < quantidade; i++)
        {
            Vector3 ponto = i < quantidadeDoContorno ? pontosParaMundo.MultiplyPoint3x4(pontos[i]) : fechamento[i - quantidadeDoContorno];
            Vector3 local = mundoParaLocal.MultiplyPoint3x4(ponto - Vector3.up * 0.008f);
            mudouPosicao |= vertices[i] != local;
            vertices[i] = local;
        }
        if (!mudouPosicao)
            return;

        if (mudouTopologia)
            malha.Clear();
        malha.vertices = vertices;
        if (mudouTopologia)
            malha.triangles = triangulos;
        malha.RecalculateBounds();
    }

    private void OnDestroy()
    {
        Destroy(malha);
        Destroy(material);
    }
}
