using UnityEngine;

public sealed class BuildPlacementPreview : MonoBehaviour
{
    Mesh mesh;
    Material material;
    readonly LineRenderer[] lines = new LineRenderer[3];
    void Awake()
    {
        gameObject.hideFlags = HideFlags.DontSave;
        material = new Material(Shader.Find("Sprites/Default")); mesh = new Mesh { name = "2x2 construction preview" };
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var fill = gameObject.AddComponent<MeshRenderer>();
        fill.sharedMaterial = material; fill.sortingLayerName = "UI"; fill.sortingOrder = 80;
        for (int i = 0; i < lines.Length; i++)
        {
            var obj = new GameObject("Footprint outline " + i); obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
            line.startWidth = line.endWidth = .035f; line.sortingLayerName = "UI"; line.sortingOrder = 81; lines[i] = line;
        }
    }
    public void Show(BuildFootprint site, bool allowed)
    {
        gameObject.SetActive(true);
        var color = allowed ? new Color(.35f, 1, .5f, .22f) : new Color(1, .25f, .2f, .25f);
        Vector3 front = new Vector3(0, 0, -.05f);
        mesh.vertices = new[] { site.Corner(0, 0) + front, site.Corner(2, 0) + front, site.Corner(2, 2) + front, site.Corner(0, 2) + front };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.colors = new[] { color, color, color, color }; mesh.RecalculateBounds();
        color.a = .9f; foreach (var line in lines) line.startColor = line.endColor = color;
        lines[0].positionCount = 5;
        lines[0].SetPositions(new[] { site.Corner(0, 0) + front, site.Corner(2, 0) + front, site.Corner(2, 2) + front, site.Corner(0, 2) + front, site.Corner(0, 0) + front });
        lines[1].positionCount = lines[2].positionCount = 2;
        lines[1].SetPositions(new[] { site.Corner(1, 0) + front, site.Corner(1, 2) + front });
        lines[2].SetPositions(new[] { site.Corner(0, 1) + front, site.Corner(2, 1) + front });
    }
    public void Hide() => gameObject.SetActive(false);
    void OnDestroy()
    {
        if (Application.isPlaying) { Destroy(mesh); Destroy(material); }
        else { DestroyImmediate(mesh); DestroyImmediate(material); }
    }
}
