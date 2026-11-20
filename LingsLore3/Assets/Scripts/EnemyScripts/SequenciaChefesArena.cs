using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SequenciaChefesArena : MonoBehaviour
{
    [SerializeField] private EnemyHealth[] chefes;
    [SerializeField] private PlayerResources jogador;
    [SerializeField] private FimDeJogo fimDeJogo;
    [SerializeField, Min(0f)] private float intervaloEntreChefes = 0.75f;

    private static int etapaDaRetentativa;
    private int etapaAtual;
    private EnemyHealth chefeAtual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRetentativa()
    {
        etapaDaRetentativa = 0;
    }

    private void Awake()
    {
        if (jogador == null || fimDeJogo == null || chefes == null || chefes.Length == 0 ||
            Array.Exists(chefes, chefe => chefe == null))
        {
            Debug.LogError("Configure os chefes da arena, Ling e a tela de fim de jogo.", this);
            enabled = false;
            return;
        }

        // Só uma recarga solicitada pela tela de derrota retoma uma etapa.
        etapaAtual = Mathf.Clamp(etapaDaRetentativa, 0, chefes.Length - 1);
        LimparRetentativa();

        for (int i = 0; i < chefes.Length; i++)
        {
            if (i != etapaAtual)
                chefes[i].gameObject.SetActive(false);
        }

        fimDeJogo.RepetindoAposDerrota += PrepararRetentativa;
        AtivarChefeAtual();
    }

    private void AtivarChefeAtual()
    {
        chefeAtual = chefes[etapaAtual];
        chefeAtual.RestoreHealth();
        chefeAtual.Died += AoDerrotarChefe;
        chefeAtual.gameObject.SetActive(true);
    }

    private void AoDerrotarChefe(EnemyHealth derrotado)
    {
        if (derrotado != chefeAtual || jogador.IsDead)
            return;

        derrotado.Died -= AoDerrotarChefe;
        chefeAtual = null;
        derrotado.gameObject.SetActive(false);

        bool ultimoChefe = etapaAtual == chefes.Length - 1;
        if (!ultimoChefe)
            etapaAtual++;

        StartCoroutine(Prosseguir(ultimoChefe));
    }

    private IEnumerator Prosseguir(bool ultimoChefe)
    {
        if (ultimoChefe || intervaloEntreChefes <= 0f)
            yield return null;
        else
            yield return new WaitForSeconds(intervaloEntreChefes);

        while (MenuPausa.EstaPausado && !jogador.IsDead)
            yield return null;

        if (jogador.IsDead)
            yield break;

        if (ultimoChefe)
            fimDeJogo.ExibirVitoria();
        else
            AtivarChefeAtual();
    }

    private void PrepararRetentativa()
    {
        etapaDaRetentativa = etapaAtual;
    }

    private void OnDestroy()
    {
        if (fimDeJogo != null)
            fimDeJogo.RepetindoAposDerrota -= PrepararRetentativa;
        if (chefeAtual != null)
            chefeAtual.Died -= AoDerrotarChefe;
    }
}
