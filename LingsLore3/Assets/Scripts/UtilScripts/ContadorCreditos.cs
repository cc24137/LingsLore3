using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ContadorCreditos : MonoBehaviour
{
    [System.Serializable]
    private class RodadaTutorial
    {
        public GameObject grupoCreditos;
        [Min(0f)] public float tempoLimite;
        public Transform pontoInicial;
    }

    [SerializeField] private TMP_Text textoContador;
    [SerializeField, Min(1)] private int totalCreditos = 5;

    [Header("Rodadas opcionais - o total sera contado automaticamente")]
    [SerializeField] private RodadaTutorial[] rodadas = new RodadaTutorial[0];

    [Header("Cronometro e ponto de inicio opcional")]
    [SerializeField] private TMP_Text textoTempo;
    [SerializeField] private PlayerResources jogador;

    private int creditosColetados;
    private int rodadaAtual = -1;
    private bool coletaAutomatica;
    private bool tempoAtivo;
    private bool tutorialConcluido;
    private float tempoRestante;
    private MoedaColetavel[][] notasPorRodada;
    private readonly Dictionary<MoedaColetavel, int> rodadaPorNota =
        new Dictionary<MoedaColetavel, int>();
    private readonly HashSet<MoedaColetavel> notasContadas =
        new HashSet<MoedaColetavel>();

    private void Awake()
    {
        if (textoContador == null)
        {
            Debug.LogError("Atribua o texto do contador no Inspector.", this);
            enabled = false;
            return;
        }

        AtualizarTexto();
        if (textoTempo != null)
            textoTempo.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (rodadas.Length == 0)
            return;

        // Valida tudo antes de alterar a cena.
        notasPorRodada = new MoedaColetavel[rodadas.Length][];
        for (int i = 0; i < rodadas.Length; i++)
        {
            GameObject grupo = rodadas[i].grupoCreditos;
            if (grupo == null || transform.IsChildOf(grupo.transform) ||
                grupo.GetComponentsInChildren<MoedaColetavel>(true).Length == 0)
            {
                Debug.LogError($"Rodada {i + 1}: atribua um grupo com notas, separado do Canvas.", this);
                enabled = false;
                return;
            }

            notasPorRodada[i] = grupo.GetComponentsInChildren<MoedaColetavel>(true);

            if (rodadas[i].pontoInicial != null &&
                rodadas[i].pontoInicial.IsChildOf(grupo.transform))
            {
                Debug.LogError($"Rodada {i + 1}: coloque o ponto inicial fora do grupo de creditos.", this);
                enabled = false;
                return;
            }

            for (int j = 0; j < i; j++)
            {
                GameObject anterior = rodadas[j].grupoCreditos;
                if (grupo.transform.IsChildOf(anterior.transform) ||
                    anterior.transform.IsChildOf(grupo.transform))
                {
                    Debug.LogError("Use grupos separados para cada rodada, sem colocar um dentro do outro.", this);
                    enabled = false;
                    return;
                }
            }
        }

        for (int i = 0; i < rodadas.Length; i++)
        {
            GameObject grupo = rodadas[i].grupoCreditos;
            grupo.SetActive(false);

            foreach (MoedaColetavel nota in notasPorRodada[i])
            {
                rodadaPorNota.Add(nota, i);
                nota.Coletada += RegistrarColetaAutomatica;
            }
        }

        if (jogador == null)
            jogador = FindFirstObjectByType<PlayerResources>();

        coletaAutomatica = true;
        IniciarRodada(0);
    }

    private void Update()
    {
        if (!tempoAtivo || tutorialConcluido ||
            (jogador != null && jogador.IsDead))
            return;

        // O cronometro tambem pausa quando a tela de fim de jogo pausa o jogo.
        tempoRestante = Mathf.Max(0f, tempoRestante - Time.deltaTime);
        AtualizarTempo();

        if (tempoRestante <= 0f)
            IniciarRodada(rodadaAtual);
    }

    // Compatibilidade com o contador antigo, sem rodadas configuradas.
    // No modo automatico, ignora ligacoes antigas para nao contar duas vezes.
    public void RegistrarColeta()
    {
        if (coletaAutomatica)
            return;

        ContabilizarCredito();
    }

    private void RegistrarColetaAutomatica(MoedaColetavel nota)
    {
        if (!isActiveAndEnabled || !coletaAutomatica ||
            !rodadaPorNota.TryGetValue(nota, out int indice) ||
            indice != rodadaAtual || !notasContadas.Add(nota))
            return;

        ContabilizarCredito();
    }

    private void ContabilizarCredito()
    {
        if (!isActiveAndEnabled || creditosColetados >= totalCreditos)
            return;

        creditosColetados++;
        AtualizarTexto();

        if (creditosColetados == totalCreditos && rodadaAtual >= 0)
        {
            tempoAtivo = false;
            rodadas[rodadaAtual].grupoCreditos.SetActive(false);

            if (rodadaAtual + 1 < rodadas.Length)
                IniciarRodada(rodadaAtual + 1);
            else
            {
                tutorialConcluido = true;
                if (textoTempo != null)
                    textoTempo.gameObject.SetActive(false);
            }
        }
    }

    private void IniciarRodada(int indice)
    {
        rodadaAtual = indice;
        RodadaTutorial rodada = rodadas[indice];
        tempoAtivo = false;
        rodada.grupoCreditos.SetActive(false);
        notasContadas.Clear();
        totalCreditos = notasPorRodada[indice].Length;
        creditosColetados = 0;

        foreach (MoedaColetavel nota in notasPorRodada[indice])
            nota.ReiniciarColeta();

        ReposicionarJogador(rodada.pontoInicial);
        AtualizarTexto();
        rodada.grupoCreditos.SetActive(true);

        tempoRestante = Mathf.Max(0f, rodada.tempoLimite);
        tempoAtivo = tempoRestante > 0f;
        if (textoTempo != null)
        {
            textoTempo.gameObject.SetActive(tempoAtivo);
            AtualizarTempo();
        }
    }

    private void ReposicionarJogador(Transform ponto)
    {
        if (ponto == null || jogador == null)
            return;

        Rigidbody corpo = jogador.GetComponent<Rigidbody>();
        if (corpo != null)
        {
            corpo.position = ponto.position;
            corpo.rotation = ponto.rotation;
            corpo.linearVelocity = Vector3.zero;
            corpo.angularVelocity = Vector3.zero;
        }
        else
            jogador.transform.SetPositionAndRotation(ponto.position, ponto.rotation);
    }

    private void AtualizarTempo()
    {
        if (textoTempo != null)
            textoTempo.text = $"Tempo: {Mathf.CeilToInt(tempoRestante)}s";
    }

    private void AtualizarTexto()
    {
        textoContador.text = $"Créditos sociais: {creditosColetados}/{totalCreditos}";
    }

    private void OnDestroy()
    {
        foreach (MoedaColetavel nota in rodadaPorNota.Keys)
        {
            if (nota != null)
                nota.Coletada -= RegistrarColetaAutomatica;
        }
    }
}
