using UnityEngine;
using UnityEngine.SceneManagement;

public enum Dificuldade
{
    Facil = 0,
    Medio = 1,
    Dificil = 2,
    Impossivel = 3
}

public class SelecaoDificuldade : MonoBehaviour
{
    public static Dificuldade DificuldadeEscolhida { get; private set; }
        = Dificuldade.Facil;

    private bool carregandoCena;

    public void SelecionarDificuldade(int indice)
    {
        if (carregandoCena)
            return;

        carregandoCena = true;
        DificuldadeEscolhida = (Dificuldade)indice;

        SceneManager.LoadScene("Contextualizacao");
    }
}