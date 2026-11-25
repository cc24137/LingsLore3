using UnityEngine;

/// <summary>
/// Anexe este script ao GameObject raiz do "chapeu_helice" (ou diretamente ao filho "Helice").
/// Ele gira automaticamente a hélice em torno do eixo Y local.
/// </summary>
public class GirarHelice : MonoBehaviour
{
    [Tooltip("Velocidade de rotação em graus por segundo.")]
    public float velocidade = 540f;

    [Tooltip("Arraste o filho 'Helice' aqui (se vazio, busca automaticamente).")]
    public Transform heliceTransform;

    void Start()
    {
        if (heliceTransform == null)
        {
            heliceTransform = transform.Find("Helice");
            if (heliceTransform == null)
                heliceTransform = transform;
        }
    }

    void Update()
    {
        heliceTransform.Rotate(0f, velocidade * Time.deltaTime, 0f, Space.Self);
    }
}
