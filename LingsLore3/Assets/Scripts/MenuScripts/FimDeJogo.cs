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

    [Header("Configuracao")]
    [SerializeField, Min(0f)] private float atrasoParaExibir = 1.5f;
    [SerializeField] private string cenaMenu = "MenuInicial";

    private bool encerrando;
    private bool carregandoCena;
    private bool pausou;
    private float escalaAnterior;

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

    private void Encerrar(string textoTitulo, string textoMensagem, bool vitoria)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || encerrando)
            return;

        encerrando = true;
        StartCoroutine(ExibirPainel(textoTitulo, textoMensagem, vitoria));
    }

    private IEnumerator ExibirPainel(string textoTitulo, string textoMensagem, bool vitoria)
    {
        // Permite que a animacao de morte aconteca antes de pausar.
        yield return new WaitForSecondsRealtime(atrasoParaExibir);

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
        painel.SetActive(true);

        if (EventSystem.current != null && botaoTentarNovamente != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(botaoTentarNovamente.gameObject);
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
        // Reabre a arena diretamente, mantendo as escolhas estaticas do projeto.
        CarregarCena(SceneManager.GetActiveScene().path);
    }

    public void VoltarAoMenu()
    {
        CarregarCena(cenaMenu);
    }

    private void CarregarCena(string cena)
    {
        if (carregandoCena || !pausou)
            return;

        if (string.IsNullOrEmpty(cena) || !Application.CanStreamedLevelBeLoaded(cena))
        {
            Debug.LogError($"Adicione a cena '{cena}' ao Scene List do Build Profile.", this);
            return;
        }

        carregandoCena = true;
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



