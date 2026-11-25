using System.Collections;
using UnityEngine;

public sealed partial class BossNetanyahu
{
    [Header("Segunda fase - robô")]
    [SerializeField] private ExplosaoVfx prefabExplosaoRobo;
    [SerializeField] private MiniTralalero prefabMiniNetanyahu;
    [SerializeField, Min(1f)] private float tempoParaRessurgir = 2.8f;
    [SerializeField, Min(0f)] private float intervaloDoComboRobo = 20f;

    private const float MultiplicadorDeDanoRobo = 1.5f;
    private const float RaioDaPancadaRobo = 8.6f;
    private const float RaioDoGiroRobo = 10f;
    private const float DuracaoDoStunRobo = 4f;
    private static readonly Color CorLaser = new Color(0.15f, 1f, 1.8f);
    private static readonly Color CorLaserDoCombo = new Color(1.8f, 0.18f, 0.42f);

    private float proximoComboRobo;
    private int projeteisLaserAtivos;

    private IEnumerator RenascerComoRobo()
    {
        estado = Estado.Transicao;
        colisaoDoBoss.enabled = false;
        IgnorarColisaoComLing(false);
        if (visualHumano != null)
            visualHumano.enabled = false;
        if (robo != null)
            robo.SetActive(false);

        Vector3 centro = corpo.position;
        centro.y = piso.bounds.max.y;
        ExplodirRobo(centro, 7.2f, Vermelho, 2f);
        yield return new WaitForSeconds(tempoParaRessurgir);

        ExibirForma(true);
        ExplodirRobo(centro, 3.6f, CorLaser, 0.8f);
        yield return new WaitForSeconds(0.65f);
        colisaoDoBoss.enabled = true;
    }

    private void ExplodirRobo(Vector3 ponto, float raio, Color cor, float intensidade)
    {
        EfeitosSonoros.Tocar(Som.Explosao, ponto);
        if (prefabExplosaoRobo == null)
            return;

        ExplosaoVfx explosao = Instantiate(prefabExplosaoRobo, ponto, Quaternion.identity);
        explosao.Configurar(raio, cor, intensidade);
    }

    private IEnumerator ExecutarRodadaRobo()
    {
        if (PodeGirarComTung && vida.CurrentHealth <= vida.MaxHealth * 0.5f && Time.time >= proximoComboRobo)
        {
            ataqueEscolhido = Ataque.ComboRobo;
            ultimoAtaqueNoChao = ataqueEscolhido;
            yield return ExecutarComboRobo();
            yield break;
        }

        ataquesDisponiveis.Clear();
        ataquesDisponiveis.Add(Ataque.Laser);
        if (prefabMetralhadora != null)
            ataquesDisponiveis.Add(Ataque.RajadaRobo);
        if (PodeInvocarMinis(prefabMiniNetanyahu))
            ataquesDisponiveis.Add(Ataque.MinisRobo);
        if (PodeGirarComTung)
            ataquesDisponiveis.Add(Ataque.GiroTung);
        ataqueEscolhido = SortearAtaque(ref ultimoAtaqueNoChao);
        switch (ataqueEscolhido)
        {
            case Ataque.Laser:
                yield return ExecutarQuandoPerto(DispararSalvaLaser(rapida: false), 23f);
                break;
            case Ataque.RajadaRobo:
                yield return ExecutarQuandoPerto(ExecutarRajada(robotica: true), 22f);
                break;
            case Ataque.MinisRobo:
                yield return ExecutarQuandoPerto(ComandarMinis(prefabMiniNetanyahu, roboticos: true), distanciaParaInvocar);
                break;
            case Ataque.GiroTung:
                yield return ExecutarQuandoPerto(ExecutarGiroComModelo(robotico: true), RaioDoGiroRobo);
                break;
        }
    }

