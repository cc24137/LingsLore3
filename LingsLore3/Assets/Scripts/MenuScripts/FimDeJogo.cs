using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Coloque no Canvas, fora do painel que sera ocultado.
public class FimDeJogo : MonoBehaviour
{
    [Header("Interface")]
    [SerializeField] private GameObject painel;
    [SerializeField] private TMP_Text titulo;
    [SerializeField] private TMP_Text mensagem;
    [SerializeField] private Button botaoTentarNovamente;
    [SerializeField] private Button botaoVoltarAoMenu;

    [Header("Configuracao")]
    [SerializeField, Min(0f)] private float atrasoParaExibir = 1.5f;
    [SerializeField] private string cenaMenu = "MenuInicial";

    private bool encerrando;
    private bool foiVitoria;
    private bool carregandoCena;
    private bool pausou;
    private float escalaAnterior;

    public event Action RepetindoAposDerrota;

    private void Awake()
    {
        if (painel == null || transform.IsChildOf(painel.transform))
        {
            Debug.LogError(
                "Atribua o painel e coloque FimDeJogo no Canvas, fora do painel.",
                this
            );
            enabled = false;
            return;
        }

        painel.SetActive(false);
    }

    [ContextMenu("Teste/Exibir derrota")]
    public void ExibirDerrota()
    {
        Encerrar("Fim de Jogo", "Ling caiu. Sua jornada ainda não acabou.", false);
    }

    [ContextMenu("Teste/Exibir vitoria")]
    public void ExibirVitoria()
    {
        Encerrar("VITÓRIA!", "Ling venceu esta batalha.", true);
    }

    internal void ExibirVitoriaFinal(bool executou)
    {
        string epilogo = executou
            ? "O garfo voltou pesado na mão de Ling. Na porta do restaurante vazio, ele ainda procurou alguém a quem culpar."
            : "Ling deixou o garfo sobre o balcão. Pela primeira vez desde a invasão, pensou no que serviria se abrisse as portas amanhã.";
        Encerrar("VITÓRIA", epilogo, true);
    }

