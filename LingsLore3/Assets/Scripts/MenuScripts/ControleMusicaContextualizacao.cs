using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ControleMusicaContextualizacao : MonoBehaviour
{
    [SerializeField] private AudioSource musica;
    [SerializeField] private Image icone;
    [SerializeField] private Sprite altoFalante;
    [SerializeField] private Sprite altoFalanteSilenciado;

    private InputAction atalho;

    private void Awake()
    {
        atalho = new InputAction("Alternar música", InputActionType.Button, "<Keyboard>/m");
    }

    private void OnEnable()
    {
        atalho.performed += AoPressionarAtalho;
        atalho.Enable();
        AtualizarIcone();
    }

    private void OnDisable()
    {
        atalho.performed -= AoPressionarAtalho;
        atalho.Disable();
    }

    private void OnDestroy() => atalho?.Dispose();

    private void AoPressionarAtalho(InputAction.CallbackContext _) => AlternarMusica();

    public void AlternarMusica()
    {
        musica.mute = !musica.mute;
        AtualizarIcone();
    }

    private void AtualizarIcone()
    {
        icone.sprite = musica.mute ? altoFalanteSilenciado : altoFalante;
    }
}
