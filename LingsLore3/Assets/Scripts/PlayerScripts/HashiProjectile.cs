using System;
using UnityEngine;

// Raiz do prefab: +Z aponta para a ponta. Sem Rigidbody ou Collider.
public class HashiProjectile : MonoBehaviour
{
    public event Action<EnemyHealth> HitEnemy;

    [SerializeField, Min(0.1f)] private float speed = 30f;
    [SerializeField, Min(0.001f)] private float hitRadius = 0.06f;
    [SerializeField, Min(0.1f)] private float maxDistance = 80f;
    [SerializeField, Min(0.1f)] private float lifetime = 5f;

    private Transform owner;
    private HitImpactVfx hitImpactVfx;
    private Vector3 direction;
    private LayerMask hitMask;
    private float damage;
    private float traveled;
    private float age;
    private bool launched;
    private bool spent;

    public void Launch(Transform shooter, Vector3 forward, float hitDamage, LayerMask mask)
    {
        owner = shooter;
        hitImpactVfx = shooter != null ? shooter.GetComponent<HitImpactVfx>() : null;
        direction = forward.normalized;
        damage = hitDamage;
        hitMask = mask;
        traveled = 0f;
        age = 0f;
        spent = false;
        launched = true;
        transform.rotation = Quaternion.LookRotation(direction);
        CheckOverlap();
    }

    private bool CanHit(Collider candidate)
    {
        if (candidate == null)
            return false;

        Transform candidateTransform = candidate.transform;
        bool isProjectile = candidateTransform == transform ||
            candidateTransform.IsChildOf(transform);
        bool isShooter = owner != null &&
            (candidateTransform == owner || candidateTransform.IsChildOf(owner));

        return !isProjectile && !isShooter;
    }

    private bool CheckOverlap()
    {
        Collider closest = null;
        Vector3 closestPoint = default;
        float distance = float.PositiveInfinity;
        foreach (Collider candidate in Physics.OverlapSphere(
            transform.position, hitRadius, hitMask, QueryTriggerInteraction.Collide))
        {
            if (!CanHit(candidate)) continue;
            Vector3 point = candidate.ClosestPoint(transform.position);
            float pointDistance = (point - transform.position).sqrMagnitude;
            if (pointDistance < distance)
            {
                distance = pointDistance;
                closest = candidate;
                closestPoint = point;
            }
        }
        if (closest == null) return false;
        Impact(closest, closestPoint);
        return true;
    }

    private void FixedUpdate()
    {
        if (!launched || spent) return;
        age += Time.fixedDeltaTime;
        if (age >= lifetime || traveled >= maxDistance)
        {
            spent = true;
            Destroy(gameObject);
            return;
        }
        if (CheckOverlap()) return;

        float step = Mathf.Min(speed * Time.fixedDeltaTime, maxDistance - traveled);
        RaycastHit nearest = default;
        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        // Consulta todo o trecho percorrido para nao atravessar paredes finas.
        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position,
            hitRadius, direction, step, hitMask, QueryTriggerInteraction.Collide))
        {
            if (!CanHit(hit.collider) || hit.distance >= nearestDistance) continue;
            nearest = hit;
            nearestDistance = hit.distance;
            found = true;
        }
        if (found)
        {
            transform.position += direction * nearest.distance;
            Vector3 point = nearest.distance > 0f
                ? nearest.point
                : nearest.collider.ClosestPoint(transform.position);
            Impact(nearest.collider, point);
            return;
        }
        transform.position += direction * step;
        traveled += step;
    }

    private void Impact(Collider target, Vector3 point)
    {
        if (spent) return;
        spent = true;
        EnemyHealth enemy = target.GetComponentInParent<EnemyHealth>();
        if (enemy != null && enemy.TryTakeDamage(damage))
        {
            EfeitosSonoros.Tocar(Som.ImpactoDoHashi, point);
            if (hitImpactVfx != null)
                hitImpactVfx.Play(point - direction * 0.05f);

            HitEnemy?.Invoke(enemy);
        }
        Destroy(gameObject);
    }
}
