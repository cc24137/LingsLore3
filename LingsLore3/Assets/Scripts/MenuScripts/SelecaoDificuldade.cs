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
        = Dificuldade.Medio;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InicializarDificuldade() => DificuldadeEscolhida = Dificuldade.Medio;

    private bool carregandoCena;
    private DestaqueCardDificuldade cardComFoco;
    private DestaqueCardDificuldade cardSobPonteiro;
    private DestaqueCardDificuldade cardDestacado;

    internal void PonteiroSobre(DestaqueCardDificuldade card)
    {
        cardSobPonteiro = card;
        Destacar(card);
    }

    internal void PonteiroSaiu(DestaqueCardDificuldade card)
    {
        if (cardSobPonteiro != card)
            return;

        cardSobPonteiro = null;
        Destacar(cardComFoco);
    }

    internal void Focar(DestaqueCardDificuldade card)
    {
        cardComFoco = card;
        Destacar(card);
    }

    internal void Desfocar(DestaqueCardDificuldade card)
    {
        if (cardComFoco != card)
            return;

        cardComFoco = null;
        Destacar(cardSobPonteiro);
    }

    private void Destacar(DestaqueCardDificuldade card)
    {
        if (cardDestacado == card)
            return;

        if (cardDestacado != null)
            cardDestacado.DefinirDestaque(false);

        cardDestacado = card;
        if (cardDestacado != null)
            cardDestacado.DefinirDestaque(true);
    }

    public void SelecionarDificuldade(int indice)
    {
        if (carregandoCena || indice < (int)Dificuldade.Facil || indice > (int)Dificuldade.Impossivel)
            return;

        carregandoCena = true;
        DificuldadeEscolhida = (Dificuldade)indice;

        SceneManager.LoadScene("Contextualizacao");
    }
}
