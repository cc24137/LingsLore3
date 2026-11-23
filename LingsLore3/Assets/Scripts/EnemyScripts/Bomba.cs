using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Bomba : MonoBehaviour
{
    [Header("Voo")]
    [SerializeField, Min(0.01f)] private float tempoDeVoo = 0.75f;
    [SerializeField, Min(0f)] private float alturaDoArco = 2f;
    [SerializeField, Min(0f)] private float alturaAoPousar = 0.22f;

    [Header("Explosão")]
    [SerializeField, Min(0f)] private float pavio = 0.65f;
    [SerializeField, Min(0.01f)] private float raioDaExplosao = 1.6f;
    [SerializeField, Min(0f)] private float dano = 18f;
    [SerializeField, Min(0f)] private float duracaoDoImpacto = 0.18f;

    [Header("Visual")]
    [SerializeField] private Renderer visual;
    [SerializeField] private Material materialDoAviso;
    [SerializeField] private Color corDoAviso = new Color(1f, 0.72f, 0.12f);
    [SerializeField] private Color corDoImpacto = new Color(1f, 0.22f, 0.06f);

    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private bool lancada;

    public float RaioDaExplosao => raioDaExplosao;
    public event System.Action<Vector3> Explodiu;

    public void Lancar(Vector3 destino, PlayerResources alvo, LayerMask obstaculos,
        ISet<PlayerResources> alvosDaSalva = null)
    {
        if (lancada)
            return;
        lancada = true;

        if (!isActiveAndEnabled || visual == null || materialDoAviso == null)
        {
            Debug.LogError("Ative a bomba e configure o visual e o material do aviso no prefab.", this);
            Destroy(gameObject);
            return;
        }

        StartCoroutine(VoarEExplodir(destino, alvo, obstaculos, alvosDaSalva));
    }

    private IEnumerator VoarEExplodir(Vector3 destino, PlayerResources alvo, LayerMask obstaculos,
        ISet<PlayerResources> alvosDaSalva)
    {
        LineRenderer aviso = MarcarExplosao(destino);
        Vector3 origem = transform.position;
        Vector3 pouso = destino + Vector3.up * alturaAoPousar;
        float inicio = Time.time;
        while (Time.time < inicio + tempoDeVoo)
        {
            float t = (Time.time - inicio) / tempoDeVoo;
            transform.position = Vector3.Lerp(origem, pouso, t) + Vector3.up * (4f * alturaDoArco * t * (1f - t));
            yield return null;
        }

        transform.position = pouso;
        yield return new WaitForSeconds(pavio);

        visual.enabled = false;
        aviso.widthMultiplier = 0.22f;
        Colorir(aviso, corDoImpacto);
        CausarDano(destino, alvo, obstaculos, alvosDaSalva);
        if (isActiveAndEnabled)
            Explodiu?.Invoke(destino);

        yield return new WaitForSeconds(duracaoDoImpacto);
        Destroy(gameObject);
    }

    private void CausarDano(Vector3 centro, PlayerResources alvo, LayerMask obstaculos, ISet<PlayerResources> alvosDaSalva)
    {
        if (alvo == null || !alvo.isActiveAndEnabled || alvosDaSalva?.Contains(alvo) == true
            || !alvo.TryGetComponent(out Collider colisor) || !colisor.enabled)
            return;

        Vector3 origem = centro + Vector3.up * 0.65f;
        Vector3 ponto = colisor.ClosestPoint(origem);
        Vector3 delta = ponto - centro;
        delta.y = 0f;
        if (delta.sqrMagnitude > raioDaExplosao * raioDaExplosao || Mathf.Abs(ponto.y - origem.y) > 0.85f
            || Physics.Linecast(origem, ponto, obstaculos, QueryTriggerInteraction.Ignore))
            return;

        if (alvo.TakeDamage(dano, centro))
            alvosDaSalva?.Add(alvo);
    }

    private LineRenderer MarcarExplosao(Vector3 centro)
    {
        const int segmentos = 40;
        var objeto = new GameObject("Área da explosão");
        objeto.transform.SetParent(transform, false);
        var linha = objeto.AddComponent<LineRenderer>();
        linha.useWorldSpace = true;
        linha.loop = true;
        linha.widthMultiplier = 0.1f;
        linha.positionCount = segmentos;
        linha.sharedMaterial = materialDoAviso;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.receiveShadows = false;
        Colorir(linha, corDoAviso);

        centro.y += 0.05f;
        for (int i = 0; i < segmentos; i++)
        {
            float angulo = 360f * i / segmentos;
            linha.SetPosition(i, centro + Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * raioDaExplosao);
        }
        return linha;
    }

    private static void Colorir(Renderer renderizador, Color cor)
    {
        var propriedades = new MaterialPropertyBlock();
        propriedades.SetColor(CorBase, cor);
        renderizador.SetPropertyBlock(propriedades);
    }
}
