using UnityEngine;

// Executa o salto. O PlayerMotor verifica se ele pode comecar.
[RequireComponent(typeof(Rigidbody))]
public class PlayerJump : MonoBehaviour
{
    [SerializeField, Min(0f)] private float jumpHeight = 2f;
    [SerializeField, Range(0.1f, 1f)]
    private float jumpCutMultiplier = 1f;

    private Rigidbody body;
    private bool canCutJump;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void ExecuteJump()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Sqrt(
            2f * Mathf.Abs(Physics.gravity.y) * jumpHeight
        );

        body.linearVelocity = velocity;
        canCutJump = true;
    }

    // Chamado pelo motor uma vez por passo de fisica.
    public void Tick(bool jumpHeld)
    {
        if (!canCutJump)
            return;

        Vector3 velocity = body.linearVelocity;

        if (velocity.y <= 0f)
        {
            canCutJump = false;
        }
        else if (!jumpHeld)
        {
            velocity.y *= jumpCutMultiplier;
            body.linearVelocity = velocity;
            canCutJump = false;
        }
    }

    public void Cancel()
    {
        canCutJump = false;
    }

    private void OnDisable()
    {
        Cancel();
    }
}
