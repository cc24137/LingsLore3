using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ContadorCreditos : MonoBehaviour
{
    [System.Serializable]
    private class RodadaTutorial
    {
        public GameObject grupoCreditos;
    }

    [SerializeField] private TMP_Text textoContador;
    [SerializeField, Min(1)] private int totalCreditos = 5;

    [Header("Rodadas opcionais - o total sera contado automaticamente")]
    [SerializeField] private RodadaTutorial[] rodadas = new RodadaTutorial[0];

    private int creditosColetados;
    private int rodadaAtual = -1;
    private bool coletaAutomatica;
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
    }

    private void Start()
    {
        if (rodadas.Length == 0)
            return;

        // Valida tudo antes de alterar a cena.
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

            foreach (MoedaColetavel nota in grupo.GetComponentsInChildren<MoedaColetavel>(true))
            {
                rodadaPorNota.Add(nota, i);
                nota.Coletada += RegistrarColetaAutomatica;
            }
        }

        coletaAutomatica = true;
        IniciarRodada(0);
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
            rodadas[rodadaAtual].grupoCreditos.SetActive(false);

            if (rodadaAtual + 1 < rodadas.Length)
                IniciarRodada(rodadaAtual + 1);
        }
    }

    private void IniciarRodada(int indice)
    {
        rodadaAtual = indice;
        RodadaTutorial rodada = rodadas[indice];
        totalCreditos = rodada.grupoCreditos
            .GetComponentsInChildren<MoedaColetavel>(true).Length;
        creditosColetados = 0;

        AtualizarTexto();
        rodada.grupoCreditos.SetActive(true);
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