    private IEnumerator ExecutarComboRobo()
    {
        estado = Estado.Preparando;
        corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());
        AcenderOlhoDoCombo();
        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(1.05f));
        ApagarOlhoDoCombo();

        yield return AproximarParaComboRobo(RaioDaPancadaRobo, 1f);
        yield return ExecutarGiroComModelo(robotico: true, frontal: true);
        if (LutaEncerrada)
            yield break;

        yield return new WaitForSeconds(0.45f);
        yield return DispararSalvaLaser(rapida: true);
        for (int i = 0; i < 2 && !LutaEncerrada; i++)
        {
            yield return new WaitForSeconds(0.45f);
            yield return AproximarParaComboRobo(RaioDoGiroRobo, 0.78f);
            yield return ExecutarGiroComModelo(robotico: true);
        }
        if (LutaEncerrada)
            yield break;

        proximoComboRobo = Time.time + intervaloDoComboRobo;
        estado = Estado.Atordoado;
        ataqueEscolhido = Ataque.Nenhum;
        CriarEspiralDoStun();
        yield return new WaitForSeconds(DuracaoDoStunRobo);
        LimparEspiralDoStun();
        estado = Estado.Descansando;
    }

    private IEnumerator AproximarParaComboRobo(float raio, float duracao)
    {
        estado = Estado.Aproximando;
        float prazo = Time.fixedTime + duracao;
        float lado = 0f;
        while (!LutaEncerrada && Time.fixedTime < prazo)
        {
            Vector3 ateLing = VetorHorizontal(corpoDoLing.position - corpo.position);
            float distancia = ateLing.magnitude;
            if (distancia <= raio * 0.68f)
                yield break;

            Vector3 direcao = DirecaoDaCorrida(ateLing / distancia, distancia, ref lado);
            corpo.MoveRotation(Quaternion.LookRotation(direcao));
            Mover(corpo.position + direcao * (18f * Time.fixedDeltaTime));
            yield return passoFisico;
        }
    }

    private IEnumerator DispararSalvaLaser(bool rapida)
    {
        estado = Estado.Preparando;
        CriarEfeitos(rapida ? "Salva laser do combo" : "Mísseis laser");
        Color cor = rapida ? CorLaserDoCombo : CorLaser;
        int quantidade = rapida ? 8 : 5;
        float intervalo = rapida ? 0.08f : 0.24f;
        float avisoPorMissil = BalanceamentoDificuldade.Atual.Aviso(rapida ? 0.15f : 0.26f, rapida ? 0.15f : 0.22f);
        var primeiraMira = CriarLinha("Carga dos mísseis", 2);
        primeiraMira.loop = false;
        ConfigurarVisual(primeiraMira, cor);
        corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());
        primeiraMira.SetPosition(0, OrigemDoLaser(-1));
        primeiraMira.SetPosition(1, colisaoDoLing.bounds.center);
        yield return new WaitForSeconds(BalanceamentoDificuldade.Atual.Aviso(rapida ? 0.43f : 0.75f));
        primeiraMira.enabled = false;
        estado = Estado.Atacando;

        float dano = (rapida ? 8f : 12f) * MultiplicadorDeDanoRobo;
        float proximoDano = float.NegativeInfinity;
        for (int i = 0; i < quantidade; i++)
        {
            corpo.rotation = Quaternion.LookRotation(DirecaoDoLing());
            Vector3 origem = OrigemDoLaser(i % 2 == 0 ? -1 : 1);
            Vector3 alvo = colisaoDoLing.bounds.center;
            Vector3 direcao = (alvo - origem).normalized;
            if (direcao == Vector3.zero)
                direcao = transform.forward;

            StartCoroutine(MoverMissilLaser(origem, direcao, cor, rapida, avisoPorMissil, dano, TentarDano));
            yield return new WaitForSeconds(intervalo);
        }

        while (projeteisLaserAtivos > 0)
            yield return null;
        LimparEfeitos();

        void TentarDano(Vector3 origem)
        {
            if (Time.time >= proximoDano && ling.TakeBossDamage(dano, origem))
                proximoDano = Time.time + (rapida ? 0.23f : 0.30f);
        }
    }

    private Vector3 OrigemDoLaser(int lado) => transform.TransformPoint(new Vector3(lado * 0.48f, 0.22f, 0.44f));

    private IEnumerator MoverMissilLaser(Vector3 posicao, Vector3 direcao, Color cor, bool rapida, float aviso, float dano,
        System.Action<Vector3> tentarDano)
    {
        projeteisLaserAtivos++;
        Vector3 origem = posicao;
        LineRenderer linha = CriarLinha("Míssil laser", 2);
        linha.loop = false;
        linha.widthMultiplier = rapida ? 0.10f : 0.14f;
        linha.numCapVertices = 4;
        ConfigurarVisual(linha, cor * 0.75f);
        linha.SetPosition(0, origem);
        int barreiras = obstaculos.value | (1 << piso.gameObject.layer);
        float alcance = 40f;
        if (Physics.Raycast(origem, direcao, out RaycastHit parede, alcance, barreiras, QueryTriggerInteraction.Ignore))
            linha.SetPosition(1, parede.point);
        else
            linha.SetPosition(1, origem + direcao * alcance);

        yield return new WaitForSeconds(aviso);
        EfeitosSonoros.Tocar(Som.Laser, origem);
        linha.widthMultiplier = rapida ? 0.33f : 0.44f;
        Colorir(linha, cor * 2f);
        linha.SetPosition(0, posicao);
        linha.SetPosition(1, posicao);
        float raio = rapida ? 0.16f : 0.22f;
        float velocidade = BalanceamentoDificuldade.Atual.Velocidade(rapida ? 51f : 32f);
        int mascara = barreiras | (1 << ling.gameObject.layer);
        float percorrido = 0f;
        while (percorrido < alcance && !LutaEncerrada)
        {
            yield return passoFisico;
            float passo = Mathf.Min(velocidade * Time.fixedDeltaTime, alcance - percorrido);
            bool noLing = (colisaoDoLing.ClosestPoint(posicao) - posicao).sqrMagnitude <= raio * raio;
            bool colidiu = Physics.SphereCast(posicao, raio, direcao, out RaycastHit acerto, passo,
                mascara, QueryTriggerInteraction.Ignore);
            float avanco = noLing ? 0f : colidiu ? acerto.distance : passo;
            posicao += direcao * avanco;
            percorrido += avanco;
            linha.SetPosition(0, posicao);
            linha.SetPosition(1, posicao - direcao * Mathf.Min(percorrido, rapida ? 2.5f : 3.1f));
            if (!noLing && !colidiu)
                continue;

            if (noLing || acerto.collider == colisaoDoLing)
                tentarDano(origem);
            else
                PillarHealth.HitCollider(acerto.collider, dano, acerto.point);
            if (LutaEncerrada)
                yield break;
            yield return null;
            break;
        }

        projeteisLaserAtivos--;
        Destroy(linha.gameObject);
    }
}
