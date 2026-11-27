using UnityEngine;

internal readonly struct BalanceamentoDificuldade
{
    internal static BalanceamentoDificuldade Atual => SelecaoDificuldade.DificuldadeEscolhida switch
    {
        Dificuldade.Facil => new(0.75f, 0.55f, 1.35f, 1.35f, 0.8f),
        Dificuldade.Dificil => new(1.25f, 1.2f, 0.88f, 0.9f, 1.08f),
        Dificuldade.Impossivel => new(1.65f, 1.55f, 0.70f, 0.75f, 1.22f),
        _ => new(1f, 1f, 1f, 1f, 1f)
    };

    internal static bool Facil => SelecaoDificuldade.DificuldadeEscolhida == Dificuldade.Facil;
    internal static bool Impossivel => SelecaoDificuldade.DificuldadeEscolhida == Dificuldade.Impossivel;

    internal readonly float Vida;
    internal readonly float Dano;
    private readonly float intervalo;
    private readonly float aviso;
    private readonly float velocidade;

    private BalanceamentoDificuldade(float vida, float dano, float intervalo, float aviso, float velocidade)
    {
        Vida = vida;
        Dano = dano;
        this.intervalo = intervalo;
        this.aviso = aviso;
        this.velocidade = velocidade;
    }

    internal float Descanso(float segundos, float minimo = 0.75f) =>
        Mathf.Max(segundos * intervalo, minimo);

    internal float Aviso(float segundos, float minimo = 0.40f) =>
        Mathf.Max(segundos * aviso, minimo);

    internal float Velocidade(float valor) => valor * velocidade;

    internal float Duracao(float segundos) => segundos / velocidade;
}
