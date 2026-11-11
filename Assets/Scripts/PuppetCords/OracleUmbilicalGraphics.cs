using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(60)]
public class OracleUmbilicalGraphics : MonoBehaviour
{
    [Header("Physics source")]
    public OracleUmbilical umbilicalPhysics;

    [Header("Main umbilical palette")]
    public Color trunkBaseColor = new Color(0.18f, 0.18f, 0.18f);
    public Color trunkHighlightColor = new Color(0.48f, 0.46f, 0.42f);
    [Tooltip("Outer width of the main cable in world units.")]
    public float trunkWidth = 0.2f;
    public float trunkHeight = 0.06f;

    [Header("Fine cable palette")]
    public Color[] cordColors =
    {
        new Color(0.62f, 0.55f, 0.44f),
        new Color(0.34f, 0.42f, 0.45f),
        new Color(0.48f, 0.34f, 0.32f)
    };
    public float cordWidth = 0.035f;

    private Mesh trunkShadowMesh;
    private Mesh trunkCoreMesh;
    private Mesh fineCordMesh;
    private MeshFilter trunkShadowFilter;
    private MeshFilter trunkCoreFilter;
    private MeshFilter fineCordFilter;
    private Material cableMaterial;
    private readonly List<Vector3> vertices = new List<Vector3>(1024);
    private readonly List<Color> colors = new List<Color>(1024);
    private readonly List<int> triangles = new List<int>(2048);

    private void Start()
    {
        cableMaterial = new Material(Shader.Find("Sprites/Default"));
        trunkShadowFilter = CreateMeshObject("Umbilical shadow", -15, out trunkShadowMesh);
        trunkCoreFilter = CreateMeshObject("Umbilical core", -14, out trunkCoreMesh);
        fineCordFilter = CreateMeshObject("Fine data cords", -13, out fineCordMesh);
    }

    private MeshFilter CreateMeshObject(string objectName, int sortingOrder, out Mesh mesh)
    {
        GameObject meshObject = new GameObject(objectName);
        meshObject.transform.SetParent(transform, false);
        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = cableMaterial;
        renderer.sortingOrder = sortingOrder;
        mesh = new Mesh();
        mesh.name = objectName;
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        return filter;
    }

    private void LateUpdate()
    {
        if (umbilicalPhysics == null || umbilicalPhysics.trunkPos == null ||
            trunkShadowMesh == null || trunkCoreMesh == null || fineCordMesh == null)
            return;

        BuildMainCable(trunkShadowMesh, trunkShadowFilter, trunkWidth * 0.5f,
            trunkBaseColor, Vector2.zero, false);
        BuildMainCable(trunkCoreMesh, trunkCoreFilter, Mathf.Max(0.02f, trunkHeight * 0.7f),
            trunkHighlightColor, new Vector2(trunkWidth * 0.14f, 0.01f), true);
        BuildFineCords();
    }

    private void BuildMainCable(Mesh mesh, MeshFilter filter, float halfWidth, Color color,
        Vector2 sideOffset, bool tapered)
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        Vector2[] points = umbilicalPhysics.trunkPos;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 tangent = GetTangent(points, i);
            Vector2 side = new Vector2(-tangent.y, tangent.x);
            float endpointScale = tapered
                ? Mathf.Lerp(0.62f, 1f, Mathf.Min(i, points.Length - 1 - i) / 6f)
                : 1f;
            float width = halfWidth * endpointScale;
            Vector2 center = points[i] + side * sideOffset.x + tangent * sideOffset.y;
            vertices.Add(ToLocal(center + side * width, -0.01f));
            vertices.Add(ToLocal(center - side * width, -0.01f));
            colors.Add(color);
            colors.Add(color);

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = a + 1;
                int c = i * 2;
                int d = c + 1;
                AddTriangle(a, b, c);
                AddTriangle(c, b, d);
            }
        }

        Commit(mesh, filter);
    }

    private void BuildFineCords()
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        Vector2[][] cords = umbilicalPhysics.smallCordsPos;
        if (cords == null)
        {
            Commit(fineCordMesh, fineCordFilter);
            return;
        }

        for (int cord = 0; cord < cords.Length; cord++)
        {
            Vector2[] points = cords[cord];
            if (points == null || points.Length < 2)
                continue;
            Color cordColor = cordColors != null && cordColors.Length > 0
                ? cordColors[cord % cordColors.Length]
                : trunkHighlightColor;
            float halfWidth = cordWidth * Mathf.Lerp(0.36f, 0.56f,
                Mathf.Repeat(cord * 0.381966f, 1f));

            // Dark outline and inset color form the source's flat, bundled mesh cords.
            AddCordRibbon(points, halfWidth + 0.012f, trunkBaseColor * 0.82f, -0.012f);
            AddCordRibbon(points, halfWidth * 0.6f, cordColor, -0.006f);
        }

        Commit(fineCordMesh, fineCordFilter);
    }

    private void AddCordRibbon(Vector2[] points, float halfWidth, Color color, float depth)
    {
        int startVertex = vertices.Count;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 tangent = GetTangent(points, i);
            Vector2 side = new Vector2(-tangent.y, tangent.x);
            vertices.Add(ToLocal(points[i] + side * halfWidth, depth));
            vertices.Add(ToLocal(points[i] - side * halfWidth, depth));
            colors.Add(color);
            colors.Add(color);

            if (i > 0)
            {
                int a = startVertex + (i - 1) * 2;
                int b = a + 1;
                int c = startVertex + i * 2;
                int d = c + 1;
                AddTriangle(a, b, c);
                AddTriangle(c, b, d);
            }
        }
    }

    private Vector2 GetTangent(Vector2[] points, int index)
    {
        Vector2 tangent;
        if (index == 0)
            tangent = points[1] - points[0];
        else if (index == points.Length - 1)
            tangent = points[index] - points[index - 1];
        else
            tangent = points[index + 1] - points[index - 1];

        if (tangent.sqrMagnitude < 0.000001f)
            return Vector2.right;
        return tangent.normalized;
    }

    private void Commit(Mesh mesh, MeshFilter filter)
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
    }

    private void AddTriangle(int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private Vector3 ToLocal(Vector2 point, float depth)
    {
        return transform.InverseTransformPoint(new Vector3(point.x, point.y, depth));
    }

    private void OnDestroy()
    {
        if (trunkShadowMesh != null) Destroy(trunkShadowMesh);
        if (trunkCoreMesh != null) Destroy(trunkCoreMesh);
        if (fineCordMesh != null) Destroy(fineCordMesh);
        if (cableMaterial != null) Destroy(cableMaterial);
    }
}
