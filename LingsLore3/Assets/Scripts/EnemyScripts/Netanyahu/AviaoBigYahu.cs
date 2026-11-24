using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class AviaoBigYahu : EnemyHealth
{
    [SerializeField] private GameObject modelo;
    [SerializeField, Min(0.1f)] private float envergadura = 6f;
    [SerializeField] private float rotacaoDoModelo;

    public BoxCollider Hitbox { get; private set; }
    public bool EstaMontado { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Hitbox = GetComponent<BoxCollider>();
        Hitbox.enabled = false;
        if (modelo == null)
            return;

        var visual = Instantiate(modelo, transform, false);
        var filtro = visual.GetComponentInChildren<MeshFilter>();
        if (filtro == null || !filtro.TryGetComponent(out MeshRenderer malha))
            return;

        // O FBX tem rotações de apresentação na hierarquia. A malha usa Y para cima.
        Quaternion rotacaoDaMalha = Quaternion.Inverse(transform.rotation) * malha.transform.rotation;
        visual.transform.localRotation = Quaternion.Euler(0f, rotacaoDoModelo, 0f) *
            Quaternion.Inverse(rotacaoDaMalha) * visual.transform.localRotation;

        Vector3 tamanho = malha.bounds.size;
        float largura = Mathf.Max(tamanho.x, tamanho.z);
        if (largura < 0.00001f)
            return;

        visual.transform.localScale *= envergadura / largura;
        Bounds limites = malha.bounds;
        visual.transform.localPosition -= transform.InverseTransformPoint(limites.center);
        Hitbox.size = new Vector3(limites.size.x, Mathf.Max(limites.size.y, 1.6f), limites.size.z);
        EstaMontado = true;
    }

    // O avião informa acertos sem consumir a barra de vida do piloto.
    public override bool TryTakeDamage(float amount, bool ataqueBasico = false)
    {
        if (!Hitbox.enabled || !(amount > 0f) || float.IsInfinity(amount))
            return false;

        DamageFlash.PlayOn(this);
        return true;
    }
}
