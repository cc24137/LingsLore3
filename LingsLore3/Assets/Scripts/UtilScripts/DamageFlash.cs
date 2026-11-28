using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Color flashColor = new Color(1f, 0.04f, 0.04f);
    [SerializeField, Range(0f, 1f)] private float intensity = 0.55f;
    [SerializeField, Min(0.01f)] private float duration = 0.12f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private struct MaterialSlot
    {
        public Renderer Renderer;
        public int Index;
        public int ColorId;
        public Color OriginalColor;
        public MaterialPropertyBlock OriginalBlock;
    }

    private readonly List<MaterialSlot> slots = new List<MaterialSlot>();
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<Material> materials = new List<Material>();
    private MaterialPropertyBlock flashBlock;
    private float startedAt;

    public static void PlayOn(Component target)
    {
        if (!target.gameObject.activeInHierarchy)
            return;

        if (!target.TryGetComponent<DamageFlash>(out var flash))
            flash = target.gameObject.AddComponent<DamageFlash>();

        flash.Play();
    }

    public void Play()
    {
        if (!isActiveAndEnabled || intensity <= 0f)
            return;

        if (slots.Count == 0)
            CaptureMaterials();

        startedAt = Time.time;
        ApplyFlash(intensity);
    }

    private void CaptureMaterials()
    {
        if (flashBlock == null)
            flashBlock = new MaterialPropertyBlock();

        Transform root = visualRoot != null ? visualRoot : transform;
        root.GetComponentsInChildren(false, renderers);

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                renderer.TryGetComponent<PreenchimentoAviso>(out _))
                continue;

            renderer.GetSharedMaterials(materials);
            for (int i = 0; i < materials.Count; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                int colorId = material.HasColor(BaseColor) ? BaseColor : ColorProperty;
                if (!material.HasColor(colorId))
                    continue;

                renderer.GetPropertyBlock(flashBlock, i);
                MaterialPropertyBlock originalBlock = null;
                if (flashBlock.isEmpty)
                {
                    renderer.GetPropertyBlock(flashBlock);
                }
                else
                {
                    originalBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(originalBlock, i);
                }

                slots.Add(new MaterialSlot
                {
                    Renderer = renderer,
                    Index = i,
                    ColorId = colorId,
                    OriginalColor = flashBlock.HasColor(colorId)
                        ? flashBlock.GetColor(colorId)
                        : material.GetColor(colorId),
                    OriginalBlock = originalBlock
                });
            }
        }

        renderers.Clear();
        materials.Clear();
    }

    private void Update()
    {
        if (slots.Count == 0)
            return;

        float elapsed = Time.time - startedAt;
        if (elapsed >= duration)
        {
            Clear();
            return;
        }

        ApplyFlash(intensity * (1f - elapsed / duration));
    }

    private void ApplyFlash(float strength)
    {
        foreach (MaterialSlot slot in slots)
        {
            if (slot.Renderer == null)
                continue;

            slot.Renderer.GetPropertyBlock(flashBlock, slot.Index);
            if (flashBlock.isEmpty)
                slot.Renderer.GetPropertyBlock(flashBlock);

            Color tint = Color.Lerp(slot.OriginalColor, flashColor, strength);
            tint.a = slot.OriginalColor.a;
            flashBlock.SetColor(slot.ColorId, tint);
            slot.Renderer.SetPropertyBlock(flashBlock, slot.Index);
        }
    }

    public void Clear()
    {
        foreach (MaterialSlot slot in slots)
        {
            if (slot.Renderer != null)
                slot.Renderer.SetPropertyBlock(slot.OriginalBlock, slot.Index);
        }

        slots.Clear();
    }

    private void OnDisable()
    {
        Clear();
    }
}
