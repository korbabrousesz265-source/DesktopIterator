using System.Collections.Generic;
using UnityEngine;

// The long spans are the only part of the arm the game draws as a mesh. This is a
// direct port of OracleGraphics.ArmJointGraphics.DrawSprites:
//
//   * each joint owns two cubic halves, joint -> elbow and elbow -> next, both
//     sharing the mid direction at the elbow;
//   * each half is sampled meshSegs = totalLength / 10 times (30 / 15 / 9 / 3 for
//     the four SS spans) and every row writes four vertices: a "join" pair at the
//     midpoint to the previous row and a "ring" pair on the row itself;
//   * width = num3 * num5(t) with num3 = 7 / 5 / 4 / 3 by joint index and
//     num5(t) = 0.6 + 0.5 (1 - sin(pi t)) + 0.3 max(sin(lerp(0,0.3,t) pi),
//     sin(lerp(0.7,1,t) pi)), forced to 0.5 at t == 1;
//   * on the affected rows (first half t > 0.75, second half t < 0.25) num5 halves
//     and the row shifts sideways by num6 = num5 * num3 * (index even ? 1 : -1),
//     which is one local half width - the hook shoulder;
//   * the highlight is a second, concentric mesh at half width.
//
// Our spans are the same nominal lengths (300 / 150 / 90 / 30 game pixels), so the
// row counts match. The only desktop factor is gamePixelScale: the pet renders the
// game's pixels at 1.35x so the arm, the head and the cord share one pixel scale.
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    public OracleArm oracleArm;
    public Color shadowColor = new Color(0.105f, 0.104f, 0.106f);
    public Color baseMetalColor = new Color(0.32f, 0.31f, 0.29f);
    public Color highlightColor = new Color(0.40f, 0.39f, 0.37f);
    public Color pinColor = new Color(0.18f, 0.18f, 0.19f);

    [Header("Pixel scale")]
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Tooltip("Game pixels -> world scale. 1.35 matches the puppet head and the cord bands.")]
    [Min(0.1f)] public float gamePixelScale = 1.35f;

    [Header("Sorting")]
    public int shellOrder = -10;
    public int shellHighlightOrder = -9;

    // num3 per joint index: shell half width at t = 0 in game pixels.
    private static readonly float[] JointHalfWidth = { 7f, 5f, 4f, 3f };

    private Mesh shellMesh;
    private Mesh highlightMesh;
    private MeshRenderer highlightRenderer;
    private Material metalMaterial;
    private OracleArmSprites armSprites;

    private readonly List<Vector3> shellVertices = new List<Vector3>(4096);
    private readonly List<Vector3> highlightVertices = new List<Vector3>(4096);
    private readonly List<int> shellTriangles = new List<int>(8192);
    private readonly List<int> highlightTriangles = new List<int>(8192);

    private void Awake()
    {
        // The joints and the carriage are sprite layers in the original game.
        armSprites = GetComponent<OracleArmSprites>();
        if (armSprites == null)
            armSprites = gameObject.AddComponent<OracleArmSprites>();
        if (armSprites.oracleArm == null)
            armSprites.oracleArm = oracleArm;
    }

    private void Start()
    {
        metalMaterial = new Material(Shader.Find("Sprites/Default"));

        shellMesh = new Mesh { name = "Arm shells" };
        shellMesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = shellMesh;
        MeshRenderer shellRenderer = GetComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = metalMaterial;
        shellRenderer.sortingOrder = shellOrder;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;

        GameObject highlight = new GameObject("Arm shell highlight");
        highlight.transform.SetParent(transform, false);
        highlightMesh = new Mesh { name = "Arm shells highlight" };
        highlightMesh.MarkDynamic();
        highlight.AddComponent<MeshFilter>().sharedMesh = highlightMesh;
        highlightRenderer = highlight.AddComponent<MeshRenderer>();
        highlightRenderer.sharedMaterial = metalMaterial;
        highlightRenderer.sortingOrder = shellHighlightOrder;
        highlightRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        highlightRenderer.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (oracleArm == null || oracleArm.joints == null || oracleArm.joints.Length < 2 ||
            oracleArm.Elbows == null || oracleArm.Elbows.Length + 1 != oracleArm.joints.Length)
            return;

        Vector3[] joints = oracleArm.joints;
        Vector2[] elbows = oracleArm.Elbows;
        shellVertices.Clear();
        highlightVertices.Clear();
        shellTriangles.Clear();
        highlightTriangles.Clear();

        int spans = joints.Length - 1;
        for (int i = 0; i < spans; i++)
        {
            float halfWidth = JointHalfWidth[Mathf.Min(i, JointHalfWidth.Length - 1)];
            float nominalPixels = SpanNominalPixels(i);
            int rows = Mathf.Max(2, Mathf.RoundToInt(nominalPixels / 10f));
            DrawSpan(i, joints[i], elbows[i], joints[i + 1], halfWidth, nominalPixels, rows);
        }

        ApplyMesh(shellMesh, shellVertices, shellTriangles, baseMetalColor);
        ApplyMesh(highlightMesh, highlightVertices, highlightTriangles, highlightColor);
        DrawTipCuff(joints[spans]);
    }

    private float SpanNominalPixels(int index)
    {
        if (oracleArm.segmentLengths != null && index < oracleArm.segmentLengths.Length)
            return Mathf.Max(10f, oracleArm.segmentLengths[index] * pixelsPerUnit);
        return 300f;
    }

    private void DrawSpan(int index, Vector2 joint, Vector2 elbow, Vector2 next,
        float num3, float nominalPixels, int rows)
    {
        Vector2 d1 = Direction(joint, elbow);
        Vector2 d2 = Direction(elbow, next);
        Vector2 mid = Vector3.Slerp(d1, d2, 0.5f);
        if (mid.sqrMagnitude < 1e-8f)
            mid = new Vector2(-d1.y, d1.x);

        // The original insets both ends by half of Lerp(length * 0.05, 4, 0.5).
        Vector2 start = joint + d1 * Pixel(Mathf.Lerp(nominalPixels * 0.05f, 4f, 0.5f));
        Vector2 end = next - d2 * Pixel(Mathf.Lerp(nominalPixels * 0.025f, 4f, 0.5f));
        float firstLength = Vector2.Distance(start, elbow);
        float secondLength = Vector2.Distance(elbow, end);

        Vector2 cA = start + d1 * (firstLength * 0.2f);
        Vector2 cB = elbow - mid * (firstLength * 0.2f);
        Vector2 cA2 = elbow + mid * (secondLength * 0.2f);
        Vector2 cB2 = end - d2 * (secondLength * 0.2f);

        DrawHalf(index, start, cA, elbow, cB, d1, num3, rows, true);
        DrawHalf(index, elbow, cA2, end, cB2, mid, num3, rows, false);
    }

    private void DrawHalf(int index, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Vector2 startDirection, float num3, int rows, bool firstHalf)
    {
        Vector2 previous = a;
        Vector2 previousDirection = startDirection;
        float previousWidth = 0.5f;

        int shellRowStart = -1;
        int highlightRowStart = -1;
        int previousShellRow = -1;
        int previousHighlightRow = -1;

        for (int row = 1; row <= rows; row++)
        {
            float t = row / (float)(rows - 1);
            float num5 = Profile(t);
            if (t == 1f)
                num5 = 0.5f;

            float offset = 0f;
            if ((firstHalf && t > 0.75f) || (!firstHalf && t < 0.25f))
            {
                num5 *= 0.5f;
                offset = num5 * num3 * ((index % 2 == 0) ? 1f : -1f);
            }

            Vector2 point = Bezier(a, b, c, d, t);
            Vector2 direction = BezierTangent(a, b, c, d, t);
            if (direction.sqrMagnitude < 1e-10f)
                direction = previousDirection;
            direction.Normalize();

            float joinWidth = num3 * (num5 + previousWidth) * 0.5f;
            Vector2 middle = (previous + point) * 0.5f;
            Vector2 previousPerp = new Vector2(-previousDirection.y, previousDirection.x);
            Vector2 perp = new Vector2(-direction.y, direction.x);

            Vector2 j0 = middle + previousPerp * Pixel(offset + joinWidth);
            Vector2 j1 = middle + previousPerp * Pixel(offset - joinWidth);
            Vector2 r0 = point + perp * Pixel(offset + num3 * num5);
            Vector2 r1 = point + perp * Pixel(offset - num3 * num5);

            int shellRow = AppendRow(shellVertices, j0, j1, r0, r1);
            int highlightRow = AppendRow(highlightVertices,
                middle + previousPerp * Pixel(offset + joinWidth * 0.5f),
                middle + previousPerp * Pixel(offset - joinWidth * 0.5f),
                point + perp * Pixel(offset + num3 * num5 * 0.5f),
                point + perp * Pixel(offset - num3 * num5 * 0.5f));

            if (shellRowStart < 0)
            {
                shellRowStart = shellRow;
                highlightRowStart = highlightRow;
            }
            if (previousShellRow >= 0)
            {
                Stitch(shellTriangles, previousShellRow, shellRow);
                Stitch(highlightTriangles, previousHighlightRow, highlightRow);
            }
            RowQuad(shellTriangles, shellRow);
            RowQuad(highlightTriangles, highlightRow);

            previousShellRow = shellRow;
            previousHighlightRow = highlightRow;
            previous = point;
            previousDirection = direction;
            previousWidth = num5;
        }
    }

    private int AppendRow(List<Vector3> vertices, Vector2 joinLeft, Vector2 joinRight,
        Vector2 ringLeft, Vector2 ringRight)
    {
        int first = vertices.Count;
        vertices.Add(transform.InverseTransformPoint(joinLeft));
        vertices.Add(transform.InverseTransformPoint(joinRight));
        vertices.Add(transform.InverseTransformPoint(ringLeft));
        vertices.Add(transform.InverseTransformPoint(ringRight));
        return first;
    }

    private static void Stitch(List<int> triangles, int previousRow, int row)
    {
        // Connect the previous row's ring pair to this row's join pair.
        triangles.Add(previousRow + 2);
        triangles.Add(previousRow + 3);
        triangles.Add(row + 0);
        triangles.Add(row + 0);
        triangles.Add(previousRow + 3);
        triangles.Add(row + 1);
    }

    private static void RowQuad(List<int> triangles, int row)
    {
        // Row layout: [join left, join right, ring left, ring right].
        triangles.Add(row + 0);
        triangles.Add(row + 1);
        triangles.Add(row + 2);
        triangles.Add(row + 2);
        triangles.Add(row + 1);
        triangles.Add(row + 3);
    }

    private void ApplyMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles, Color color)
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        int count = vertices.Count;
        Color[] colors = new Color[count];
        for (int i = 0; i < count; i++)
            colors[i] = color;
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
    }

    private void DrawTipCuff(Vector2 tip)
    {
        // The cuff keeps the robe edge closed where the last link meets the puppet.
        // It is desktop-only; the original ends on the puppet body chunk.
        int first = shellVertices.Count;
        Vector2 left = tip + Vector2.left * 0.027f;
        Vector2 right = tip + Vector2.left * 0.034f;
        shellVertices.Add(transform.InverseTransformPoint(left + new Vector2(-0.084f, -0.059f)));
        shellVertices.Add(transform.InverseTransformPoint(left + new Vector2(-0.084f, 0.059f)));
        shellVertices.Add(transform.InverseTransformPoint(left + new Vector2(0.084f, -0.059f)));
        shellVertices.Add(transform.InverseTransformPoint(left + new Vector2(0.084f, 0.059f)));
        shellVertices.Add(transform.InverseTransformPoint(right + new Vector2(-0.063f, -0.0395f)));
        shellVertices.Add(transform.InverseTransformPoint(right + new Vector2(-0.063f, 0.0395f)));
        shellVertices.Add(transform.InverseTransformPoint(right + new Vector2(0.063f, -0.0395f)));
        shellVertices.Add(transform.InverseTransformPoint(right + new Vector2(0.063f, 0.0395f)));
        shellTriangles.Add(first + 0); shellTriangles.Add(first + 1); shellTriangles.Add(first + 2);
        shellTriangles.Add(first + 2); shellTriangles.Add(first + 1); shellTriangles.Add(first + 3);
        shellTriangles.Add(first + 4); shellTriangles.Add(first + 5); shellTriangles.Add(first + 6);
        shellTriangles.Add(first + 6); shellTriangles.Add(first + 5); shellTriangles.Add(first + 7);
    }

    // num5(t) from ArmJointGraphics.DrawSprites.
    private static float Profile(float t)
    {
        float sine = Mathf.Sin(t * Mathf.PI);
        float startBump = Mathf.Sin(Mathf.InverseLerp(0f, 0.3f, t) * Mathf.PI);
        float endBump = Mathf.Sin(Mathf.InverseLerp(0.7f, 1f, t) * Mathf.PI);
        return 0.6f + 0.5f * (1f - sine) + 0.3f * Mathf.Max(startBump, endBump);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    private static Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
    }

    private static Vector2 Direction(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        if (delta.sqrMagnitude < 1e-10f)
            return Vector2.down;
        return delta.normalized;
    }

    private float Pixel(float gamePixels)
    {
        return gamePixels * gamePixelScale / Mathf.Max(1f, pixelsPerUnit);
    }

    private void OnDestroy()
    {
        if (shellMesh != null)
            Destroy(shellMesh);
        if (highlightMesh != null)
            Destroy(highlightMesh);
        if (metalMaterial != null)
            Destroy(metalMaterial);
    }
}
