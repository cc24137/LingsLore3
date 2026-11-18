using UnityEngine;
using UnityEngine.UI;

public sealed class MarcadorTriangular : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();

        Rect area = rectTransform.rect;
        mesh.AddVert(new Vector3(area.xMin, area.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(area.xMax, area.yMax), color, Vector2.zero);
        mesh.AddVert(new Vector3(area.xMax, area.yMin), color, Vector2.zero);
        mesh.AddTriangle(0, 1, 2);
    }
}
