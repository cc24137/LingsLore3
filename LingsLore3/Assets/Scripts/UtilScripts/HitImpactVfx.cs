using UnityEngine;

[DisallowMultipleComponent]
public class HitImpactVfx : MonoBehaviour
{
    [SerializeField] private GameObject effectPrefab;
    [SerializeField, Min(0.01f)] private float scale = 0.6f;
    [SerializeField, Min(0.1f)] private float lifetime = 2f;

    public void Play(Vector3 point)
    {
        if (!isActiveAndEnabled || effectPrefab == null)
            return;

        GameObject instance = Instantiate(effectPrefab, point, effectPrefab.transform.rotation);
        instance.transform.localScale *= scale;

        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
        {
            var main = particles.main;
            if (main.scalingMode == ParticleSystemScalingMode.Local)
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        Destroy(instance, lifetime);
    }
}
