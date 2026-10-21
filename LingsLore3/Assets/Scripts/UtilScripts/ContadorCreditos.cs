using TMPro;
using UnityEngine;

public class ContadorCreditos : MonoBehaviour
{
    [SerializeField] private TMP_Text textoContador;
    [SerializeField, Min(1)] private int totalCreditos = 5;

    private int creditosColetados;

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

    // Conecte ao evento Ao Coletar de cada nota desta rodada.
    public void RegistrarColeta()
    {
        if (!isActiveAndEnabled || creditosColetados >= totalCreditos)
            return;

        creditosColetados++;
        AtualizarTexto();
    }

    private void AtualizarTexto()
    {
        textoContador.text = $"Créditos sociais: {creditosColetados}/{totalCreditos}";
    }
}
