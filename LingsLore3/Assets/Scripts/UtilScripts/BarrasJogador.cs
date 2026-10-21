using UnityEngine;
using UnityEngine.UI;

public class BarrasJogador : MonoBehaviour
{
    [SerializeField] private PlayerResources jogador;
    [SerializeField] private Slider barraVida;
    [SerializeField] private Slider barraStamina;

    private void Start()
    {
        if (jogador == null || barraVida == null || barraStamina == null)
        {
            Debug.LogError("Atribua o PlayerResources e as duas barras no Inspector.", this);
            enabled = false;
            return;
        }

        ConfigurarBarra(barraVida);
        ConfigurarBarra(barraStamina);
        AtualizarBarras();
    }

    private void ConfigurarBarra(Slider barra)
    {
        barra.minValue = 0f;
        barra.maxValue = 1f;
        barra.wholeNumbers = false;
        barra.direction = Slider.Direction.LeftToRight;
        barra.interactable = false;
        barra.transition = Selectable.Transition.None;
        barra.navigation = new Navigation { mode = Navigation.Mode.None };

        // As barras apenas exibem os recursos, sem receber cliques.
        foreach (Graphic imagem in barra.GetComponentsInChildren<Graphic>(true))
            imagem.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (jogador != null)
            AtualizarBarras();
    }

    private void AtualizarBarras()
    {
        barraVida.SetValueWithoutNotify(jogador.HealthNormalized);
        barraStamina.SetValueWithoutNotify(jogador.StaminaNormalized);
    }
}
