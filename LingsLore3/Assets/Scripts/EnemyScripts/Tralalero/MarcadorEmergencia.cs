using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MarcadorEmergencia : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color corInicial = Color.yellow;
    [SerializeField] private Color corFinal = Color.red;
    [SerializeField, Range(0.01f, 1f)] private float escalaFinal = 0.25f;
    [SerializeField, Min(0.01f)] private float duracaoPadrao = 1.5f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private Renderer renderizador;
    private MaterialPropertyBlock propriedades;
    private Vector3 escalaInicial;
    private int propriedadeCor;
    private float inicio;
    private float duracao;

    private void Awake()
    {
        renderizador = GetComponent<Renderer>();
        propriedades = new MaterialPropertyBlock();
        escalaInicial = transform.localScale;

        Material material = renderizador.sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty(BaseColor))
                propriedadeCor = BaseColor;
            else if (material.HasProperty(ColorProperty))
                propriedadeCor = ColorProperty;
        }

        Iniciar(duracaoPadrao);
    }

    public void Iniciar(float tempoDeAviso)
    {
        duracao = Mathf.Max(0.01f, tempoDeAviso);
        inicio = Time.time;
        transform.localScale = escalaInicial;
        AtualizarVisual(0f);
    }

    private void Update()
    {
        float progresso = Mathf.Clamp01((Time.time - inicio) / duracao);
        AtualizarVisual(progresso);
    }

    private void AtualizarVisual(float progresso)
    {
        transform.localScale = escalaInicial * Mathf.Lerp(1f, escalaFinal, progresso);

        if (propriedadeCor == 0)
            return;

        renderizador.GetPropertyBlock(propriedades);
        propriedades.SetColor(propriedadeCor, Color.Lerp(corInicial, corFinal, progresso));
        renderizador.SetPropertyBlock(propriedades);
    }
}
