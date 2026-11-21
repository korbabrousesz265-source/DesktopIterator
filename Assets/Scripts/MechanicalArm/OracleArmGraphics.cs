using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    public OracleArm oracleArm;
    public Color shadowColor = new Color(0.105f, 0.11f, 0.13f);
    public Color baseMetalColor = new Color(0.29f, 0.28f, 0.27f);
    public Color highlightColor = new Color(0.39f, 0.35f, 0.30f);
    public Color pinColor = new Color(0.18f, 0.19f, 0.20f);
    [Min(0f)] public float rootThickness = 0.164f;
    [Min(0f)] public float tipThickness = 0.069f;

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
        DrawMount(joints[0], joints[1]);

        int links = joints.Length - 1;
        for (int i = 0; i < links; i++)
        {
            float thickness = Mathf.Lerp(rootThickness, tipThickness, i / (float)Mathf.Max(1, links - 1));
            DrawLink(joints[i], joints[i + 1], thickness, i < links - 1);
        }
        for (int i = 0; i < links; i++)
        {
            float radius = Mathf.Lerp(rootThickness * 0.61f, tipThickness * 0.52f, i / (float)links);
            Vector2 axis = ((Vector2)(joints[i + 1] - joints[i])).normalized;
            DrawBearing(joints[i], radius, axis);
        }
        DrawBodySocket(joints[links]);

        armMesh.Clear();
        armMesh.SetVertices(vertices);
        armMesh.SetColors(colors);
        armMesh.SetTriangles(triangles, 0);
        armMesh.RecalculateBounds();
    }

    private void DrawMount(Vector2 root, Vector2 firstJoint)
    {
        Vector2 tangent = oracleArm.GetCarriageTangent();
        Vector2 inward = new Vector2(tangent.y, -tangent.x);
        Vector2 yoke = root + inward * 0.145f;
        Vector2 neck = Vector2.MoveTowards(root, firstJoint, 0.35f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 shoe = root + tangent * (side * 0.185f) + inward * 0.042f;
            Vector2 bracket = yoke + tangent * (side * 0.135f);
            Bar(shoe, bracket, 0.083f, pinColor);
            Bar(bracket, neck, 0.043f, shadowColor);
            Quad(shoe, tangent, inward, 0.195f, 0.15f, shadowColor);
            Quad(shoe + inward * 0.01f, tangent, inward, 0.15f, 0.105f, baseMetalColor);
            Quad(shoe + inward * 0.046f, tangent, inward, 0.095f, 0.021f, highlightColor);
            Disc(bracket, 0.027f, pinColor, 8);
        }
        Quad(yoke, tangent, inward, 0.38f, 0.102f, shadowColor);
        Quad(yoke + inward * 0.014f, tangent, inward, 0.32f, 0.061f, baseMetalColor);
    }

    private void DrawLink(Vector2 start, Vector2 end, float width, bool hasFaceplate)
    {
        Vector2 displacement = end - start;
        float length = displacement.magnitude;
        if (length < 0.0001f)
            return;

        Vector2 axis = displacement / length;
        Vector2 side = new Vector2(-axis.y, axis.x);
        Bar(start, end, width * 0.47f, pinColor);
        if (!hasFaceplate)
        {
            Bar(Vector2.Lerp(start, end, 0.11f), Vector2.Lerp(start, end, 0.89f),
                width * 0.71f, baseMetalColor);
            return;
        }

        Faceplate(start, end, side, width, shadowColor);
        Faceplate(Vector2.Lerp(start, end, 0.018f), Vector2.Lerp(start, end, 0.982f),
            side, width * 0.74f, baseMetalColor);
        Quad(Vector2.Lerp(start, end, 0.26f) + side * width * 0.19f,
            axis, side, length * 0.23f, width * 0.13f, highlightColor);
        Quad(Vector2.Lerp(start, end, 0.82f), axis, side,
            Mathf.Min(0.085f, length * 0.11f), width * 0.57f, pinColor);
    }

    private void Faceplate(Vector2 start, Vector2 end, Vector2 side, float width, Color color)
    {
        int first = vertices.Count;
        AddRow(Vector2.Lerp(start, end, 0.052f), side, width * 0.22f, color);
        AddRow(Vector2.Lerp(start, end, 0.16f), side, width * 0.50f, color);
        AddRow(Vector2.Lerp(start, end, 0.62f), side, width * 0.29f, color);
        AddRow(Vector2.Lerp(start, end, 0.84f), side, width * 0.41f, color);
        AddRow(Vector2.Lerp(start, end, 0.947f), side, width * 0.19f, color);
        for (int i = first; i < first + 8; i += 2)
        {
            Triangle(i, i + 1, i + 2);
            Triangle(i + 2, i + 1, i + 3);
        }
    }

    private void AddRow(Vector2 center, Vector2 side, float halfWidth, Color color)
    {
        Vertex(center - side * halfWidth, color);
        Vertex(center + side * halfWidth, color);
    }

    private void DrawBearing(Vector2 center, float radius, Vector2 axis)
    {
        Disc(center, radius, shadowColor, 10);
        Disc(center, radius * 0.68f, pinColor, 8);
        Quad(center, axis, new Vector2(-axis.y, axis.x), radius * 0.82f, radius * 0.22f, highlightColor);
    }

    private void DrawBodySocket(Vector2 tip)
    {
        // Set the cuff just behind the robe edge and keep the pin outside it.
        Quad(tip + Vector2.left * 0.027f, Vector2.right, Vector2.up, 0.168f, 0.118f, shadowColor);
        Quad(tip + Vector2.left * 0.034f, Vector2.right, Vector2.up, 0.126f, 0.079f, baseMetalColor);
        DrawBearing(tip, 0.045f, Vector2.up);
    }

    private void Bar(Vector2 start, Vector2 end, float width, Color color)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < 0.0001f)
            return;
        Vector2 axis = delta / length;
        Quad((start + end) * 0.5f, axis, new Vector2(-axis.y, axis.x), length, width, color);
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
