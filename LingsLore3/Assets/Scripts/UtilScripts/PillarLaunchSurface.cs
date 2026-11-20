using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class PillarLaunchSurface : MonoBehaviour
{
    [Header("Entrada")]
    [SerializeField] private CapsuleCollider shaftCollider;
    [SerializeField, Range(0f, 1f)] private float minimumApproachDot = 0.7f;

    [Header("Subida e lancamento")]
    [SerializeField, Min(0.1f)] private float climbSpeed = 10f;
    [Tooltip("Altura do apice a partir da base. 1.2 equivale a 120% da altura do pilar.")]
    [SerializeField, Min(MinimumApexFraction)] private float launchApexFraction = 1.2f;
    [SerializeField, Min(0.1f)] private float landingSpeed = 3.5f;

    // Proporcoes da malha RampaPilar no prefab.
    private const float RampWidthRatio = 0.65f;
    private const float RampRiseFraction = 0.85f / 14f;
    private const float ReleaseFraction = 0.25f;
    private const float MinimumApexFraction = 1.1f;

    private Vector3 baseCenter;
    private float shaftRadius;
    private float shaftHeight;

    public float ReleaseY => baseCenter.y + shaftHeight * ReleaseFraction;
    internal Vector3 TopCenter => baseCenter + Vector3.up * shaftHeight;
    internal float LandingSpeed => landingSpeed;

    private void Awake()
    {
        if (shaftCollider == null)
            shaftCollider = GetComponentInParent<CapsuleCollider>();
        if (shaftCollider == null)
        {
            enabled = false;
            return;
        }

        Bounds shaft = shaftCollider.bounds;
        baseCenter = new Vector3(shaft.center.x, shaft.min.y, shaft.center.z);
        shaftRadius = Mathf.Max(shaft.extents.x, shaft.extents.z);
        shaftHeight = shaft.size.y;
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) => CheckRoll(other);
    private void OnTriggerStay(Collider other) => CheckRoll(other);

    private void CheckRoll(Collider other)
    {
        if (isActiveAndEnabled && other.attachedRigidbody != null &&
            other.attachedRigidbody.TryGetComponent(out PlayerDodge dodge))
            dodge.TryBeginPillarClimb(this);
    }

    internal bool TryGetEntry(Vector3 position, Vector3 direction, float playerRadius,
        out Vector3 outward, out float climbRadius)
    {
        Vector3 radial = Vector3.ProjectOnPlane(position - baseCenter, Vector3.up);
        outward = radial.normalized;
        climbRadius = shaftRadius + playerRadius + 0.04f;

        float height = position.y - baseCenter.y;
        float entryRadius = climbRadius + shaftRadius * RampWidthRatio;
        return height >= -0.15f && height <= 0.45f &&
            radial.sqrMagnitude <= entryRadius * entryRadius &&
            Vector3.Dot(direction, -outward) >= minimumApproachDot &&
            radial.sqrMagnitude > 0.001f;
    }

    internal Vector3 GetClimbPosition(Vector3 start, Vector3 outward, float radius, float time)
    {
        float startRadius = Vector3.ProjectOnPlane(start - baseCenter, Vector3.up).magnitude;
        float rise = Mathf.Max(0.05f, baseCenter.y + shaftHeight * RampRiseFraction - start.y);
        float curveTime = Mathf.PI * 0.5f *
            Mathf.Max(rise, Mathf.Abs(startRadius - radius)) / climbSpeed;
        float angle = Mathf.Clamp01(time / curveTime) * Mathf.PI * 0.5f;
        float height = start.y + rise * (1f - Mathf.Cos(angle)) +
            Mathf.Max(0f, time - curveTime) * climbSpeed;
        Vector3 position = baseCenter + outward * Mathf.Lerp(startRadius, radius, Mathf.Sin(angle));
        position.y = Mathf.Min(height, ReleaseY);
        return position;
    }

    internal Vector3 GetLaunchVelocity(float feetY, float deltaTime)
    {
        float apexY = baseCenter.y + shaftHeight * Mathf.Max(MinimumApexFraction, launchApexFraction);
        float remainingRise = Mathf.Max(0f, apexY - feetY);
        float gravity = Mathf.Abs(Physics.gravity.y);
        // Compensa a gravidade aplicada no primeiro passo do lancamento.
        float liftSpeed = Mathf.Sqrt(2f * gravity * remainingRise) + gravity * deltaTime;
        return Vector3.up * liftSpeed;
    }
}
