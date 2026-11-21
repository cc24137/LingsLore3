using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class MoedaColetavel : MonoBehaviour
{
    [SerializeField, Min(0f)] private float velocidadeGiro = 90f;
    [SerializeField] private UnityEvent aoColetar = new UnityEvent();
    [SerializeField] private Collider apoioParaColeta;

    private bool coletada;

    // O ContadorCreditos se conecta a este evento pelo grupo da rodada.
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

    private void OnTriggerEnter(Collider other) => TentarColetar(other);

    private void OnTriggerStay(Collider other)
    {
        if (apoioParaColeta != null)
            TentarColetar(other);
    }

    private void TentarColetar(Collider other)
    {
        if (coletada)
            return;

        PlayerResources jogador = other.GetComponentInParent<PlayerResources>();
        if (jogador == null || jogador.IsDead)
            return;

        if (apoioParaColeta != null)
        {
            if (!jogador.TryGetComponent(out PlayerMotor motor) || !motor.IsGrounded)
                return;

            Bounds limites = other.bounds;
            Ray raio = new Ray(limites.center, Vector3.down);
            if (!apoioParaColeta.Raycast(raio, out _, limites.extents.y + 0.05f))
                return;
        }

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
