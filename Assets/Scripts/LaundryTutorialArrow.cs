using UnityEngine;
using UnityEngine.UI;

// Saved uGUI graphic: a crisp, outlined arrow without a runtime-generated texture.
[RequireComponent(typeof(CanvasRenderer))]
public class LaundryTutorialArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Draw(mesh, 1, new Color32(23, 31, 35, 255));
        Draw(mesh, .76f, color);
    }
    private void Draw(VertexHelper mesh, float scale, Color tint)
    {
        Vector2[] points = { new Vector2(0,.5f), new Vector2(-.5f,0), new Vector2(-.19f,0), new Vector2(-.19f,-.5f), new Vector2(.19f,-.5f), new Vector2(.19f,0), new Vector2(.5f,0) };
        int start = mesh.currentVertCount; Rect r = rectTransform.rect;
        foreach (var p in points) mesh.AddVert(new Vector3(r.center.x+p.x*r.width*scale,r.center.y+p.y*r.height*scale),tint,Vector2.zero);
        mesh.AddTriangle(start,start+1,start+6);
        mesh.AddTriangle(start+2,start+3,start+4); mesh.AddTriangle(start+2,start+4,start+5);
    }
}
