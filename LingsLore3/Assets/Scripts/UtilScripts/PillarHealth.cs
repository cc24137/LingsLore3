using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider), typeof(HitImpactVfx))]
public sealed class PillarHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 300f;
    [SerializeField, Range(0f, 1f)] private float minimumBrightness = 0.28f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly List<PillarHealth> pillars = new List<PillarHealth>();
    private static PillarHealth[] pillarSnapshot;

    // Taking damage can disable a pillar during an attack's iteration.
    internal static IReadOnlyList<PillarHealth> ActivePillars => pillarSnapshot ??= pillars.ToArray();

    private CapsuleCollider shaft;
    private Collider[] colliders;
    private PillarLaunchSurface launchSurface;
    private HitImpactVfx impact;
    private DamageFlash flash;
    private MeshRenderer[] visuals;
    private MaterialPropertyBlock tint;

    [SerializeField] private float currentHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    internal bool CanBeHit => this != null && isActiveAndEnabled && currentHealth > 0f && shaft.enabled;
    internal Collider Shaft => shaft;

    private void Awake()
    {
        currentHealth = maxHealth;
        shaft = GetComponent<CapsuleCollider>();
        colliders = GetComponentsInChildren<Collider>();
        launchSurface = GetComponentInChildren<PillarLaunchSurface>();
        impact = GetComponent<HitImpactVfx>();
        if (!TryGetComponent(out flash))
            flash = gameObject.AddComponent<DamageFlash>();
        visuals = GetComponentsInChildren<MeshRenderer>();
        tint = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        pillars.Add(this);
        pillarSnapshot = null;
    }

    private void OnDisable()
    {
        pillars.Remove(this);
        pillarSnapshot = null;
    }

    public bool TakeDamage(float damage, Vector3 hitPoint)
    {
        if (!CanBeHit || !(damage > 0f) || float.IsInfinity(damage))
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        impact.Play(hitPoint);
        flash.Clear();
        UpdateColor();

        if (currentHealth > 0f)
            flash.Play();
        else
            BreakApart();

        return true;
    }

    private void UpdateColor()
    {
        float brightness = Mathf.Lerp(minimumBrightness, 1f, currentHealth / maxHealth);
        foreach (MeshRenderer visual in visuals)
        {
            Material material = visual.sharedMaterial;
            if (material == null)
                continue;

            int colorId = material.HasColor(BaseColor) ? BaseColor : ColorProperty;
            if (!material.HasColor(colorId))
                continue;

            Color color = material.GetColor(colorId);
            visual.GetPropertyBlock(tint, 0);
            tint.SetColor(colorId, new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a));
            visual.SetPropertyBlock(tint, 0);
        }
    }

    private void BreakApart()
    {
        Bounds bounds = shaft.bounds;
        MeshRenderer shaftVisual = GetComponent<MeshRenderer>();
        if (shaftVisual != null)
        {
            shaftVisual.GetPropertyBlock(tint, 0);
            SpawnFragments(bounds, shaftVisual.sharedMaterial);
        }

        foreach (MeshRenderer visual in visuals)
            visual.enabled = false;
        foreach (Collider col in colliders)
            col.enabled = false;
        if (launchSurface != null)
            launchSurface.enabled = false;

        Destroy(gameObject);
    }

    private void SpawnFragments(Bounds bounds, Material material)
    {
        const int count = 14;
        float width = Mathf.Min(bounds.size.x, bounds.size.z);
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle;
            Vector3 outward = new Vector3(offset.x, 0f, offset.y);
            float height = Mathf.Lerp(bounds.min.y, bounds.max.y, (i + 0.5f) / count);
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "Fragmento de pilar";
            fragment.GetComponent<Collider>().enabled = false;
            fragment.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, height, bounds.center.z) + outward * (width * 0.3f),
                Random.rotationUniform);
            fragment.transform.localScale = new Vector3(0.25f, 0.38f, 0.28f) * (width * Random.Range(0.65f, 1.1f));
            MeshRenderer visual = fragment.GetComponent<MeshRenderer>();
            visual.sharedMaterial = material;
            visual.SetPropertyBlock(tint, 0);
            Rigidbody body = fragment.AddComponent<Rigidbody>();
            body.linearVelocity = outward.normalized * Random.Range(1.5f, 3.2f) + Vector3.up * Random.Range(0.2f, 1.4f);
            body.angularVelocity = Random.insideUnitSphere * 6f;
            Destroy(fragment, 0.9f);
        }
    }

    internal static void HitCollider(Collider col, float damage, Vector3 point, HashSet<PillarHealth> alreadyHit = null)
    {
        PillarHealth pillar = col.GetComponentInParent<PillarHealth>();
        if (pillar != null && (alreadyHit == null || !alreadyHit.Contains(pillar)) && pillar.TakeDamage(damage, point))
            alreadyHit?.Add(pillar);
    }

    internal static void HitArea(Vector3 center, float radius, float height, float damage,
        LayerMask obstacles = default, HashSet<PillarHealth> alreadyHit = null)
    {
        foreach (PillarHealth pillar in ActivePillars)
        {
            if (alreadyHit?.Contains(pillar) != true &&
                pillar.TryGetAreaHit(center, radius, height, obstacles, out Vector3 point) &&
                pillar.TakeDamage(damage, point))
                alreadyHit?.Add(pillar);
        }
    }

    internal bool TryGetAreaHit(Vector3 center, float radius, float height, LayerMask obstacles, out Vector3 point)
    {
        point = default;
        if (!CanBeHit)
            return false;

        Vector3 origin = center + Vector3.up * Mathf.Min(0.75f, height * 0.5f);
        point = shaft.ClosestPoint(origin);
        Vector3 delta = point - center;
        delta.y = 0f;
        if (delta.sqrMagnitude > radius * radius || point.y < center.y - 0.01f || point.y > center.y + height)
            return false;

        return !Physics.Linecast(origin, point, out RaycastHit hit, obstacles, QueryTriggerInteraction.Ignore)
            || hit.collider.GetComponentInParent<PillarHealth>() == this;
    }
}