    internal void MostrarEscolhaFinal(Action<bool> aoEscolher)
    {
        var faixa = new GameObject("Decisão de Ling", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)faixa.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(0f, 248f);
        faixa.GetComponent<Image>().color = new Color(0.055f, 0.047f, 0.043f, 0.95f);

        var fala = Instantiate(mensagem, rect);
        fala.rectTransform.anchorMin = new Vector2(0.06f, 0.43f);
        fala.rectTransform.anchorMax = new Vector2(0.94f, 0.98f);
        fala.rectTransform.offsetMin = fala.rectTransform.offsetMax = Vector2.zero;
        fala.alignment = TextAlignmentOptions.Center;
        fala.fontSize = 36f;
        fala.enableAutoSizing = true;
        fala.fontSizeMin = 18f;
        fala.fontSizeMax = 36f;
        fala.text = "Quando destruíram meu restaurante, eu queria que alguém os tivessem impedido.\nAgora, sou eu quem segura o garfo...";
        fala.gameObject.SetActive(true);

        Button poupar = CriarOpcao("Baixar o garfo e partir", 0.26f, false);
        Button executar = CriarOpcao("Matar Betanyahu", 0.74f, true);
        var navegacao = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = executar };
        poupar.navigation = navegacao;
        executar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = poupar };
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(poupar.gameObject);
        }

        Button CriarOpcao(string legenda, float centro, bool golpe)
        {
            Button botao = Instantiate(botaoVoltarAoMenu, rect);
            var area = (RectTransform)botao.transform;
            area.anchorMin = new Vector2(centro - 0.20f, 0.08f);
            area.anchorMax = new Vector2(centro + 0.20f, 0.39f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            var texto = botao.GetComponentInChildren<TMP_Text>(true);
            texto.text = legenda;
            texto.enableAutoSizing = true;
            texto.fontSizeMin = 16f;
            texto.fontSizeMax = 28f;
            botao.onClick = new Button.ButtonClickedEvent();
            botao.onClick.AddListener(() =>
            {
                if (!faixa.activeSelf)
                    return;
                faixa.SetActive(false);
                Destroy(faixa);
                EventSystem.current?.SetSelectedGameObject(null);
                aoEscolher(golpe);
            });
            botao.gameObject.SetActive(true);
            return botao;
        }
    }

    private void Encerrar(string textoTitulo, string textoMensagem, bool vitoria)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || encerrando)
            return;

        encerrando = true;
        foiVitoria = vitoria;
        StartCoroutine(ExibirPainel(textoTitulo, textoMensagem, vitoria));
    }

    private IEnumerator ExibirPainel(string textoTitulo, string textoMensagem, bool vitoria)
    {
        // Permite que a animacao de morte aconteca antes de pausar.
        yield return new WaitForSeconds(atrasoParaExibir);

        while (MenuPausa.EstaPausado)
            yield return null;

        if (titulo != null)
        {
            titulo.text = textoTitulo;
            titulo.color = vitoria ? new Color32(244, 241, 234, 255) : new Color32(207, 203, 194, 255);
        }
        AplicarEstadoVisual(vitoria);
        if (mensagem != null)
            mensagem.text = textoMensagem;

        escalaAnterior = Time.timeScale;
        pausou = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (botaoTentarNovamente != null)
            botaoTentarNovamente.gameObject.SetActive(!vitoria);
        if (vitoria && botaoVoltarAoMenu != null)
        {
            var rectBotao = (RectTransform)botaoVoltarAoMenu.transform;
            rectBotao.anchoredPosition = new Vector2(0f, rectBotao.anchoredPosition.y);
        }

        painel.SetActive(true);

        Button botaoSelecionado = vitoria ? botaoVoltarAoMenu : botaoTentarNovamente;
        if (EventSystem.current != null && botaoSelecionado != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(botaoSelecionado.gameObject);
        }
    }

    private void AplicarEstadoVisual(bool vitoria)
    {
        Transform painelCentral = painel.transform.Find("PainelCentral");
        if (painelCentral == null)
            return;

        Color coral = new Color32(217, 67, 67, 255);
        Color amber = new Color32(229, 169, 59, 255);
        Color blue = new Color32(59, 116, 184, 255);
        var outline = painelCentral.GetComponent<UnityEngine.UI.Outline>();
        if (outline != null)
            outline.effectColor = vitoria ? coral : blue;

        Transform faixa = painelCentral.Find("FaixaResultado");
        var imagemFaixa = faixa != null ? faixa.GetComponent<UnityEngine.UI.Image>() : null;
        if (imagemFaixa != null)
            imagemFaixa.color = vitoria ? coral : blue;

        Transform filete = faixa != null ? faixa.Find("FileteVitoria") : null;
        if (filete != null)
        {
            filete.gameObject.SetActive(vitoria);
            var imagemFilete = filete.GetComponent<UnityEngine.UI.Image>();
            if (imagemFilete != null)
                imagemFilete.color = amber;
        }
    }
    public void TentarNovamente()
    {
        if (!foiVitoria)
            CarregarCena(SceneManager.GetActiveScene().path, repetirAposDerrota: true);
    }

    public void VoltarAoMenu()
    {
        CarregarCena(cenaMenu);
    }

    private void CarregarCena(string cena, bool repetirAposDerrota = false)
    {
        if (carregandoCena || !pausou)
            return;

        if (string.IsNullOrEmpty(cena) || !Application.CanStreamedLevelBeLoaded(cena))
        {
            Debug.LogError($"Adicione a cena '{cena}' ao Scene List do Build Profile.", this);
            return;
        }

        carregandoCena = true;
        if (repetirAposDerrota)
            RepetindoAposDerrota?.Invoke();
        RetomarTempo();
        SceneManager.LoadScene(cena);
    }

    private void RetomarTempo()
    {
        if (!pausou)
            return;

        Time.timeScale = escalaAnterior;
        pausou = false;
    }

    private void OnDestroy()
    {
        RetomarTempo();
    }
}



