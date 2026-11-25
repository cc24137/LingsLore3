using System;
using UnityEngine;

internal enum Som
{
    Bonk,
    MorteDoChefe,
    GongoDaArena,
    Explosao,
    Garfo,
    DanoDoLing,
    MorteDoLing,
    Tiro,
    ImpactoDoHashi = 9, // Preserva os IDs do catálogo após remover o som de quebra.
    ArremessoDoHashi,
    TungTung,
    QuebraDoPilar,
    Laser,
    Cura,
    SocoDoYahu,
    Fogo,
    SomSagrado,
    AparecimentoDoLing
}

public sealed class EfeitosSonoros : ScriptableObject
{
    [Serializable]
    private struct Efeito
    {
        public Som som;
        public AudioClip clipe;
        [Range(0f, 1f)] public float volume;
    }

    [SerializeField] private Efeito[] efeitos;

    private static EfeitosSonoros catalogo;
    private static readonly System.Random sorteioDePitch = new System.Random();

    internal static void Tocar(Som som, Vector3? posicao = null, float variacaoDePitch = 0f)
    {
        if (catalogo == null)
            catalogo = Resources.Load<EfeitosSonoros>("CatalogoEfeitosSonoros");
        if (catalogo == null || catalogo.efeitos == null)
            return;

        Efeito efeito = Array.Find(catalogo.efeitos, item => item.som == som);
        if (efeito.clipe == null || efeito.volume <= 0f)
            return;

        // O emissor fica na cena para o som continuar se o objeto que o disparou sumir.
        var emissor = new GameObject(efeito.clipe.name);
        var fonte = emissor.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.clip = efeito.clipe;
        fonte.volume = efeito.volume;
        fonte.spatialBlend = posicao.HasValue ? 0.65f : 0f;
        if (posicao.HasValue)
        {
            emissor.transform.position = posicao.Value;
            fonte.rolloffMode = AudioRolloffMode.Linear;
            fonte.minDistance = 3f;
            fonte.maxDistance = 45f;
            fonte.dopplerLevel = 0f;
        }

        if (variacaoDePitch > 0f)
            fonte.pitch = 1f + ((float)sorteioDePitch.NextDouble() * 2f - 1f) * variacaoDePitch;

        emissor.AddComponent<VolumeDaFonte>();
        fonte.Play();
        Destroy(emissor, efeito.clipe.length / fonte.pitch);
    }
}
