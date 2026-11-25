using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public sealed class PocaDeFogo : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float raio = 3.5f;
    [SerializeField, Min(0.1f)] private float duracao = 4.5f;
    [SerializeField, Min(0f)] private float danoPorPulso = 7f;
    [SerializeField, Min(0.1f)] private float intervaloDoDano = 0.8f;
    [SerializeField, Min(0.1f)] private float alturaDoFogo = 0.4f;

    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private static readonly Color CorBaixa = new Color(0.61f, 0.12f, 0.025f);
    private static readonly Color CorAlta = new Color(1f, 0.43f, 0.06f);

    public void Iniciar(PlayerResources alvo)
    {
        transform.localScale = new Vector3(raio * 2f, transform.localScale.y, raio * 2f);
        StartCoroutine(Queimar(alvo));
    }

    private IEnumerator Queimar(PlayerResources alvo)
    {
        var visual = GetComponent<MeshRenderer>();
        var cor = new MaterialPropertyBlock();
        var passoFisico = new WaitForFixedUpdate();
        Collider colisor = alvo != null ? alvo.GetComponent<Collider>() : null;
        Vector3 centro = transform.position;
        float inicio = Time.time;
        float proximoDano = inicio + intervaloDoDano;
        float proximoDanoNosPilares = proximoDano;
        while (Time.time < inicio + duracao)
        {
            float pulso = 0.5f + Mathf.Sin((Time.time - inicio) * 9f) * 0.5f;
            cor.SetColor(CorBase, Color.Lerp(CorBaixa, CorAlta, pulso));
            visual.SetPropertyBlock(cor);

            if (Time.time >= proximoDano && alvo != null && alvo.isActiveAndEnabled && colisor != null && colisor.enabled
                && DentroDaPoca(colisor, centro))
            {
                proximoDano = Time.time + intervaloDoDano;
                alvo.TakeDamage(danoPorPulso, centro);
            }

            if (Time.time >= proximoDanoNosPilares)
            {
                proximoDanoNosPilares = Time.time + intervaloDoDano;
                PillarHealth.HitArea(centro, raio, alturaDoFogo, danoPorPulso);
            }

            yield return passoFisico;
        }

        Destroy(gameObject);
    }

    private bool DentroDaPoca(Collider alvo, Vector3 centro)
    {
        Bounds limites = alvo.bounds;
        if (limites.min.y > centro.y + alturaDoFogo || limites.max.y < centro.y)
            return false;

        Vector3 ponto = alvo.ClosestPoint(centro + Vector3.up * alturaDoFogo);
        Vector3 distancia = ponto - centro;
        distancia.y = 0f;
        return distancia.sqrMagnitude <= raio * raio;
    }
}
