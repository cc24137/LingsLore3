using UnityEngine;
using UnityEngine.SceneManagement;

public class SelecaoPersonagem : MonoBehaviour
{
    public static int PersonagemEscolhido { get; private set; } = -1;

    private bool carregandoCena;

    public void SelecionarPersonagem(int indice)
    {
        if (carregandoCena)
            return;

        carregandoCena = true;
        PersonagemEscolhido = indice;

        SceneManager.LoadScene("SelecaoDificuldade");
    }
}