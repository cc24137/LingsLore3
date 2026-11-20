using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class Contextualizacao : MonoBehaviour
{
    [SerializeField] private RectTransform textoHistoria;
    [SerializeField] private float velocidade = 150f;
    [SerializeField] private float posicaoInicial = -1000f;
    [SerializeField] private float posicaoFinal = 6000f;
    [SerializeField] private string cenaJogo = "ArenaPrototype";

    private bool carregandoJogo;

    private void Awake()
    {
        LiberarCursor();

        if (textoHistoria.TryGetComponent<Graphic>(out var texto))
            texto.raycastTarget = false;
    }

    private void Start()
    {
        textoHistoria.anchoredPosition = new Vector2(0f, posicaoInicial);
    }

    private void Update()
    {
        if (carregandoJogo)
            return;

        LiberarCursor();
        textoHistoria.anchoredPosition +=
            Vector2.up * velocidade * Time.deltaTime;

        if (textoHistoria.anchoredPosition.y >= posicaoFinal)
        {
            Pular();
        }
    }

    private void OnApplicationFocus(bool temFoco)
    {
        if (temFoco && !carregandoJogo)
            LiberarCursor();
    }

    private static void LiberarCursor()
    {
        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
        if (!Cursor.visible)
            Cursor.visible = true;
    }

    public void Pular()
    {
        if (carregandoJogo)
            return;

        carregandoJogo = true;
        SceneManager.LoadScene(cenaJogo);
    }
}
