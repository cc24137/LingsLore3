using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PlayerKnockback : MonoBehaviour
{
    [Header("Reacao ao dano")]
    [SerializeField, Min(0f)] private float horizontalSpeed = 6f;
    [SerializeField, Min(0f)] private float upwardSpeed = 3f;
    [SerializeField, Min(0.01f)] private float controlLockDuration = 0.18f;

    private Rigidbody body;
    private Vector3 pendingVelocity;
    private bool hasPendingVelocity;
    private float remainingControlLock;

    public bool IsActive => hasPendingVelocity || remainingControlLock > 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void QueueHit(Vector3? sourcePosition)
    {
        if (!isActiveAndEnabled || (horizontalSpeed <= 0f && upwardSpeed <= 0f))
            return;

        Vector3 backward = -(body.rotation * Vector3.forward);
        Vector3 away = sourcePosition.HasValue ? body.position - sourcePosition.Value : backward;
        Vector3 direction = Vector3.ProjectOnPlane(away, Vector3.up);
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.ProjectOnPlane(backward, Vector3.up);

        pendingVelocity = direction.normalized * horizontalSpeed + Vector3.up * upwardSpeed;
        hasPendingVelocity = true;
    }

    // Chamado pelo motor enquanto a reacao tem prioridade sobre o movimento.
    public void Tick(float deltaTime)
    {
        if (hasPendingVelocity)
        {
            body.linearVelocity = pendingVelocity;
            hasPendingVelocity = false;
            remainingControlLock = controlLockDuration;
        }

        remainingControlLock = Mathf.Max(0f, remainingControlLock - deltaTime);
    }

    public void Cancel()
    {
        hasPendingVelocity = false;
        remainingControlLock = 0f;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
