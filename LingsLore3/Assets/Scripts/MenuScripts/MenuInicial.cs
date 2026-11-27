using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuInicial : MonoBehaviour
{
    [SerializeField] private GameObject painelMenu;
    [SerializeField] private GameObject painelCreditos;
    [SerializeField] private Button botaoCreditos;
    [SerializeField] private Button botaoVoltar;

    public void Jogar()
    {
        SceneManager.LoadScene("SelecaoPersonagem");
    }

    public void AbrirCreditos()
    {
        painelMenu.SetActive(false);
        painelCreditos.SetActive(true);
        botaoVoltar.Select();
    }

    public void FecharCreditos()
    {
        painelCreditos.SetActive(false);
        painelMenu.SetActive(true);
        botaoCreditos.Select();
    }

    public void Sair()
    {
        Application.Quit();
    }
}
