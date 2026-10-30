using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerResources))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(Rigidbody))]
public class TutorialRespawn : MonoBehaviour
{
    [Header("Respawn")]
    [SerializeField, Min(0f)] private float atrasoRespawn = 0.35f;
    [SerializeField, Min(0f)] private float tempoInvulnerabilidade = 2.5f;

    [Header("Protecao contra quedas")]
    [SerializeField] private bool usarUltimaPosicaoNoChao = true;
    [SerializeField] private float limiteAlturaQueda = -5f;

    private PlayerResources resources;
    private PlayerMotor motor;
    private Rigidbody body;

    private Vector3 ultimaPosicaoNoChao;
    private Quaternion ultimaRotacaoNoChao;
    private bool possuiPosicaoSegura;

    private bool respawning;
    private Coroutine respawnCoroutine;

    private void Awake()
    {
        resources = GetComponent<PlayerResources>();
        motor = GetComponent<PlayerMotor>();
        body = GetComponent<Rigidbody>();

        // Usa o ponto inicial como fallback ate o primeiro registro no chao.
        ultimaPosicaoNoChao = body.position;
        ultimaRotacaoNoChao = body.rotation;
        possuiPosicaoSegura = true;
    }

    private void OnEnable()
    {
        if (resources == null)
            resources = GetComponent<PlayerResources>();

        resources.Died += AoMorrer;
    }

    private void OnDisable()
    {
        if (resources != null)
            resources.Died -= AoMorrer;

        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
            respawnCoroutine = null;
        }

        respawning = false;
    }

    private void FixedUpdate()
    {
        if (respawning || resources.IsDead)
            return;

        // PlayerMotor atualiza IsGrounded antes deste componente.
        if (motor.IsGrounded)
        {
            ultimaPosicaoNoChao = body.position;
            ultimaRotacaoNoChao = body.rotation;
            possuiPosicaoSegura = true;
        }
    }

    private void AoMorrer()
    {
        if (respawning)
            return;

        // Regra geral: retorna exatamente a posicao e rotacao da morte.
        Vector3 posicaoRespawn = body.position;
        Quaternion rotacaoRespawn = body.rotation;

        // Excecao: uma morte muito abaixo da arena e tratada como queda.
        if (usarUltimaPosicaoNoChao &&
            posicaoRespawn.y <= limiteAlturaQueda &&
            possuiPosicaoSegura)
        {
            posicaoRespawn = ultimaPosicaoNoChao;
            rotacaoRespawn = ultimaRotacaoNoChao;
        }

        respawning = true;
        respawnCoroutine = StartCoroutine(
            ExecutarRespawn(posicaoRespawn, rotacaoRespawn)
        );
    }

    private IEnumerator ExecutarRespawn(
        Vector3 position,
        Quaternion rotation)
    {
        // Usa tempo real para o retorno nao depender de timeScale.
        if (atrasoRespawn > 0f)
            yield return new WaitForSecondsRealtime(atrasoRespawn);

        // O PlayerMotor ja cancela pulo, esquiva, ataque e entradas
        // enquanto IsDead. Aqui limpamos tambem toda a velocidade.
        body.position = position;
        body.rotation = rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        Physics.SyncTransforms();

        resources.SetRespawnInvulnerability(tempoInvulnerabilidade);
        resources.RestoreFullResources();

        respawning = false;
        respawnCoroutine = null;
    }
}
