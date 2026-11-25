using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MetralhadoraBigYahu : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private GameObject visual;
    [SerializeField] private Transform saidaDosProjeteis;
    [SerializeField] private GameObject clarao;
    [SerializeField] private Material materialDosRastros;

    [Header("Rajada")]
    [SerializeField, Min(0.1f)] private float tempoDeAviso = 1f;
    [SerializeField, Min(1)] private int quantidadeDeDisparos = 9;
    [SerializeField, Min(0.02f)] private float intervaloEntreDisparos = 0.11f;
    [SerializeField, Min(0.1f)] private float velocidadeDoProjetil = 34f;
    [SerializeField, Min(0.1f)] private float alcanceDoProjetil = 65f;
    [SerializeField, Min(0f)] private float danoDoProjetil = 7f;
    [SerializeField, Min(0f)] private float intervaloDoDano = 0.28f;

    private static readonly Vector3 PosicaoGuardada = new Vector3(0.8f, -0.55f, 0.15f);
    private static readonly Vector3 PosicaoAoMirar = new Vector3(1.05f, 0.5f, 0.7f);
    private static readonly Color CorDaMira = new Color(1f, 0.34f, 0.14f);
    private static readonly Color CorDoProjetil = new Color(1f, 0.82f, 0.28f);
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private readonly WaitForFixedUpdate passoFisico = new WaitForFixedUpdate();

    private Transform piloto;
    private PlayerResources alvo;
    private Collider colisorDoAlvo;
    private LineRenderer linhaDaMira;
    private Vector3 pontoMirado;
    private int mascaraDosProjeteis;
    private int projeteisEmVoo;
    private float inicioDoAviso;
    private float travaDaMira;
    private float fimDoClarao;
    private float proximoDano;
    private bool montada;

    public IEnumerator SacarEMirar(Transform atacante, PlayerResources jogador, Collider colisor, LayerMask barreiras,
        bool fixada = false)
    {
        piloto = atacante;
        alvo = jogador;
        montada = fixada;
        colisorDoAlvo = colisor;
        mascaraDosProjeteis = barreiras.value | (1 << colisor.gameObject.layer);
        pontoMirado = colisor.bounds.center;
        inicioDoAviso = Time.time;
        float aviso = BalanceamentoDificuldade.Atual.Aviso(tempoDeAviso, 0.65f);
        travaDaMira = inicioDoAviso + aviso - Mathf.Max(0.28f, aviso * 0.35f);
        linhaDaMira = CriarLinha("Mira da rajada", 0.045f, CorDaMira);
        AtualizarPosicaoDaArma();
        yield return new WaitForSeconds(aviso);
        linhaDaMira.enabled = false;
    }

    public IEnumerator Disparar(int disparosExtras = 0, float ajusteDaMiraPorSegundo = 0f,
        float multiplicadorDeDano = 1f)
    {
        float dano = danoDoProjetil * multiplicadorDeDano;
        for (int i = 0; i < quantidadeDeDisparos + disparosExtras; i++)
        {
            if (ajusteDaMiraPorSegundo > 0f)
                pontoMirado = Vector3.MoveTowards(pontoMirado, colisorDoAlvo.bounds.center,
                    ajusteDaMiraPorSegundo * intervaloEntreDisparos);
            AtualizarPosicaoDaArma();
            Vector3 origem = saidaDosProjeteis.position;
            Vector3 direcao = transform.forward;
            if (Vector3.Dot(pontoMirado - origem, direcao) <= 0f)
                origem = transform.position;

            fimDoClarao = Time.time + 0.045f;
            clarao.SetActive(true);
            EfeitosSonoros.Tocar(Som.Tiro, origem);
            projeteisEmVoo++;
            StartCoroutine(MoverProjetil(origem, direcao, dano));
            yield return new WaitForSeconds(intervaloEntreDisparos);
        }

        visual.SetActive(false);
        while (projeteisEmVoo > 0)
            yield return null;
    }

    private void LateUpdate()
    {
        if (piloto == null || !visual.activeSelf)
            return;

        AtualizarPosicaoDaArma();
        if (clarao.activeSelf && Time.time >= fimDoClarao)
            clarao.SetActive(false);
    }

    private void AtualizarPosicaoDaArma()
    {
        if (Time.time < travaDaMira)
            pontoMirado = colisorDoAlvo.bounds.center;

        float saque = Mathf.SmoothStep(0f, 1f, (Time.time - inicioDoAviso) / 0.35f);
        Vector3 deslocamento = montada ? Vector3.zero : Vector3.Lerp(PosicaoGuardada, PosicaoAoMirar, saque);
        Vector3 posicao = piloto.position + piloto.rotation * deslocamento;
        Vector3 direcao = pontoMirado - posicao;
        if (direcao.sqrMagnitude < 0.001f)
            direcao = piloto.forward;

        transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(direcao));
        if (linhaDaMira.enabled)
        {
            linhaDaMira.SetPosition(0, saidaDosProjeteis.position);
            linhaDaMira.SetPosition(1, pontoMirado);
        }
    }

    private IEnumerator MoverProjetil(Vector3 posicao, Vector3 direcao, float dano)
    {
        const float raio = 0.10f;
        const float comprimentoDoRastro = 0.95f;
        if ((colisorDoAlvo.ClosestPoint(posicao) - posicao).sqrMagnitude <= raio * raio)
        {
            TentarCausarDano(posicao, dano);
            projeteisEmVoo--;
            yield break;
        }

        LineRenderer rastro = CriarLinha("Projétil da rajada", 0.095f, CorDoProjetil);
        rastro.SetPosition(0, posicao);
        rastro.SetPosition(1, posicao);
        float percorrido = 0f;
        while (percorrido < alcanceDoProjetil)
        {
            yield return passoFisico;
            float passo = Mathf.Min(BalanceamentoDificuldade.Atual.Velocidade(velocidadeDoProjetil) * Time.fixedDeltaTime, alcanceDoProjetil - percorrido);
            if (Physics.SphereCast(posicao, raio, direcao, out RaycastHit acerto, passo,
                mascaraDosProjeteis, QueryTriggerInteraction.Ignore))
            {
                Vector3 ponta = posicao + direcao * acerto.distance;
                rastro.SetPosition(0, ponta);
                rastro.SetPosition(1, ponta - direcao * Mathf.Min(comprimentoDoRastro, percorrido + acerto.distance));

                if (acerto.collider == colisorDoAlvo)
                    TentarCausarDano(posicao, dano);
                else
                    PillarHealth.HitCollider(acerto.collider, dano, acerto.point);
                yield return null;
                break;
            }

            posicao += direcao * passo;
            percorrido += passo;
            rastro.SetPosition(0, posicao);
            rastro.SetPosition(1, posicao - direcao * Mathf.Min(comprimentoDoRastro, percorrido));
        }

        projeteisEmVoo--;
        Destroy(rastro.gameObject);
    }

    private void TentarCausarDano(Vector3 origem, float dano)
    {
        if (Time.time >= proximoDano && alvo.TakeBossDamage(dano, origem))
            proximoDano = Time.time + intervaloDoDano;
    }

    private LineRenderer CriarLinha(string nome, float largura, Color cor)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(transform, false);
        var linha = objeto.AddComponent<LineRenderer>();
        linha.useWorldSpace = true;
        linha.positionCount = 2;
        linha.widthMultiplier = largura;
        linha.numCapVertices = 2;
        linha.sharedMaterial = materialDosRastros;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.receiveShadows = false;
        var propriedades = new MaterialPropertyBlock();
        propriedades.SetColor(CorBase, cor);
        linha.SetPropertyBlock(propriedades);
        return linha;
    }
}
