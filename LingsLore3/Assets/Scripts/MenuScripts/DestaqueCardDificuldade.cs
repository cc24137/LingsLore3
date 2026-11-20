using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DestaqueCardDificuldade : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler,
    IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private SelecaoDificuldade controlador;
    [SerializeField] private RectTransform visual;
    [SerializeField] private RectTransform sombra;
    [SerializeField] private Image fundo;
    [SerializeField] private Image faixa;
    [SerializeField] private Outline borda;

    private static readonly Color32 FundoPadrao = new Color32(43, 42, 51, 255);
    private static readonly Color32 FundoDestaque = new Color32(52, 50, 61, 255);
    private static readonly Color32 BordaPadrao = new Color32(18, 17, 21, 255);
    private const float Elevacao = 12f;
    private const float Velocidade = 120f;
    private bool destacado;

    internal void DefinirDestaque(bool ativo)
    {
        destacado = ativo;
        fundo.color = ativo ? FundoDestaque : FundoPadrao;
        borda.effectColor = ativo ? faixa.color : (Color)BordaPadrao;
        borda.effectDistance = ativo ? new Vector2(4f, -4f) : new Vector2(3f, -3f);
        sombra.anchoredPosition = ativo ? new Vector2(8f, -10f) : new Vector2(5f, -5f);
    }

    private void Update()
    {
        // Só o visual se move; o GridLayout mantém o botão na mesma posição.
        Vector2 posicao = visual.anchoredPosition;
        float alvo = destacado ? Elevacao : 0f;
        if (posicao.y == alvo)
            return;

        posicao.y = Mathf.MoveTowards(posicao.y, alvo, Velocidade * Time.unscaledDeltaTime);
        visual.anchoredPosition = posicao;
    }

    public void OnPointerEnter(PointerEventData eventData) => controlador.PonteiroSobre(this);
    public void OnPointerMove(PointerEventData eventData) => controlador.PonteiroSobre(this);
    public void OnPointerExit(PointerEventData eventData) => controlador.PonteiroSaiu(this);
    public void OnSelect(BaseEventData eventData) => controlador.Focar(this);
    public void OnDeselect(BaseEventData eventData) => controlador.Desfocar(this);
}
