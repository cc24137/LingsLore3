using UnityEngine;

[RequireComponent(typeof(PlayerResources))]
public class LingHealVisual : MonoBehaviour
{
    [SerializeField] private Transform leanPivot;
    [SerializeField] private Transform itemPivot;
    [SerializeField] private GameObject itemModel;

    [Header("Pose de cura")]
    [SerializeField] private Vector3 loweredPosition = new Vector3(0.29f, -0.29f, 0.45f);
    [SerializeField] private Vector3 raisedPosition = new Vector3(0.06f, -0.18f, 0.61f);
    [SerializeField] private Vector3 loweredRotation = new Vector3(0f, -15f, -8f);
    [SerializeField] private Vector3 raisedRotation = new Vector3(-63f, 0f, -10f);
    [SerializeField, Min(0f)] private float backwardTilt = 18f;
    [SerializeField, Min(0.01f)] private float itemSize = 0.56f;

    private PlayerResources resources;
    private Quaternion neutralLean;
    private bool showing;

    private void Awake()
    {
        if (leanPivot == null || itemPivot == null || itemModel == null)
        {
            Debug.LogError("Atribua os pivôs de cura e o modelo da batata no prefab do Ling.", this);
            enabled = false;
            return;
        }

        resources = GetComponent<PlayerResources>();
        neutralLean = leanPivot.localRotation;
        itemPivot.gameObject.SetActive(false);

        Transform model = Instantiate(itemModel).transform;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("O modelo da batata precisa de um Renderer.", this);
            Destroy(model.gameObject);
            enabled = false;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        // O FBX veio com uma escala pequena e a malha deslocada do pivot.
        float largestAxis = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = largestAxis > 0f ? itemSize / largestAxis : 1f;
        Vector3 centerOffset = bounds.center - model.position;
        model.SetParent(itemPivot, false);
        model.localScale *= scale;
        model.localPosition = -centerOffset * scale;
    }

    private void LateUpdate()
    {
        if (!resources.IsHealing)
        {
            HidePose();
            return;
        }

        float progress = resources.HealProgress;
        float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1f, progress));
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.40f, progress)) * (1f - lower);
        float tilt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.57f, progress)) * (1f - lower);

        leanPivot.localRotation = neutralLean * Quaternion.Euler(-backwardTilt * tilt, 0f, 0f);
        itemPivot.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, lift);
        itemPivot.localRotation = Quaternion.Slerp(
            Quaternion.Euler(loweredRotation), Quaternion.Euler(raisedRotation), tilt);

        if (!showing)
        {
            showing = true;
            itemPivot.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        HidePose();
    }

    private void HidePose()
    {
        if (!showing)
            return;

        showing = false;
        itemPivot.gameObject.SetActive(false);
        leanPivot.localRotation = neutralLean;
    }
}
