using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class MenuPausa : MonoBehaviour
{
    [SerializeField] private GameObject painel;
    [SerializeField] private Button botaoPausar;
    [SerializeField] private Button botaoContinuar;
    [SerializeField] private Button botaoAvancar;
    [SerializeField] private GameObject painelControles;
    [SerializeField] private Button botaoControles;
    [SerializeField] private Button botaoVoltarControles;
    [SerializeField] private Slider volumeMusica;
    [SerializeField] private Slider volumeEfeitos;
    [SerializeField] private Image iconeMusica;
    [SerializeField] private Image iconeEfeitos;
    [SerializeField] private Sprite altoFalante;
    [SerializeField] private Sprite altoFalanteSilenciado;
    [SerializeField] private string cenaMenu = "MenuInicial";
    [SerializeField] private string cenaAvanco;
    [SerializeField] private ContadorCreditos tutorialDoCircuito;

    private bool pausado;
    private bool carregandoAvanco;
    private bool volumesAlterados;
    private float escalaAnterior;
    private CursorLockMode bloqueioAnterior;
    private bool cursorVisivelAntes;
    private GameObject selecaoAnterior;
    private int quadroDaTransicao = -1;

    private bool TemAvanco => botaoAvancar != null && (tutorialDoCircuito != null
        ? tutorialDoCircuito.PodePularTutorial
        : !string.IsNullOrEmpty(cenaAvanco));
    private bool PodePularNaPausa => tutorialDoCircuito != null && pausado && painel.activeSelf;

    public static MenuPausa Ativo { get; private set; }
    public static bool EstaPausado => Ativo != null && Ativo.pausado;
    public static bool BloqueiaEntradaDeJogo =>
        Time.timeScale == 0f || (Ativo != null && Ativo.quadroDaTransicao == Time.frameCount);

    private void Awake()
    {
        if (painel == null || botaoPausar == null || botaoContinuar == null ||
            painelControles == null || botaoControles == null || botaoVoltarControles == null ||
            volumeMusica == null || volumeEfeitos == null ||
            iconeMusica == null || iconeEfeitos == null || altoFalante == null || altoFalanteSilenciado == null)
        {
            Debug.LogError("Configure o painel e os botões do menu de pausa.", this);
            enabled = false;
            return;
        }

        painel.SetActive(false);
        painelControles.SetActive(false);
        volumeMusica.SetValueWithoutNotify(VolumeDaFonte.ObterVolume(VolumeDaFonte.Canal.Musica));
        volumeEfeitos.SetValueWithoutNotify(VolumeDaFonte.ObterVolume(VolumeDaFonte.Canal.Efeitos));
        AtualizarIconeVolume(iconeMusica, volumeMusica.value);
        AtualizarIconeVolume(iconeEfeitos, volumeEfeitos.value);
        AtualizarBotoes();
    }

    private void OnEnable()
    {
        Ativo = this;
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null)
            return;

        if (teclado.escapeKey.wasPressedThisFrame)
        {
            if (pausado && painelControles.activeSelf)
                VoltarDosControles();
            else if (pausado)
                Continuar();
            else
                Pausar();
        }
        else if (TemAvanco && teclado.enterKey.wasPressedThisFrame)
        {
            Avancar();
        }
    }

    private void LateUpdate() => AtualizarBotoes();

    private void AtualizarBotoes()
    {
        bool mostrar = !pausado && !carregandoAvanco && Time.timeScale > 0f;
        botaoPausar.gameObject.SetActive(mostrar);
        if (botaoAvancar != null)
            botaoAvancar.gameObject.SetActive(TemAvanco && (mostrar || PodePularNaPausa));
    }

    public bool PonteiroSobreBotao()
    {
        if (!Cursor.visible || Mouse.current == null)
            return false;

        Vector2 posicao = Mouse.current.position.ReadValue();
        return ContemPonteiro(botaoPausar, posicao) || ContemPonteiro(botaoAvancar, posicao);
    }

    private static bool ContemPonteiro(Button botao, Vector2 posicao)
    {
        return botao != null && botao.isActiveAndEnabled &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)botao.transform, posicao);
    }

    public void Pausar()
    {
        if (!isActiveAndEnabled || pausado || carregandoAvanco || BloqueiaEntradaDeJogo)
            return;

        escalaAnterior = Time.timeScale;
        bloqueioAnterior = Cursor.lockState;
        cursorVisivelAntes = Cursor.visible;
        var eventos = EventSystem.current;
        selecaoAnterior = eventos != null ? eventos.currentSelectedGameObject : null;

        pausado = true;
        quadroDaTransicao = Time.frameCount;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AtualizarBotoes();
        painel.SetActive(true);
        if (eventos != null)
            eventos.SetSelectedGameObject(botaoContinuar.gameObject);
    }

    public void Continuar()
    {
        if (pausado)
            Fechar(restaurarCursor: true);
    }

    public void AbrirControles()
    {
        if (!pausado || painelControles.activeSelf)
            return;

        painel.SetActive(false);
        painelControles.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(botaoVoltarControles.gameObject);
    }

    public void VoltarDosControles()
    {
        if (!pausado || !painelControles.activeSelf)
            return;

        painelControles.SetActive(false);
        painel.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(botaoControles.gameObject);
    }

    public void AjustarMusica(float volume)
    {
        volumesAlterados = true;
        VolumeDaFonte.DefinirVolume(VolumeDaFonte.Canal.Musica, volume);
        AtualizarIconeVolume(iconeMusica, volume);
    }

    public void AjustarEfeitos(float volume)
    {
        volumesAlterados = true;
        VolumeDaFonte.DefinirVolume(VolumeDaFonte.Canal.Efeitos, volume);
        AtualizarIconeVolume(iconeEfeitos, volume);
    }

    public void AlternarMusica()
    {
        volumeMusica.value = volumeMusica.value > 0f
            ? 0f
            : VolumeDaFonte.ObterVolumeAnterior(VolumeDaFonte.Canal.Musica);
    }

    public void AlternarEfeitos()
    {
        volumeEfeitos.value = volumeEfeitos.value > 0f
            ? 0f
            : VolumeDaFonte.ObterVolumeAnterior(VolumeDaFonte.Canal.Efeitos);
    }

    private void AtualizarIconeVolume(Image icone, float volume)
    {
        icone.sprite = volume > 0f ? altoFalante : altoFalanteSilenciado;
    }

    public void Avancar()
    {
        if (!isActiveAndEnabled || !TemAvanco || carregandoAvanco ||
            (BloqueiaEntradaDeJogo && !PodePularNaPausa))
            return;

        if (tutorialDoCircuito != null)
        {
            tutorialDoCircuito.PularTutorial();
            if (pausado)
                Fechar(restaurarCursor: true);
            AtualizarBotoes();
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(cenaAvanco))
        {
            Debug.LogError($"A cena '{cenaAvanco}' precisa estar no Scene List do Build Profile.", this);
            return;
        }

        carregandoAvanco = true;
        quadroDaTransicao = Time.frameCount;
        AtualizarBotoes();
        SceneManager.LoadScene(cenaAvanco);
    }

    public void VoltarAoMenu()
    {
        if (!pausado)
            return;

        if (!Application.CanStreamedLevelBeLoaded(cenaMenu))
        {
            Debug.LogError($"A cena '{cenaMenu}' precisa estar no Scene List do Build Profile.", this);
            return;
        }

        Fechar(restaurarCursor: false);
        SceneManager.LoadScene(cenaMenu);
    }

    private void Fechar(bool restaurarCursor)
    {
        pausado = false;
        quadroDaTransicao = Time.frameCount;
        Time.timeScale = escalaAnterior;

        var eventos = EventSystem.current;
        if (eventos != null)
        {
            GameObject selecao = restaurarCursor && selecaoAnterior != null && selecaoAnterior.activeInHierarchy
                ? selecaoAnterior : null;
            eventos.SetSelectedGameObject(selecao);
        }

        selecaoAnterior = null;
        painel.SetActive(false);
        painelControles.SetActive(false);
        if (volumesAlterados)
        {
            PlayerPrefs.Save();
            volumesAlterados = false;
        }

        if (restaurarCursor)
        {
            Cursor.lockState = bloqueioAnterior;
            Cursor.visible = cursorVisivelAntes;
        }
    }

    private void OnDisable()
    {
        if (pausado)
            Fechar(restaurarCursor: true);

        if (Ativo == this)
            Ativo = null;
    }
}
