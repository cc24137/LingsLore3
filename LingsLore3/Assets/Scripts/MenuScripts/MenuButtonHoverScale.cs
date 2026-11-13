using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MenuButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.04f;
    [SerializeField, Min(0.1f)] private float transitionSpeed = 12f;

    private Vector3 baseScale;
    private bool pointerOver;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        var targetScale = pointerOver ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
    }
}
