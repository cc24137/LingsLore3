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
        Encerrar("FIM DE JOGO", "Ling caiu. Sua jornada ainda não acabou.");
    }

    [ContextMenu("Teste/Exibir vitoria")]
    public void ExibirVitoria()
    {
        Encerrar("VITÓRIA!", "Ling venceu esta batalha.");
    }

    private void Encerrar(string textoTitulo, string textoMensagem)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || encerrando)
            return;

        encerrando = true;
        StartCoroutine(ExibirPainel(textoTitulo, textoMensagem));
    }

    private IEnumerator ExibirPainel(string textoTitulo, string textoMensagem)
    {
        // Permite que a animacao de morte aconteca antes de pausar.
        yield return new WaitForSecondsRealtime(atrasoParaExibir);

        if (titulo != null)
            titulo.text = textoTitulo;
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
