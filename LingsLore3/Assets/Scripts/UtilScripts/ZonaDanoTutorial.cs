using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ZonaDanoTutorial : MonoBehaviour
{
    private enum Estado { Seguro, Aviso, Perigo }

    [Header("Ciclo em segundos")]
    [SerializeField, Min(0.1f)] private float tempoSeguro = 2f;
    [SerializeField, Min(0.1f)] private float tempoAviso = 1f;
    [SerializeField, Min(0.1f)] private float tempoPerigo = 1f;

    [Header("Dano")]
    [SerializeField, Min(0f)] private float dano = 10f;
    [SerializeField, Min(0.1f)] private float intervaloEntreDanos = 1f;

    [Header("Indicador visual")]
    [SerializeField] private Renderer indicador;
    [SerializeField] private Color corSegura = Color.green;
    [SerializeField] private Color corAviso = Color.yellow;
    [SerializeField] private Color corPerigo = Color.red;

    [Header("Estado atual - acompanhe no Play")]
    [SerializeField] private Estado estadoAtual;

    private float tempoRestante;
    private MaterialPropertyBlock propriedades;
    private readonly Dictionary<PlayerResources, float> proximoDano =
        new Dictionary<PlayerResources, float>();

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        propriedades = new MaterialPropertyBlock();

        if (indicador == null)
            indicador = GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        proximoDano.Clear();
        DefinirEstado(Estado.Seguro);
    }

    private void FixedUpdate()
    {
        tempoRestante -= Time.fixedDeltaTime;
        if (tempoRestante > 0f)
            return;

        switch (estadoAtual)
        {
            case Estado.Seguro:
                DefinirEstado(Estado.Aviso);
                break;
            case Estado.Aviso:
                DefinirEstado(Estado.Perigo);
                break;
            case Estado.Perigo:
                DefinirEstado(Estado.Seguro);
                break;
        }
    }

    private void DefinirEstado(Estado estado)
    {
        estadoAtual = estado;
        Color cor;

        switch (estado)
        {
            case Estado.Aviso:
                tempoRestante = Mathf.Max(0.1f, tempoAviso);
                cor = corAviso;
                break;
            case Estado.Perigo:
                tempoRestante = Mathf.Max(0.1f, tempoPerigo);
                cor = corPerigo;
                break;
            default:
                tempoRestante = Mathf.Max(0.1f, tempoSeguro);
                cor = corSegura;
                break;
        }

        if (indicador == null || indicador.sharedMaterial == null)
            return;

        indicador.GetPropertyBlock(propriedades);
        // URP/Lit usa _BaseColor; o shader Standard usa _Color.
        if (indicador.sharedMaterial.HasProperty(BaseColor))
            propriedades.SetColor(BaseColor, cor);
        if (indicador.sharedMaterial.HasProperty(ColorId))
            propriedades.SetColor(ColorId, cor);
        indicador.SetPropertyBlock(propriedades);
    }

    private void OnTriggerEnter(Collider other)
    {
        AplicarDano(other);
    }

    private void OnTriggerStay(Collider other)
    {
        AplicarDano(other);
    }

    private void AplicarDano(Collider other)
    {
        if (!isActiveAndEnabled || estadoAtual != Estado.Perigo)
            return;

        PlayerResources jogador = other.GetComponentInParent<PlayerResources>();
        if (jogador == null || jogador.IsDead)
            return;

        // Varios colliders do Ling compartilham o mesmo intervalo de dano.
        if (proximoDano.TryGetValue(jogador, out float instante) && Time.time < instante)
            return;

        // TakeDamage retorna false durante a invulnerabilidade da esquiva.
        if (jogador.TakeDamage(dano))
            proximoDano[jogador] = Time.time + Mathf.Max(0.1f, intervaloEntreDanos);
    }
}
