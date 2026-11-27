using UnityEngine;

public sealed class VisualSkinLing : MonoBehaviour
{
    [SerializeField] private CatalogoSkinsLing catalogo;
    [SerializeField] private Transform encaixe;

    private void Awake()
    {
        int indice = catalogo.LerIndiceSalvo();
        if (indice >= 0)
            catalogo.Obter(indice).Equipar(encaixe);
    }
}
