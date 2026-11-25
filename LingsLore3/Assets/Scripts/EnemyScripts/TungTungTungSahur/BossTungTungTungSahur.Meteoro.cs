using UnityEngine;

public sealed partial class BossTungTungTungSahur
{
    [Header("Meteoro")]
    [SerializeField, Min(1f)] private float raioDoMeteoro = 8.5f;
    [SerializeField, Min(0.3f)] private float avisoDoMeteoro = 1.05f;
    [SerializeField, Min(0f)] private float danoDoMeteoro = 42f;
    [SerializeField, Min(0.1f)] private float alturaDaElevacaoDoMeteoro = 4.8f;
    [SerializeField, Min(0.1f)] private float tempoDaElevacaoDoMeteoro = 1.15f;

    private const float AlturaDoMeteoro = 32f;
    private const float TempoDeQuedaDoMeteoro = 0.75f;
    private float proximoMeteoro;
    private Vector3 inicioDaQueda;
    private TrailRenderer rastroDoMeteoro;
    private Material materialDoMeteoro;
    private Light luzDoMeteoro;
    private ParticleSystem brilhoDoMeteoro;
    private float proximoPulsoDoMeteoro;

    private float RaioDoMeteoro => vida.CurrentBar == 3 ? raioDoMeteoro * 1.15f : raioDoMeteoro;
    private Color CorDoMeteoro => vida.CurrentBar == 3 ? Dourado : Carmesim;

    private bool TentarIniciarAtaqueEspecial(Bounds arena, Vector3 centro, Vector3 frente, float distancia)
    {
        if (vida.CurrentBar >= 2 && Time.fixedTime >= proximoMeteoro)
        {
            saidaDoSalto = centro;
            corpo.rotation = Quaternion.LookRotation(frente);
            IgnorarColisaoComLing(true);
            AcenderBrilhoDoMeteoro();
            EntrarNoEstado(Estado.ElevandoMeteoro, tempoDaElevacaoDoMeteoro);
            EfeitosSonoros.Tocar(Som.TungTung, corpo.position);
            return true;
        }

        return Time.fixedTime >= proximaInvocacao && distancia <= alcanceDasLancas &&
            TentarInvocarArmas(arena, centro, frente, distancia);
    }

    private void AtualizarMeteoro()
    {
        float t = ProgressoDoEstado;
        switch (estado)
        {
            case Estado.ElevandoMeteoro:
                float elevacao = Mathf.SmoothStep(0f, 1f, t);
                corpo.MovePosition(saidaDoSalto + Vector3.up * (alturaDaElevacaoDoMeteoro * elevacao));
                AtualizarBrilhoDoMeteoro(elevacao * 0.36f);
                if (t >= 1f)
                    EntrarNoEstado(Estado.SubindoMeteoro, 0.42f);
                break;
            case Estado.SubindoMeteoro:
                float altura = Mathf.Lerp(alturaDaElevacaoDoMeteoro, AlturaDoMeteoro, t * t);
                corpo.MovePosition(saidaDoSalto + Vector3.up * altura);
                AtualizarBrilhoDoMeteoro(Mathf.Lerp(0.36f, 0.7f, t));
                if (t < 1f)
                    return;

                colisor.enabled = false;
                ApagarBrilhoDoMeteoro();
                if (modelo != null)
                    modelo.gameObject.SetActive(false);
                EntrarNoEstado(Estado.ForaDaArena, 0.45f);
                break;
            case Estado.ForaDaArena when t >= 1f:
                PlanejarQuedaDoMeteoro();
                break;
            case Estado.MirandoMeteoro when t >= 1f:
                corpo.position = inicioDaQueda;
                if (modelo != null)
                    modelo.gameObject.SetActive(true);
                AcenderBrilhoDoMeteoro();
                PrepararRastroDoMeteoro();
                if (vida.CurrentBar == 2)
                    EfeitosSonoros.Tocar(Som.Fogo, pousoDoSalto);
                EntrarNoEstado(Estado.CaindoMeteoro, TempoDeQuedaDoMeteoro);
                break;
            case Estado.CaindoMeteoro:
                AtualizarQuedaDoMeteoro(t);
                break;
        }
    }

    private void PlanejarQuedaDoMeteoro()
    {
        Bounds arena = piso.bounds;
        if (!TentarEscolherPouso(arena, saidaDoSalto, float.PositiveInfinity, out pousoDoSalto))
            pousoDoSalto = saidaDoSalto;

        Vector3 frente = Horizontal(pousoDoSalto - saidaDoSalto).normalized;
        if (frente == Vector3.zero)
            frente = corpo.rotation * Vector3.forward;
        corpo.rotation = Quaternion.LookRotation(frente);
        inicioDaQueda = pousoDoSalto + Vector3.up * AlturaDoMeteoro - frente * 9f;
        MarcarArea(pousoDoSalto, RaioDoMeteoro, frente, 180f);
        aviso.widthMultiplier = 0.22f;
        materialDoAviso.SetColor(CorBase, CorDoMeteoro);
        EntrarNoEstado(Estado.MirandoMeteoro, avisoDoMeteoro);
    }

