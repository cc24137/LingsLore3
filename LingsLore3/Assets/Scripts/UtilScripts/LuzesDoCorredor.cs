using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class LuzesDoCorredor : MonoBehaviour
{
    private static readonly int CorBase = Shader.PropertyToID("_BaseColor");
    private static readonly int CorEmissao = Shader.PropertyToID("_EmissionColor");

    [System.Serializable]
    private struct Lanterna
    {
        public MeshRenderer vidro;
        public Light luz;
    }

    [SerializeField] private Lanterna[] lanternas;
    [SerializeField] private Material vidroAceso;
    [SerializeField, Min(0.01f)] private float intervalo = 0.32f;

    private bool acendimentoIniciado;

    public bool TodasAcesas { get; private set; }

    private void OnTriggerStay(Collider outro)
    {
        Rigidbody corpo = outro.attachedRigidbody;
        if (acendimentoIniciado || outro.isTrigger || corpo == null ||
            !corpo.TryGetComponent(out PlayerResources jogador) || jogador.IsDead ||
            Vector3.Dot(corpo.linearVelocity, transform.forward) <= 0.1f)
            return;

        acendimentoIniciado = true;
        StartCoroutine(AcenderEmSequencia());
    }

    private IEnumerator AcenderEmSequencia()
    {
        var coresDoVidro = new MaterialPropertyBlock();
        Color corAcesa = vidroAceso.GetColor(CorBase);
        Color emissaoAcesa = vidroAceso.GetColor(CorEmissao);
        foreach (Lanterna lanterna in lanternas)
        {
            Color corApagada = lanterna.vidro.sharedMaterial.GetColor(CorBase);
            float intensidadeAcesa = lanterna.luz.intensity;
            lanterna.vidro.sharedMaterial = vidroAceso;
            lanterna.luz.enabled = true;

            for (float tempo = 0f; tempo < intervalo; tempo += Time.deltaTime)
            {
                float brilho = Mathf.SmoothStep(0f, 1f, tempo / intervalo);
                lanterna.luz.intensity = intensidadeAcesa * brilho;
                coresDoVidro.SetColor(CorBase, Color.Lerp(corApagada, corAcesa, brilho));
                coresDoVidro.SetColor(CorEmissao, emissaoAcesa * brilho);
                lanterna.vidro.SetPropertyBlock(coresDoVidro);
                yield return null;
            }

            lanterna.luz.intensity = intensidadeAcesa;
            lanterna.vidro.SetPropertyBlock(null);
        }

        TodasAcesas = true;
    }
}
