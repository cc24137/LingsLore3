using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelecaoPersonagem : MonoBehaviour
{
    [Serializable]
    private class Slot
    {
        public Image fundo;
        public Outline borda;
        public Image retrato;
        public TMP_Text nome;
    }

    [SerializeField] private Slot[] slots;
    [SerializeField] private RectTransform marcador;
    [SerializeField] private TMP_Text nomeDetalhe;
    [SerializeField] private TMP_Text textoDetalhe;
    [SerializeField] private Image retratoDetalhe;
    [SerializeField] private Button botaoAvancar;

    public static int PersonagemEscolhido { get; private set; } = -1;

    private static readonly Color32 FundoPadrao = new Color32(43, 42, 51, 255);
    private static readonly Color32 FundoSelecionado = new Color32(54, 37, 40, 255);
    private static readonly Color32 BordaPadrao = new Color32(63, 61, 74, 255);
    private static readonly Color32 BordaSelecionada = new Color32(217, 67, 67, 255);

    private int indiceSelecionado = -1;
    private bool carregandoCena;

    private void Awake()
    {
        marcador.gameObject.SetActive(false);
        retratoDetalhe.enabled = false;
        botaoAvancar.interactable = false;
    }

    public void SelecionarPersonagem(int indice)
    {
        if (carregandoCena || indice < 0 || indice >= slots.Length)
            return;

        if (indiceSelecionado >= 0)
            AtualizarSlot(slots[indiceSelecionado], false);

        indiceSelecionado = indice;
        Slot slot = slots[indice];
        AtualizarSlot(slot, true);
        marcador.SetParent(slot.fundo.rectTransform, false);
        marcador.SetAsLastSibling();
        marcador.gameObject.SetActive(true);

        nomeDetalhe.text = slot.nome.text;
        textoDetalhe.text = $"Espaço {indice + 1:00} de {slots.Length:00}";
        retratoDetalhe.sprite = slot.retrato.sprite;
        retratoDetalhe.enabled = retratoDetalhe.sprite != null;
        botaoAvancar.interactable = true;
    }

    public void Avancar()
    {
        if (carregandoCena || indiceSelecionado < 0)
            return;

        carregandoCena = true;
        PersonagemEscolhido = indiceSelecionado;
        SceneManager.LoadScene("SelecaoDificuldade");
    }

    private static void AtualizarSlot(Slot slot, bool selecionado)
    {
        slot.fundo.color = selecionado ? FundoSelecionado : FundoPadrao;
        slot.borda.effectColor = selecionado ? BordaSelecionada : BordaPadrao;
        float espessura = selecionado ? 4f : 3f;
        slot.borda.effectDistance = new Vector2(espessura, -espessura);
        slot.retrato.color = selecionado ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f);
        slot.retrato.rectTransform.localScale = Vector3.one * (selecionado ? 1.08f : 1f);
    }
}
