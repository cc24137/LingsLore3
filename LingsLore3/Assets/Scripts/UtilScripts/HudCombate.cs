using TMPro;
using UnityEngine;

[DefaultExecutionOrder(200)]
[RequireComponent(typeof(BarrasJogador))]
public sealed class HudCombate : MonoBehaviour
{
    [SerializeField] private SequenciaChefesArena sequencia;
    [SerializeField] private RectTransform mira;
    [SerializeField] private RectTransform filaDeCura;
    [SerializeField] private GameObject painelChefe;
    [SerializeField] private RectTransform preenchimentoChefe;
    [SerializeField] private TMP_Text nomeChefe;

    private PlayerResources jogador;
    private PlayerHashi hashi;
    private EnemyHealth chefeExibido;
    private int barraExibida;
    private int cargasExibidas = -1;

    private void Awake()
    {
        jogador = GetComponent<BarrasJogador>().Jogador;
        if (jogador != null)
            hashi = jogador.GetComponent<PlayerHashi>();
    }

    private void OnEnable()
    {
        if (hashi != null)
            hashi.UseHudReticle(true);
    }

    private void OnDisable()
    {
        if (hashi != null)
            hashi.UseHudReticle(false);
    }

    private void LateUpdate()
    {
        AtualizarMira();
        AtualizarCura();
        AtualizarChefe();
    }

    private void AtualizarMira()
    {
        bool visivel = hashi != null && hashi.ShowsAimIndicator;
        mira.gameObject.SetActive(visivel);
        if (visivel && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)mira.parent, hashi.AimCamera.pixelRect.center, null, out Vector2 centro))
            mira.localPosition = centro;
    }

    private void AtualizarCura()
    {
        if (jogador == null || jogador.HealCharges == cargasExibidas)
            return;

        cargasExibidas = jogador.HealCharges;
        for (int i = 0; i < filaDeCura.childCount; i++)
            filaDeCura.GetChild(i).gameObject.SetActive(i < cargasExibidas);
    }

    private void AtualizarChefe()
    {
        EnemyHealth chefe = sequencia != null ? sequencia.ChefeAtual : null;
        bool visivel = chefe != null && chefe.isActiveAndEnabled && !chefe.IsDead;
        painelChefe.SetActive(visivel);
        if (!visivel)
        {
            chefeExibido = null;
            return;
        }

        if (chefe != chefeExibido || barraExibida != chefe.CurrentBar)
        {
            chefeExibido = chefe;
            barraExibida = chefe.CurrentBar;
            nomeChefe.text = sequencia.NomeDoChefeAtual;
        }

        float proporcaoDaVida = Mathf.Clamp01(chefe.CurrentHealth / chefe.MaxHealth);
        preenchimentoChefe.anchorMax = new Vector2(proporcaoDaVida, 1f);
    }
}
