using UnityEngine;
using UnityEngine.UI;

public sealed class CirculoUI : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        const int lados = 32;
        mesh.Clear();

        Rect area = GetPixelAdjustedRect();
        Vector2 centro = area.center;
        float raio = Mathf.Min(area.width, area.height) * 0.5f;
        mesh.AddVert(centro, color, Vector2.zero);

        for (int i = 0; i < lados; i++)
        {
            float angulo = -2f * Mathf.PI * i / lados;
            Vector2 ponto = centro + raio * new Vector2(Mathf.Cos(angulo), Mathf.Sin(angulo));
            mesh.AddVert(ponto, color, Vector2.zero);
            mesh.AddTriangle(0, i + 1, (i + 1) % lados + 1);
        }
    }
}
