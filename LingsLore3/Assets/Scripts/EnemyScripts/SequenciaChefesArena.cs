using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SequenciaChefesArena : MonoBehaviour
{
    [Serializable]
    private sealed class AcessoArena
    {
        public PortaoArena entrada;
        public PortaoArena saida;
        public Transform pontoRetentativa;
        public LuzesDoCorredor corredorDeEntrada;
        public GameObject iluminacao;
    }

    [SerializeField] private EnemyHealth[] chefes;
    [SerializeField] private string[] nomesDosChefes;
    [SerializeField] private PlayerResources jogador;
    [SerializeField] private FimDeJogo fimDeJogo;
    [SerializeField, Min(0f)] private float intervaloEntreChefes = 0.75f;
    [SerializeField] private AcessoArena[] acessos;
    [SerializeField] private ContadorCreditos tutorial;

    [Header("Trilha sonora")]
    [SerializeField] private AudioClip musicaInterludio;
    [Tooltip("Mesma ordem da lista de chefes. Uma faixa vazia usa o interlúdio.")]
    [SerializeField] private AudioClip[] musicasDosChefes;
    [SerializeField, Range(0f, 1f)] private float volumeDaTrilha = 0.55f;

    private static int etapaDaRetentativa;
    private static string cenaDaRetentativa;
    private int etapaAtual;
    private EnemyHealth chefeAtual;
    private PortaoArena entradaAtual;
    private bool retomando;
    private AudioSource trilha;

    private bool TemAcessos => acessos != null && acessos.Length > 0;

    internal EnemyHealth ChefeAtual => chefeAtual;

    internal string NomeDoChefeAtual
    {
        get
        {
            if (chefeAtual == null)
                return string.Empty;

            string nome = nomesDosChefes != null && etapaAtual < nomesDosChefes.Length
                ? nomesDosChefes[etapaAtual]
                : null;
            return string.IsNullOrWhiteSpace(nome) ? chefeAtual.gameObject.name : nome;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRetentativa()
    {
        etapaDaRetentativa = 0;
        cenaDaRetentativa = null;
    }

    private void Awake()
    {
        if (jogador == null || fimDeJogo == null || chefes == null || chefes.Length == 0 ||
            Array.Exists(chefes, chefe => chefe == null))
        {
            Debug.LogError("Configure os chefes da arena, Ling e a tela de fim de jogo.", this);
            enabled = false;
            return;
        }

        if (TemAcessos && (acessos.Length != chefes.Length ||
            Array.Exists(acessos, acesso => acesso == null || acesso.entrada == null ||
                acesso.pontoRetentativa == null) ||
            Array.FindIndex(acessos, 0, chefes.Length - 1, acesso => acesso.saida == null) >= 0))
        {
            Debug.LogError("Configure a entrada, a saída e o ponto de retentativa de cada arena.", this);
            enabled = false;
            return;
        }

        // Só uma recarga solicitada pela tela de derrota retoma uma etapa.
        retomando = cenaDaRetentativa == gameObject.scene.path;
        etapaAtual = retomando ? Mathf.Clamp(etapaDaRetentativa, 0, chefes.Length - 1) : 0;
        if (retomando)
            tutorial?.IgnorarNaRetentativa();
        LimparRetentativa();

        for (int i = 0; i < chefes.Length; i++)
        {
            if (TemAcessos || i != etapaAtual)
                chefes[i].gameObject.SetActive(false);
        }

        fimDeJogo.RepetindoAposDerrota += PrepararRetentativa;
        if (!TemAcessos)
            AtivarChefeAtual();
    }

    private void Start()
    {
        if (!TemAcessos)
            return;

        for (int i = 0; i < acessos.Length; i++)
        {
            acessos[i].entrada.ConfigurarPosicao(false);
            acessos[i].saida?.ConfigurarPosicao(i < etapaAtual);
        }

        if (retomando)
        {
            Transform ponto = acessos[etapaAtual].pontoRetentativa;
            if (jogador.TryGetComponent(out Rigidbody corpo))
            {
                corpo.position = ponto.position;
                corpo.rotation = ponto.rotation;
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
            else
            {
                jogador.transform.SetPositionAndRotation(ponto.position, ponto.rotation);
            }
            Physics.SyncTransforms();
            AcenderIluminacaoAtual();
            AtivarChefeAtual();
        }
        else
        {
            TocarMusica(musicaInterludio);
            StartCoroutine(AguardarEntrada());
        }
    }

    private IEnumerator AguardarEntrada()
    {
        // A morte durante o tutorial reinicia a rodada, sem recarregar a cena.
        while (etapaAtual == 0 && tutorial != null && !tutorial.TutorialConcluido)
            yield return null;

        AcessoArena acesso = acessos[etapaAtual];
        LuzesDoCorredor corredor = acesso.corredorDeEntrada;
        while (!jogador.IsDead && (MenuPausa.EstaPausado || (corredor != null && !corredor.TodasAcesas)))
            yield return null;

        if (jogador.IsDead)
            yield break;

        entradaAtual = acesso.entrada;
        entradaAtual.EntrouNaArena += AcenderIluminacaoAtual;
        entradaAtual.FechouAposEntrada += AoFecharEntrada;
        entradaAtual.AbrirSuavemente(fecharAoPassar: true);
    }

    private void AcenderIluminacaoAtual()
    {
        GameObject iluminacao = acessos[etapaAtual].iluminacao;
        if (iluminacao != null)
            iluminacao.SetActive(true);
    }

    private void AoFecharEntrada()
    {
        LimparEntradaAtual();
        if (!jogador.IsDead)
            AtivarChefeAtual();
    }

    private void LimparEntradaAtual()
    {
        if (entradaAtual == null)
            return;

        entradaAtual.EntrouNaArena -= AcenderIluminacaoAtual;
        entradaAtual.FechouAposEntrada -= AoFecharEntrada;
        entradaAtual = null;
    }

    private void AtivarChefeAtual()
    {
        tutorial?.OcultarObjetivo();
        chefeAtual = chefes[etapaAtual];
        chefeAtual.RestoreHealth();
        chefeAtual.Died += AoDerrotarChefe;
        AudioClip musicaDoChefe = musicasDosChefes != null && etapaAtual < musicasDosChefes.Length
            ? musicasDosChefes[etapaAtual]
            : null;
        TocarMusica(musicaDoChefe != null ? musicaDoChefe : musicaInterludio);
        chefeAtual.gameObject.SetActive(true);
    }

    private void AoDerrotarChefe(EnemyHealth derrotado)
    {
        if (derrotado != chefeAtual || jogador.IsDead)
            return;

        derrotado.Died -= AoDerrotarChefe;
        chefeAtual = null;
        derrotado.gameObject.SetActive(false);

        bool ultimoChefe = etapaAtual == chefes.Length - 1;
        TocarMusica(ultimoChefe ? null : musicaInterludio);
        if (!ultimoChefe)
            etapaAtual++;

        StartCoroutine(Prosseguir(ultimoChefe));
    }

    private IEnumerator Prosseguir(bool ultimoChefe)
    {
        if (ultimoChefe || intervaloEntreChefes <= 0f)
            yield return null;
        else
            yield return new WaitForSeconds(intervaloEntreChefes);

        while (MenuPausa.EstaPausado && !jogador.IsDead)
            yield return null;

        if (jogador.IsDead)
            yield break;

        if (ultimoChefe)
            fimDeJogo.ExibirVitoria();
        else if (TemAcessos)
        {
            acessos[etapaAtual - 1].saida.AbrirSuavemente();
            yield return AguardarEntrada();
        }
        else
            AtivarChefeAtual();
    }

    private void TocarMusica(AudioClip faixa)
    {
        if (trilha == null)
        {
            if (faixa == null)
                return;

            trilha = gameObject.AddComponent<AudioSource>();
            trilha.playOnAwake = false;
            trilha.loop = true;
            trilha.spatialBlend = 0f;
            trilha.volume = volumeDaTrilha;
            gameObject.AddComponent<VolumeDaFonte>().DefinirCanal(VolumeDaFonte.Canal.Musica);
        }

        if (trilha.clip == faixa)
            return;

        trilha.Stop();
        trilha.clip = faixa;
        if (faixa != null)
            trilha.Play();
    }

    private void PrepararRetentativa()
    {
        etapaDaRetentativa = etapaAtual;
        cenaDaRetentativa = gameObject.scene.path;
    }

    private void OnDestroy()
    {
        if (fimDeJogo != null)
            fimDeJogo.RepetindoAposDerrota -= PrepararRetentativa;
        if (chefeAtual != null)
            chefeAtual.Died -= AoDerrotarChefe;
        LimparEntradaAtual();
    }
}
