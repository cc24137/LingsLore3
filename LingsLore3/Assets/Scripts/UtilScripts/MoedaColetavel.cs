using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class MoedaColetavel : MonoBehaviour
{
    [SerializeField, Min(0f)] private float velocidadeGiro = 90f;
    [SerializeField] private UnityEvent aoColetar = new UnityEvent();

    private bool coletada;

    // O contador se conecta a este evento pelo grupo da rodada.
    public event System.Action<MoedaColetavel> Coletada;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, velocidadeGiro * Time.deltaTime, Space.World);
    }

    public void ReiniciarColeta()
    {
        coletada = false;
        GetComponent<BoxCollider>().enabled = true;
        gameObject.SetActive(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (coletada)
            return;

        PlayerResources jogador = other.GetComponentInParent<PlayerResources>();
        if (jogador == null || jogador.IsDead)
            return;

        // O Ling pode ter mais de um collider: conta a coleta uma unica vez.
        coletada = true;
        GetComponent<BoxCollider>().enabled = false;
        // Oculta antes de avisar o contador: o evento pode trocar de rodada.
        // Manter a nota permite repetir uma rodada sem recriar o cenario.
        gameObject.SetActive(false);
        Coletada?.Invoke(this);
        aoColetar.Invoke();
    }
}
