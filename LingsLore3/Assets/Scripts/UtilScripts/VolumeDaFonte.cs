using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class VolumeDaFonte : MonoBehaviour
{
    public enum Canal { Efeitos, Musica }

    private const float FatorDeVolumeGeral = 0.8f;

    [SerializeField] private Canal canal;

    private static event Action<Canal> VolumeAlterado;
    private AudioSource fonte;
    private float volumeOriginal;

    private static string Chave(Canal canal) =>
        canal == Canal.Musica ? "audio.musica" : "audio.efeitos";

    private static string ChaveVolumeAnterior(Canal canal) => Chave(canal) + ".anterior";

    public static float ObterVolume(Canal canal) =>
        Mathf.Clamp01(PlayerPrefs.GetFloat(Chave(canal), 1f));

    public static float ObterVolumeAnterior(Canal canal)
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(ChaveVolumeAnterior(canal), 1f));
        return volume > 0f ? volume : 1f;
    }

    public static void DefinirVolume(Canal canal, float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (volume == 0f)
        {
            float anterior = ObterVolume(canal);
            if (anterior > 0f)
                PlayerPrefs.SetFloat(ChaveVolumeAnterior(canal), anterior);
        }

        PlayerPrefs.SetFloat(Chave(canal), volume);
        VolumeAlterado?.Invoke(canal);
    }

    private void Awake()
    {
        fonte = GetComponent<AudioSource>();
        volumeOriginal = fonte.volume;
    }

    private void OnEnable()
    {
        VolumeAlterado += AoAlterarVolume;
        AplicarVolume();
    }

    private void OnDisable()
    {
        VolumeAlterado -= AoAlterarVolume;
    }

    public void DefinirCanal(Canal novoCanal)
    {
        canal = novoCanal;
        if (fonte != null && isActiveAndEnabled)
            AplicarVolume();
    }

    private void AoAlterarVolume(Canal canalAlterado)
    {
        if (canalAlterado == canal)
            AplicarVolume();
    }

    private void AplicarVolume()
    {
        fonte.volume = volumeOriginal * FatorDeVolumeGeral * ObterVolume(canal);
    }
}
