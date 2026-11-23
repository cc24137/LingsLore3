using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class BossNetanyahu
{
    [Header("Bomba massiva")]
    [SerializeField, Min(0.1f)] private float preparacaoDaBombaMassiva = 4.3f;
    [SerializeField, Min(0.1f)] private float alturaDaExplosaoMassiva = 9f;
    [SerializeField, Min(0f)] private float danoDaBombaMassiva = 38f;
    [SerializeField, Min(0f)] private float intervaloDaBombaMassiva = 16f;

    [Header("Giro com o Tralalero")]
    [SerializeField] private GameObject modeloTralaleroDoGiro;
    [SerializeField, Min(0.1f)] private float raioDoGiro = 9f;
    [SerializeField, Min(0.1f)] private float alturaDoGiro = 4f;
    [SerializeField, Min(0.1f)] private float avisoDoGiro = 1.25f;
    [SerializeField, Min(0.1f)] private float duracaoDoGiro = 0.30f;
    [SerializeField, Min(0f)] private float danoDoGiro = 24f;

    private float proximaBombaMassiva;
    private float fimDoAvisoMassivo;
    private Material materialDoVolumeMassivo;

    private bool PodeUsarBombaMassiva => prefabBomba != null && Time.time >= proximaBombaMassiva;

    private IEnumerator ExecutarBombaMassiva()
    {
        estado = Estado.Preparando;
        Bounds arena = piso.bounds;
        var centro = new Vector3(arena.center.x, arena.max.y, arena.center.z);
        float raio = RaioDoPiso(arena);
        float inicio = Time.time;
        float duracaoDoAviso = preparacaoDaBombaMassiva + prefabBomba.TempoAteDetonar;
        fimDoAvisoMassivo = inicio + duracaoDoAviso;
        proximaBombaMassiva = fimDoAvisoMassivo + intervaloDaBombaMassiva;

        CriarEfeitos("Zona da bomba massiva");
        var volume = CriarVolumeMassivo(centro, raio);
        LineRenderer baseDoVolume = MarcarArea(centro, raio, Vector3.forward, 180f);
        LineRenderer limite = MarcarArea(centro + Vector3.up * alturaDaExplosaoMassiva, raio, Vector3.forward, 180f);
        LineRenderer contagem = MarcarArea(centro, raio, Vector3.forward, 180f);
        Colorir(contagem, Vermelho);

        Bomba bomba = Instantiate(prefabBomba, PosicaoDaBombaMassiva(), Quaternion.identity, efeitosDoAtaque.transform);
        bomba.transform.localScale *= 5f;
        while (Time.time < inicio + preparacaoDaBombaMassiva)
        {
            bomba.transform.position = PosicaoDaBombaMassiva();
            AtualizarMarcacaoMassiva(contagem, centro, raio, inicio, duracaoDoAviso);
            yield return null;
        }

        estado = Estado.Atacando;
        bomba.Explodiu += _ =>
        {
            fimDoAvisoMassivo = 0f;
            MostrarImpacto(limite);
            MostrarImpacto(baseDoVolume);
            Colorir(volume, new Color(1f, 0.09f, 0.025f, 0.34f));
            contagem.enabled = false;
            AplicarExplosaoMassiva(centro, raio);
        };
        bomba.LancarComExplosaoPersonalizada(centro);
        while (bomba != null)
        {
            if (contagem.enabled)
                AtualizarMarcacaoMassiva(contagem, centro, raio, inicio, duracaoDoAviso);
            yield return null;
        }

        LimparEfeitos();
    }

    private Vector3 PosicaoDaBombaMassiva() => transform.position + transform.right * 1.45f + Vector3.up * 1.3f;

    private void AtualizarMarcacaoMassiva(LineRenderer aro, Vector3 centro, float raio, float inicio, float duracao)
    {
        float progresso = Mathf.Clamp01((Time.time - inicio) / duracao);
        PosicionarArco(aro, centro + Vector3.up * (alturaDaExplosaoMassiva * progresso), raio,
            Vector3.forward, 180f, fecharSetor: false);
    }

    private Renderer CriarVolumeMassivo(Vector3 centro, float raio)
    {
        if (materialDoVolumeMassivo == null)
        {
            materialDoVolumeMassivo = new Material(shaderDosAvisos);
            materialDoVolumeMassivo.SetOverrideTag("RenderType", "Transparent");
            materialDoVolumeMassivo.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            materialDoVolumeMassivo.SetFloat("_Surface", 1f);
            materialDoVolumeMassivo.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            materialDoVolumeMassivo.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            materialDoVolumeMassivo.SetInt("_ZWrite", 0);
            materialDoVolumeMassivo.SetInt("_Cull", (int)CullMode.Off);
            materialDoVolumeMassivo.renderQueue = (int)RenderQueue.Transparent;
        }

        var cilindro = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cilindro.name = "Volume de dano abaixo do aro";
        cilindro.GetComponent<Collider>().enabled = false;
        cilindro.transform.SetParent(efeitosDoAtaque.transform, false);
        cilindro.transform.position = centro + Vector3.up * (alturaDaExplosaoMassiva * 0.5f);
        cilindro.transform.localScale = new Vector3(raio * 2f, alturaDaExplosaoMassiva * 0.5f, raio * 2f);
        Renderer visual = cilindro.GetComponent<Renderer>();
        visual.sharedMaterial = materialDoVolumeMassivo;
        visual.shadowCastingMode = ShadowCastingMode.Off;
        visual.receiveShadows = false;
        Colorir(visual, new Color(1f, 0.30f, 0.05f, 0.12f));
        return visual;
    }

    private void AplicarExplosaoMassiva(Vector3 centro, float raio)
    {
        Bounds alvo = colisaoDoLing.bounds;
        if (VetorHorizontal(alvo.center - centro).sqrMagnitude <= raio * raio &&
            alvo.min.y < centro.y + alturaDaExplosaoMassiva && alvo.max.y >= centro.y)
            ling.TakeDamage(danoDaBombaMassiva, centro, ignorarInvulnerabilidadeDaEsquiva: true);
    }

    private IEnumerator ExecutarGiroTralalero()
    {
        estado = Estado.Preparando;
        CriarEfeitos("Giro com o Tralalero");
        Vector3 centro = corpo.position;
        centro.y = piso.bounds.max.y;
        LineRenderer aviso = MarcarArea(centro, raioDoGiro, Vector3.forward, 180f);
        Quaternion orientacao = Quaternion.LookRotation(DirecaoDoLing());
        var pivo = new GameObject("Pivô do giro").transform;
        pivo.SetParent(efeitosDoAtaque.transform, false);
        pivo.position = centro;
        Transform modelo = Instantiate(modeloTralaleroDoGiro, pivo, false).transform;
        modelo.localPosition = Vector3.zero;
        modelo.localRotation = Quaternion.identity;
        Renderer[] partes = modelo.GetComponentsInChildren<Renderer>();
        if (partes.Length == 0)
        {
            LimparEfeitos();
            yield break;
        }

        Bounds limites = partes[0].bounds;
        for (int i = 1; i < partes.Length; i++)
            limites.Encapsulate(partes[i].bounds);
        float fator = raioDoGiro * 0.86f / Mathf.Max(0.1f, limites.size.z);
        Vector3 escala = modelo.localScale * fator;
        Vector3 baseDoModelo = new Vector3(limites.center.x, limites.min.y, limites.center.z) - centro;
        Vector3 posicao = new Vector3(0f, 0.12f, raioDoGiro * 0.55f) - baseDoModelo * fator;

        const float anguloInicial = -72f;
        Quaternion poseInicial = corpo.rotation;
        Quaternion poseDePreparacao = orientacao * Quaternion.Euler(0f, anguloInicial, 0f);
        for (float tempo = 0f; tempo < avisoDoGiro; tempo += Time.deltaTime)
        {
            float progresso = Mathf.SmoothStep(0f, 1f, tempo / avisoDoGiro);
            float saque = Mathf.Lerp(0.23f, 1f, progresso);
            modelo.localScale = escala * saque;
            modelo.localPosition = posicao * saque;
            corpo.rotation = Quaternion.Slerp(poseInicial, poseDePreparacao, progresso);
            pivo.rotation = corpo.rotation;
            yield return null;
        }

        modelo.localScale = escala;
        modelo.localPosition = posicao;
        MostrarImpacto(aviso);
        estado = Estado.Atacando;
        bool contatoResolvido = false;
        float anterior = anguloInicial;
        for (float tempo = 0f; tempo < duracaoDoGiro;)
        {
            yield return passoFisico;
            tempo += Time.fixedDeltaTime;
            float atual = anguloInicial + Mathf.Clamp01(tempo / duracaoDoGiro) * 360f;
            pivo.rotation = orientacao * Quaternion.Euler(0f, atual, 0f);
            corpo.rotation = pivo.rotation;
            if (!contatoResolvido && LingNoGiro(centro, orientacao * Vector3.forward, anterior, atual))
            {
                contatoResolvido = true;
                ling.TakeDamage(danoDoGiro, centro);
                if (LutaEncerrada)
                    yield break;
            }
            anterior = atual;
        }

        LimparEfeitos();
    }

    private bool LingNoGiro(Vector3 centro, Vector3 frente, float anterior, float atual)
    {
        Bounds alvo = colisaoDoLing.bounds;
        Vector3 delta = VetorHorizontal(alvo.center - centro);
        float distancia = delta.magnitude;
        float raioDoJogador = Mathf.Max(alvo.extents.x, alvo.extents.z);
        if (distancia > raioDoGiro + raioDoJogador || alvo.min.y >= centro.y + alturaDoGiro || alvo.max.y < centro.y)
            return false;

        float margemLateral = 1.15f + raioDoJogador;
        if (distancia <= margemLateral)
            return true;

        float margemAngular = Mathf.Asin(margemLateral / distancia) * Mathf.Rad2Deg;
        float anguloDoAlvo = Vector3.SignedAngle(frente, delta, Vector3.up);
        float ateAlvo = Mathf.Repeat(anguloDoAlvo - anterior, 360f);
        return ateAlvo <= atual - anterior + margemAngular || ateAlvo >= 360f - margemAngular;
    }
}
