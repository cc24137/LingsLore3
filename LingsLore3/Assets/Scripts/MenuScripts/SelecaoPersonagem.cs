using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelecaoPersonagem : MonoBehaviour
{
    [Serializable]
    private class Slot
    {
        public Image fundo;
        public Outline borda;
        public RawImage retrato;
        public TMP_Text nome;
    }

    [SerializeField] private Slot[] slots;
    [SerializeField] private RectTransform marcador;
    [SerializeField] private TMP_Text nomeDetalhe;
    [SerializeField] private TMP_Text textoDetalhe;
    [SerializeField] private Button botaoAvancar;
    [SerializeField] private CatalogoSkinsLing catalogo;
    [SerializeField] private PreviaSkinsLing previa;

    private static readonly Color32 FundoPadrao = new Color32(43, 42, 51, 255);
    private static readonly Color32 FundoSelecionado = new Color32(54, 37, 40, 255);
    private static readonly Color32 BordaPadrao = new Color32(63, 61, 74, 255);
    private static readonly Color32 BordaSelecionada = new Color32(217, 67, 67, 255);

    private int indiceSelecionado = -1;
    private bool carregandoCena;

    private void Awake()
    {
        marcador.gameObject.SetActive(false);
        botaoAvancar.interactable = false;
        previa.Preparar();

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            slot.nome.text = catalogo.Obter(i).Nome;
            slot.retrato.enabled = false;
            AtualizarSlot(slot, false);
        }

        int indiceSalvo = catalogo.LerIndiceSalvo();
        SelecionarPersonagem(indiceSalvo);
    }

    private IEnumerator Start()
    {
        if (indiceSelecionado >= 0 && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(slots[indiceSelecionado].fundo.gameObject);
        }

        yield return null;

        for (int i = 0; i < slots.Length; i++)
        {
            previa.Mostrar(catalogo.Obter(i));
            slots[i].retrato.texture = previa.RenderizarMiniatura();
            slots[i].retrato.enabled = true;
        }

        if (indiceSelecionado >= 0)
            previa.Mostrar(catalogo.Obter(indiceSelecionado));
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

        CatalogoSkinsLing.Skin skin = catalogo.Obter(indice);
        nomeDetalhe.text = skin.Nome;
        textoDetalhe.text = skin.Descricao;
        previa.Mostrar(skin);
        botaoAvancar.interactable = true;
    }

    public void Avancar()
    {
        if (carregandoCena || indiceSelecionado < 0)
            return;

        carregandoCena = true;
        catalogo.Salvar(catalogo.Obter(indiceSelecionado));
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
