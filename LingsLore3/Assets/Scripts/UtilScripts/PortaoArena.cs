using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class PortaoArena : MonoBehaviour
{
    [SerializeField] private Rigidbody grade;
    [SerializeField, Min(0.1f)] private float alturaDaAbertura = 5.05f;
    [SerializeField, Min(0.1f)] private float tempoDeAbertura = 1.8f;
    [SerializeField, Min(0.1f)] private float tempoDeFechamento = 0.95f;
    [SerializeField, Min(0f)] private float folgaDoVao = 0.85f;
    [SerializeField] private GameObject encontro;

    private Vector3 posicaoAberta;
    private Vector3 posicaoFechada;
    private PlayerMotor jogador;
    private Collider corpoDoJogador;
    private float progresso;
    private bool abrindo;
    private bool fecharAoAbrir;

    public event System.Action EntrouNaArena;
    public event System.Action FechouAposEntrada;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        if (grade == null)
        {
            Debug.LogError("Associe o Rigidbody da grade ao portão da arena.", this);
            enabled = false;
            return;
        }

        posicaoAberta = grade.position;
        posicaoFechada = posicaoAberta - transform.up * alturaDaAbertura;
    }

    public void ConfigurarPosicao(bool aberta, bool fecharAoPassar = false)
    {
        if (grade == null)
            return;

        jogador = null;
        corpoDoJogador = null;
        abrindo = false;
        progresso = aberta ? 0f : 1f;
        grade.position = aberta ? posicaoAberta : posicaoFechada;
        enabled = aberta && fecharAoPassar;
    }

    public void AbrirSuavemente(bool fecharAoPassar = false)
    {
        if (grade == null)
            return;

        jogador = null;
        corpoDoJogador = null;
        fecharAoAbrir = fecharAoPassar;
        abrindo = progresso > 0f;
        enabled = abrindo || fecharAoPassar;
    }

    private void OnTriggerStay(Collider outro)
    {
        if (!isActiveAndEnabled || (abrindo && !fecharAoAbrir) || jogador != null || outro.isTrigger)
            return;

        Rigidbody corpo = outro.attachedRigidbody;
        if (corpo != null && corpo.TryGetComponent(out PlayerMotor motor) &&
            !motor.IsDead && PassouDoVao(outro))
        {
            jogador = motor;
            corpoDoJogador = outro;
            EntrouNaArena?.Invoke();
        }
    }

    private void FixedUpdate()
    {
        if (abrindo)
        {
            MoverGrade(0f, tempoDeAbertura);
            if (progresso == 0f)
            {
                abrindo = false;
                enabled = fecharAoAbrir;
            }
            return;
        }

        if (progresso == 1f)
        {
            enabled = false;
            if (encontro != null)
                encontro.SetActive(true);
            FechouAposEntrada?.Invoke();
            return;
        }

        // Se Ling voltar para o vão enquanto a grade desce, ela sobe de novo.
        if (jogador != null && (jogador.IsDead || !PassouDoVao(corpoDoJogador)))
        {
            jogador = null;
            corpoDoJogador = null;
        }

        bool fechando = jogador != null;
        float destino = fechando ? 1f : 0f;
        if (progresso == destino)
            return;

        MoverGrade(destino, fechando ? tempoDeFechamento : tempoDeFechamento * 0.5f);
    }

    private void MoverGrade(float destino, float duracao)
    {
        progresso = Mathf.MoveTowards(progresso, destino, Time.fixedDeltaTime / duracao);
        float deslocamento = Mathf.SmoothStep(0f, 1f, progresso);
        grade.MovePosition(Vector3.Lerp(posicaoAberta, posicaoFechada, deslocamento));
    }

    private bool PassouDoVao(Collider corpo)
    {
        if (corpo == null || !corpo.enabled || !corpo.gameObject.activeInHierarchy)
            return false;

        Vector3 frente = transform.forward;
        Bounds limites = corpo.bounds;
        float raio = Vector3.Dot(limites.extents,
            new Vector3(Mathf.Abs(frente.x), Mathf.Abs(frente.y), Mathf.Abs(frente.z)));
        float distancia = Vector3.Dot(limites.center - posicaoFechada, frente);
        return distancia - raio > folgaDoVao;
    }
}
