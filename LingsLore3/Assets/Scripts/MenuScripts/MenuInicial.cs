using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuInicial : MonoBehaviour
{
    public void Jogar()
    {
        SceneManager.LoadScene("ArenaPrototype");
    }

    public void Sair()
    {
        Application.Quit();
    }
}