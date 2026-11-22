using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    public OracleArm oracleArm;
    public Color shadowColor = new Color(0.105f, 0.104f, 0.106f);
    public Color baseMetalColor = new Color(0.32f, 0.31f, 0.29f);
    public Color highlightColor = new Color(0.40f, 0.39f, 0.37f);
    public Color pinColor = new Color(0.18f, 0.18f, 0.19f);
    [Min(0f)] public float rootThickness = 0.184f;
    [Min(0f)] public float tipThickness = 0.069f;

    private Mesh armMesh;
    private Material metalMaterial;
    private readonly List<Vector3> vertices = new List<Vector3>(1024);
    private readonly List<Color> colors = new List<Color>(1024);
    private readonly List<int> triangles = new List<int>(4096);
    private static readonly float[] ShellBands = { -1f, -0.77f, -0.30f, 0.30f, 0.77f, 1f };
    private static readonly Vector2 LightDirection = new Vector2(-0.6f, 0.8f);

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
        if (oracleArm == null || oracleArm.joints == null || oracleArm.joints.Length < 2 ||
            oracleArm.Elbows == null || oracleArm.Elbows.Length + 1 != oracleArm.joints.Length)
            return;

        Vector3[] joints = oracleArm.joints;
        Vector2[] elbows = oracleArm.Elbows;
        vertices.Clear();
        colors.Clear();
        triangles.Clear();
        DrawMount(joints[0], elbows[0]);

        int links = joints.Length - 1;
        for (int i = 0; i < links; i++)
        {
            float thickness = Mathf.Lerp(rootThickness, tipThickness, i / (float)Mathf.Max(1, links - 1));
            if (i == 0)
                DrawRootShell(joints[i], elbows[i], joints[i + 1], thickness);
            else
                DrawShortShell(joints[i], elbows[i], joints[i + 1], thickness);
        }
        for (int i = 0; i < links; i++)
        {
            float radius = Mathf.Lerp(rootThickness * 0.61f, tipThickness * 0.52f, i / (float)links);
            DrawBearing(joints[i], radius);
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

    private void DrawRootShell(Vector2 start, Vector2 elbow, Vector2 end, float width)
    {
        Vector2 first = elbow - start;
        Vector2 second = end - elbow;
        float firstLength = first.magnitude;
        float secondLength = second.magnitude;
        if (firstLength < 0.0001f || secondLength < 0.0001f)
            return;

        Vector2 incoming = first / firstLength;
        Vector2 outgoing = second / secondLength;
        Vector2 middleTangent = (incoming + outgoing).normalized;
        if (middleTangent == Vector2.zero)
            middleTangent = new Vector2(-incoming.y, incoming.x);

        // Matching handles produce a shared tangent at the solved elbow.
        // This is the exterior shell. All three rig anchors retain their pose.
        Vector2 middleHandle = middleTangent * (Mathf.Min(firstLength, secondLength) * 0.38f);
        float seam = firstLength / (firstLength + secondLength);
        DrawCubicShell(start, start + first * 0.28f, elbow - middleHandle, elbow,
            0f, seam, width, 22, true);
        DrawCubicShell(elbow, elbow + middleHandle, end - second * 0.28f, end,
            seam, 1f, width, 14, false);
    }

    private void DrawCubicShell(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        float from, float to, float width, int samples, bool startNew)
    {
        for (int i = startNew ? 0 : 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 ab = Vector2.Lerp(a, b, t);
            Vector2 bc = Vector2.Lerp(b, c, t);
            Vector2 cd = Vector2.Lerp(c, d, t);
            Vector2 left = Vector2.Lerp(ab, bc, t);
            Vector2 right = Vector2.Lerp(bc, cd, t);
            Vector2 center = Vector2.Lerp(left, right, t);
            AddShellRow(center, right - left, width, Mathf.Lerp(from, to, t), i != 0);
        }
    }

    private void DrawShortShell(Vector2 start, Vector2 elbow, Vector2 end, float width)
    {
        Vector2 first = elbow - start;
        Vector2 second = end - elbow;
        float firstLength = first.magnitude;
        float secondLength = second.magnitude;
        if (firstLength < 0.0001f || secondLength < 0.0001f)
            return;

        Vector2 incoming = first / firstLength;
        Vector2 outgoing = second / secondLength;
        float cut = Mathf.Min(width * 0.61f, Mathf.Min(firstLength, secondLength) * 0.18f);
        Vector2 near = elbow - incoming * cut;
        Vector2 far = elbow + outgoing * cut;
        float length = firstLength + secondLength;
        float arcStart = (firstLength - cut) / length;
        float arcEnd = (firstLength + cut) / length;

        for (int i = 0; i <= 8; i++)
        {
            float t = i / 8f;
            AddShellRow(Vector2.Lerp(start, near, t), incoming, width, arcStart * t, i > 0);
        }
        for (int i = 1; i <= 5; i++)
        {
            float t = i / 5f;
            Vector2 a = Vector2.Lerp(near, elbow, t);
            Vector2 b = Vector2.Lerp(elbow, far, t);
            AddShellRow(Vector2.Lerp(a, b, t), b - a, width, Mathf.Lerp(arcStart, arcEnd, t), true);
        }
        for (int i = 1; i <= 8; i++)
        {
            float t = i / 8f;
            AddShellRow(Vector2.Lerp(far, end, t), outgoing, width, Mathf.Lerp(arcEnd, 1f, t), true);
        }
    }

    private void AddShellRow(Vector2 center, Vector2 tangent, float width, float t, bool joinPrevious)
    {
        Vector2 axis = tangent.normalized;
        Vector2 side = new Vector2(-axis.y, axis.x);
        float shoulder = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(t - 0.11f) / 0.24f);
        float head = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(t - 0.89f) / 0.21f);
        float radius = width * (0.26f + 0.25f * shoulder + 0.22f * head);
        float sideLight = Vector2.Dot(side, LightDirection);
        Color rim = Color.Lerp(shadowColor, pinColor, 0.72f);
        int row = vertices.Count;
        int bands = ShellBands.Length;
        for (int i = 0; i < bands; i++)
        {
            float across = ShellBands[i];
            float crown = Mathf.Sqrt(1f - across * across);
            float light = Mathf.Clamp01(0.56f * crown + 0.24f * across * sideLight);
            Color fill = Color.Lerp(rim, baseMetalColor, Mathf.Clamp01(crown * 1.2f));
            Vertex(center + side * (radius * across), Color.Lerp(fill, highlightColor, light * 0.54f));
        }

        if (!joinPrevious)
            return;
        for (int i = row; i < row + bands - 1; i++)
        {
            Triangle(i - bands, i - bands + 1, i);
            Triangle(i, i - bands + 1, i + 1);
        }
    }

    private void DrawBearing(Vector2 center, float radius)
    {
        const int sides = 20;
        Disc(center, radius, shadowColor, sides);
        float capRadius = radius * 0.87f;
        int first = vertices.Count;
        Vertex(center + LightDirection * (capRadius * 0.24f), Color.Lerp(baseMetalColor, highlightColor, 0.76f));
        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = 0; i < sides; i++)
            {
                Vector2 ray = CircleDirection(i, sides);
                float light = Vector2.Dot(ray, LightDirection) * 0.5f + 0.5f;
                if (ring == 0)
                {
                    Vector2 inner = center + (ray * 0.53f + LightDirection * 0.10f) * capRadius;
                    Vertex(inner, Color.Lerp(baseMetalColor, highlightColor, light * 0.58f));
                }
                else
                {
                    Vertex(center + ray * capRadius, Color.Lerp(pinColor, baseMetalColor, light * 0.57f));
                }
            }
        }
        for (int i = 0; i < sides; i++)
        {
            int inside = first + 1 + i;
            int next = first + 1 + (i + 1) % sides;
            Triangle(first, inside, next);
            Triangle(inside, inside + sides, next);
            Triangle(next, inside + sides, next + sides);
        }
    }

    private void DrawBodySocket(Vector2 tip)
    {
        // Set the cuff just behind the robe edge and keep the pin outside it.
        Quad(tip + Vector2.left * 0.027f, Vector2.right, Vector2.up, 0.168f, 0.118f, shadowColor);
        Quad(tip + Vector2.left * 0.034f, Vector2.right, Vector2.up, 0.126f, 0.079f, baseMetalColor);
        DrawBearing(tip, 0.045f);
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
            Vertex(center + radius * CircleDirection(i, sides), color);
        for (int i = 0; i < sides; i++)
            Triangle(first, first + 1 + i, first + 1 + (i + 1) % sides);
    }

    private static Vector2 CircleDirection(int index, int sides)
    {
        float angle = index * 2f * Mathf.PI / sides;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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
