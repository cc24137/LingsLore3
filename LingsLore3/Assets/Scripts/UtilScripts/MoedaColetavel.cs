using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class MoedaColetavel : MonoBehaviour
{
    [SerializeField, Min(0f)] private float velocidadeGiro = 90f;
    [SerializeField] private UnityEvent aoColetar = new UnityEvent();

    private bool coletada;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, velocidadeGiro * Time.deltaTime, Space.World);
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
        aoColetar.Invoke();
        Destroy(gameObject);
    }
}