    private void AtualizarQuedaDoMeteoro(float t)
    {
        Vector3 posicao = Vector3.Lerp(inicioDaQueda, pousoDoSalto, Mathf.SmoothStep(0f, 1f, t / 0.74f));
        posicao.y = Mathf.Lerp(inicioDaQueda.y, pousoDoSalto.y, t * t);
        corpo.MovePosition(posicao);
        AtualizarBrilhoDoMeteoro(Mathf.Lerp(0.64f, 1f, t));
        if (t < 1f)
            return;

        corpo.position = pousoDoSalto;
        colisor.enabled = true;
        ignorandoColisaoComLing = false;
        IgnorarColisaoComLing(true);
        rastroDoMeteoro.emitting = false;
        ApagarBrilhoDoMeteoro();
        proximoMeteoro = Time.fixedTime + (vida.CurrentBar == 3 ? 19f : 22f);
        MarcarImpacto();
        aviso.widthMultiplier = 0.38f;
        EntrarNoEstado(Estado.Impacto, 0.42f);
        ReproduzirExplosao(pousoDoSalto, RaioDoMeteoro, CorDoMeteoro, 2.3f);
        if (LingNaArea(pousoDoSalto, RaioDoMeteoro, 3.1f, out _))
            ling.TakeDamage(danoDoMeteoro, pousoDoSalto);
        PillarHealth.HitArea(pousoDoSalto, RaioDoMeteoro, 3.1f, danoDoMeteoro, obstaculos);
    }

    private void PrepararRastroDoMeteoro()
    {
        if (rastroDoMeteoro == null)
        {
            Transform ancora = CriarFilho("Rastro do meteoro", transform);
            ancora.localPosition = Vector3.up * (colisor.center.y * 0.8f);
            rastroDoMeteoro = ancora.gameObject.AddComponent<TrailRenderer>();
            materialDoMeteoro = new Material(shaderDosAvisos);
            recursosVisuais.Add(materialDoMeteoro);
            rastroDoMeteoro.sharedMaterial = materialDoMeteoro;
            rastroDoMeteoro.time = 0.6f;
            rastroDoMeteoro.minVertexDistance = 0.25f;
            rastroDoMeteoro.widthMultiplier = 7.4f;
            rastroDoMeteoro.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            rastroDoMeteoro.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rastroDoMeteoro.receiveShadows = false;
        }

        Color cor = Color.Lerp(CorDoMeteoro, Color.white, 0.28f);
        materialDoMeteoro.SetColor(CorBase, new Color(cor.r * 3.8f, cor.g * 3.8f, cor.b * 3.8f, 1f));
        rastroDoMeteoro.Clear();
        rastroDoMeteoro.emitting = true;
    }

    private void AcenderBrilhoDoMeteoro()
    {
        if (luzDoMeteoro == null)
        {
            Transform ancora = CriarFilho("Luz do meteoro", transform);
            ancora.localPosition = Vector3.up * colisor.center.y;
            luzDoMeteoro = ancora.gameObject.AddComponent<Light>();
            luzDoMeteoro.type = LightType.Point;
            luzDoMeteoro.shadows = LightShadows.None;
            if (prefabExplosao != null && prefabExplosao.MaterialDasParticulas != null)
            {
                brilhoDoMeteoro = ExplosaoVfx.CriarEmissor(ancora, "Aura do meteoro", prefabExplosao.MaterialDasParticulas,
                    ParticleSystemRenderMode.Billboard);
                ExplosaoVfx.ColorirEmissor(brilhoDoMeteoro, new Color(2.8f, 2.8f, 2.8f));
            }
        }

        luzDoMeteoro.color = Color.Lerp(CorDoMeteoro, Color.white, 0.32f);
        luzDoMeteoro.intensity = 0f;
        luzDoMeteoro.enabled = true;
        if (brilhoDoMeteoro != null)
            brilhoDoMeteoro.Play();
        proximoPulsoDoMeteoro = Time.fixedTime;
    }

    private void AtualizarBrilhoDoMeteoro(float intensidade)
    {
        luzDoMeteoro.range = Mathf.Lerp(9f, 24f, intensidade);
        luzDoMeteoro.intensity = Mathf.Lerp(0.7f, 21f, intensidade * intensidade);
        if (brilhoDoMeteoro == null || Time.fixedTime < proximoPulsoDoMeteoro)
            return;

        proximoPulsoDoMeteoro = Time.fixedTime + 0.065f;
        Color aura = Color.Lerp(CorDoMeteoro, Color.white, 0.35f + intensidade * 0.45f);
        aura.a = Mathf.Lerp(0.12f, 0.48f, intensidade);
        ExplosaoVfx.Emitir(brilhoDoMeteoro, Vector3.zero, Vector3.zero, Mathf.Lerp(4f, 13f, intensidade), 0.16f, aura);
    }

    private void ApagarBrilhoDoMeteoro()
    {
        if (luzDoMeteoro != null)
            luzDoMeteoro.enabled = false;
        if (brilhoDoMeteoro != null)
            brilhoDoMeteoro.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void EncerrarVisualDoMeteoro()
    {
        if (modelo != null && !modelo.gameObject.activeSelf)
            modelo.gameObject.SetActive(true);
        if (estado is Estado.ForaDaArena or Estado.MirandoMeteoro or Estado.CaindoMeteoro)
            colisor.enabled = vida != null && !vida.IsDead;
        if (rastroDoMeteoro != null)
        {
            rastroDoMeteoro.emitting = false;
            rastroDoMeteoro.Clear();
        }
        ApagarBrilhoDoMeteoro();
    }
}
