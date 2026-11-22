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
    private OracleArmSprites armSprites;
    private readonly List<Vector3> vertices = new List<Vector3>(1280);
    private readonly List<Color> colors = new List<Color>(1280);
    private readonly List<int> triangles = new List<int>(4608);
    private static readonly float[] ShellBands = { -1f, -0.77f, -0.30f, 0.30f, 0.77f, 1f };
    private static readonly Vector2 LightDirection = new Vector2(-0.6f, 0.8f);

    private void Awake()
    {
        // The joints and the carriage are sprite layers in the original game.
        // They live in their own component so the flat shell mesh stays readable.
        armSprites = GetComponent<OracleArmSprites>();
        if (armSprites == null)
            armSprites = gameObject.AddComponent<OracleArmSprites>();
        if (armSprites.oracleArm == null)
            armSprites.oracleArm = oracleArm;
    }

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

        int links = joints.Length - 1;
        for (int i = 0; i < links; i++)
        {
            float thickness = Mathf.Lerp(rootThickness, tipThickness, i / (float)Mathf.Max(1, links - 1));
            if (i == 0)
                DrawRootShell(joints[i], elbows[i], joints[i + 1], thickness);
            else
                DrawShortShell(joints[i], elbows[i], joints[i + 1], thickness);
        }
        DrawTipCuff(joints[links]);

        armMesh.Clear();
        armMesh.SetVertices(vertices);
        armMesh.SetColors(colors);
        armMesh.SetTriangles(triangles, 0);
        armMesh.RecalculateBounds();
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

    private void DrawTipCuff(Vector2 tip)
    {
        // The cuff keeps the robe edge closed where the last link meets the
        // puppet. The socket disc on top of it is a sprite in OracleArmSprites.
        Quad(tip + Vector2.left * 0.027f, Vector2.right, Vector2.up, 0.168f, 0.118f, shadowColor);
        Quad(tip + Vector2.left * 0.034f, Vector2.right, Vector2.up, 0.126f, 0.079f, baseMetalColor);
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
