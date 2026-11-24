using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ExplosaoVfx : MonoBehaviour
{
    [SerializeField] private Material materialDasParticulas;
    [SerializeField] private Material materialDaOnda;
    [SerializeField, Min(0.1f)] private float raio = 4f;
    [SerializeField, ColorUsage(false, true)] private Color cor = new Color(1f, 0.37f, 0.1f);
    [SerializeField, Min(0f)] private float intensidade = 1f;

    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private Light luz;
    private float inicio;

    public Material MaterialDasParticulas => materialDasParticulas;

    public void Configurar(float novoRaio, Color novaCor, float novaIntensidade = 1f)
    {
        raio = Mathf.Max(0.1f, novoRaio);
        cor = novaCor;
        intensidade = Mathf.Max(0f, novaIntensidade);
    }

    private void Start()
    {
        if (materialDasParticulas == null || materialDaOnda == null)
        {
            Destroy(gameObject);
            return;
        }

        Color clara = Color.Lerp(cor, Color.white, 0.66f);
        ParticleSystem flash = CriarEmissor(transform, "Clarão", materialDasParticulas, ParticleSystemRenderMode.Billboard);
        ColorirEmissor(flash, BrancoComBrilho(1.4f + intensidade));
        Emitir(flash, Vector3.up * (raio * 0.23f), Vector3.zero, raio * 1.36f, 0.28f, clara);

        ParticleSystem onda = CriarEmissor(transform, "Onda de choque", materialDaOnda, ParticleSystemRenderMode.HorizontalBillboard);
        var tamanho = onda.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.15f, 0f, 2.6f), new Keyframe(1f, 1f, 0.15f, 0f)));
        ColorirEmissor(onda, BrancoComBrilho(1f + intensidade));
        Emitir(onda, Vector3.up * 0.09f, Vector3.zero, raio * 2.25f, 0.62f, cor);

        ParticleSystem fagulhas = CriarEmissor(transform, "Fagulhas", materialDasParticulas, ParticleSystemRenderMode.Stretch);
        ParticleSystemRenderer renderFagulhas = fagulhas.GetComponent<ParticleSystemRenderer>();
        renderFagulhas.lengthScale = 2.7f;
        renderFagulhas.velocityScale = 0.13f;
        ColorirEmissor(fagulhas, BrancoComBrilho(1.35f + intensidade));
        var fisica = fagulhas.main;
        fisica.gravityModifier = 1.3f;
        int quantidade = Mathf.Clamp(Mathf.RoundToInt(26f + raio * 4f), 30, 70);
        for (int i = 0; i < quantidade; i++)
        {
            float angulo = Random.Range(0f, 2f * Mathf.PI);
            Vector3 direcao = new Vector3(Mathf.Cos(angulo), Random.Range(0.16f, 0.95f), Mathf.Sin(angulo));
            Emitir(fagulhas, Vector3.up * 0.28f, direcao * Random.Range(raio * 0.65f, raio * 2.1f),
                Random.Range(0.10f, 0.29f) * Mathf.Sqrt(raio), Random.Range(0.36f, 0.94f),
                Color.Lerp(cor, clara, Random.value * 0.7f));
        }

        var objetoDaLuz = new GameObject("Luz do impacto");
        objetoDaLuz.transform.SetParent(transform, false);
        objetoDaLuz.transform.localPosition = Vector3.up * Mathf.Min(2f, raio * 0.35f);
        luz = objetoDaLuz.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = clara;
        luz.range = raio * 2.1f;
        luz.intensity = 5f * intensidade;
        inicio = Time.time;
        Destroy(gameObject, 1.15f);
    }

    private void Update()
    {
        if (luz == null)
            return;

        float restante = 1f - Mathf.Clamp01((Time.time - inicio) / 0.24f);
        luz.intensity = 5f * intensidade * restante * restante;
    }

    internal static ParticleSystem CriarEmissor(Transform pai, string nome, Material material,
        ParticleSystemRenderMode modo, ParticleSystemSimulationSpace espaco = ParticleSystemSimulationSpace.Local)
    {
        var objeto = new GameObject(nome);
        objeto.SetActive(false);
        objeto.transform.SetParent(pai, false);
        ParticleSystem sistema = objeto.AddComponent<ParticleSystem>();
        var principal = sistema.main;
        principal.playOnAwake = false;
        principal.loop = false;
        principal.simulationSpace = espaco;
        principal.scalingMode = ParticleSystemScalingMode.Local;
        principal.maxParticles = 96;
        principal.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emissao = sistema.emission;
        emissao.enabled = false;
        var forma = sistema.shape;
        forma.enabled = false;

        var desvanecer = sistema.colorOverLifetime;
        desvanecer.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.78f, 0.30f), new GradientAlphaKey(0f, 1f) });
        desvanecer.color = gradiente;

        var renderizador = objeto.GetComponent<ParticleSystemRenderer>();
        renderizador.sharedMaterial = material;
        renderizador.renderMode = modo;
        renderizador.shadowCastingMode = ShadowCastingMode.Off;
        renderizador.receiveShadows = false;
        objeto.SetActive(true);
        sistema.Play();
        return sistema;
    }

    internal static void ColorirEmissor(ParticleSystem sistema, Color cor)
    {
        var propriedades = new MaterialPropertyBlock();
        propriedades.SetColor(CorBase, cor);
        sistema.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(propriedades);
    }

    internal static void Emitir(ParticleSystem sistema, Vector3 posicao, Vector3 velocidade, float tamanho,
        float duracao, Color cor)
    {
        var parametros = new ParticleSystem.EmitParams
        {
            position = posicao,
            velocity = velocidade,
            startSize = tamanho,
            startLifetime = duracao,
            startColor = cor
        };
        sistema.Emit(parametros, 1);
    }

    private static Color BrancoComBrilho(float brilho) => new Color(brilho, brilho, brilho, 1f);
}
