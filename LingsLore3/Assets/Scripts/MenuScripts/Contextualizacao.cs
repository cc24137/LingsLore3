using UnityEngine;
using UnityEngine.SceneManagement;

public class Contextualizacao : MonoBehaviour
{
    [SerializeField] private RectTransform textoHistoria;
    [SerializeField] private float velocidade = 150f;
    [SerializeField] private float posicaoInicial = -1000f;
    [SerializeField] private float posicaoFinal = 6000f;
    [SerializeField] private string cenaJogo = "ArenaPrototype";

    private bool carregandoJogo;

    private void Start()
    {
        textoHistoria.anchoredPosition = new Vector2(0f, posicaoInicial);
    }

    private void Update()
    {
        if (carregandoJogo)
            return;

        textoHistoria.anchoredPosition +=
            Vector2.up * velocidade * Time.deltaTime;

        if (textoHistoria.anchoredPosition.y >= posicaoFinal)
        {
            Pular();
        }
    }

    public void Pular()
    {
        if (carregandoJogo)
            return;

        carregandoJogo = true;
        SceneManager.LoadScene(cenaJogo);
    }
}