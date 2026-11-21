using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    public OracleArm oracleArm;
    public Color shadowColor = new Color(0.12f, 0.12f, 0.13f);
    public Color baseMetalColor = new Color(0.34f, 0.31f, 0.27f);
    public Color highlightColor = new Color(0.57f, 0.48f, 0.35f);
    public Color pipeColor = new Color(0.23f, 0.24f, 0.24f);
    [Min(0f)] public float rootThickness = 0.18f;
    [Min(0f)] public float tipThickness = 0.071f;

    private Mesh armMesh;
    private Material metalMaterial;
    private readonly List<Vector3> vertices = new List<Vector3>(512);
    private readonly List<Color> colors = new List<Color>(512);
    private readonly List<int> triangles = new List<int>(1024);

    private void Start()
    {
        metalMaterial = new Material(Shader.Find("Sprites/Default"));
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = metalMaterial;
        renderer.sortingOrder = -10;

        armMesh = new Mesh { name = "Articulated arm" };
        armMesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = armMesh;
    }

    private void LateUpdate()
    {
        if (oracleArm == null || oracleArm.joints == null || oracleArm.joints.Length < 2)
            return;

        Vector3[] joints = oracleArm.joints;
        vertices.Clear();
        colors.Clear();
        triangles.Clear();
        DrawMount(joints[0]);

        int links = joints.Length - 1;
        for (int i = 0; i < links; i++)
        {
            float thickness = Mathf.Lerp(rootThickness, tipThickness, i / (float)Mathf.Max(1, links - 1));
            DrawLink(joints[i], joints[i + 1], thickness);
        }
        for (int i = 0; i <= links; i++)
        {
            float radius = Mathf.Lerp(rootThickness * 0.78f, tipThickness * 0.65f, i / (float)links);
            Disc(joints[i], radius, shadowColor, 12);
            Disc(joints[i], radius * 0.78f, baseMetalColor, 12);
            Disc(joints[i], radius * 0.39f, pipeColor, 8);
            Disc(joints[i], radius * 0.17f, highlightColor, 6);
        }

        armMesh.Clear();
        armMesh.SetVertices(vertices);
        armMesh.SetColors(colors);
        armMesh.SetTriangles(triangles, 0);
        armMesh.RecalculateBounds();
    }

    private void DrawMount(Vector2 root)
    {
        float track = Mathf.Repeat(oracleArm.currentTrackPos, 4f);
        float edge = Mathf.Floor(track);
        Vector2 tangent = (oracleArm.GetFramePositionForGraphics(edge + 0.75f) -
            oracleArm.GetFramePositionForGraphics(edge + 0.25f)).normalized;
        Vector2 inward = new Vector2(tangent.y, -tangent.x);
        Vector2 center = root + inward * 0.09f;
        Quad(center, tangent, inward, 0.48f, 0.24f, shadowColor);
        Quad(center, tangent, inward, 0.41f, 0.17f, baseMetalColor);
        Quad(center + inward * 0.068f, tangent, inward, 0.41f, 0.016f, highlightColor);
        Disc(center + tangent * 0.16f, 0.024f, pipeColor, 8);
        Disc(center - tangent * 0.16f, 0.024f, pipeColor, 8);
    }

    private void DrawLink(Vector2 start, Vector2 end, float width)
    {
        Vector2 displacement = end - start;
        float length = displacement.magnitude;
        if (length < 0.0001f)
            return;

        Vector2 axis = displacement / length;
        Vector2 side = new Vector2(-axis.y, axis.x);
        Quad(Vector2.Lerp(start, end, 0.5f), axis, side, length, width * 0.50f, shadowColor);
        Quad(Vector2.Lerp(start, end, 0.50f), axis, side, length * 0.95f, width * 0.21f, pipeColor);

        // The housing overlaps the exposed shaft. Separate end collars give
        // the segments a clear mechanical silhouette even at desktop scale.
        Quad(Vector2.Lerp(start, end, 0.35f), axis, side, length * 0.53f, width, shadowColor);
        Quad(Vector2.Lerp(start, end, 0.35f), axis, side, length * 0.50f, width * 0.77f, baseMetalColor);
        Quad(Vector2.Lerp(start, end, 0.35f) + side * width * 0.30f,
            axis, side, length * 0.45f, width * 0.12f, highlightColor);
        Quad(Vector2.Lerp(start, end, 0.60f), axis, side, Mathf.Min(0.08f, length * 0.1f), width * 1.12f, shadowColor);
        Quad(Vector2.Lerp(start, end, 0.62f), axis, side, Mathf.Min(0.04f, length * 0.06f), width * 0.90f, baseMetalColor);
    }

    private void Quad(Vector2 center, Vector2 axis, Vector2 side, float length, float width, Color color)
    {
        int first = vertices.Count;
        Vector2 x = axis * (length * 0.5f);
        Vector2 y = side * (width * 0.5f);
        Vertex(center - x - y, color);
        Vertex(center - x + y, color);
        Vertex(center + x - y, color);
        Vertex(center + x + y, color);
        Triangle(first, first + 1, first + 2);
        Triangle(first + 2, first + 1, first + 3);
    }

    private void Disc(Vector2 center, float radius, Color color, int sides)
    {
        int first = vertices.Count;
        Vertex(center, color);
        for (int i = 0; i < sides; i++)
        {
            float angle = i * 2f * Mathf.PI / sides;
            Vertex(center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), color);
        }
        for (int i = 0; i < sides; i++)
            Triangle(first, first + 1 + i, first + 1 + (i + 1) % sides);
    }

    private void Vertex(Vector2 point, Color color)
    {
        vertices.Add(transform.InverseTransformPoint(point));
        colors.Add(color);
    }

    private void Triangle(int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    private void OnDestroy()
    {
        if (armMesh != null)
            Destroy(armMesh);
        if (metalMaterial != null)
            Destroy(metalMaterial);
    }
}
