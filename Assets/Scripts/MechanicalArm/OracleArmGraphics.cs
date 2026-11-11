using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    private static readonly float[] SegmentStations = { 0f, 0.08f, 0.2f, 0.5f, 0.8f, 0.92f, 1f };
    private static readonly float[] SegmentProfile = { 0.42f, 0.88f, 1f, 0.95f, 0.86f, 0.72f, 0.36f };
    private static readonly float[] SegmentBands = { -1f, -0.72f, -0.24f, 0.4f, 0.78f, 1f };

    [Header("Physics")]
    public OracleArm oracleArm;

    [Header("Rain World metal palette")]
    public Color shadowColor = new Color(0.10f, 0.105f, 0.11f);
    public Color baseMetalColor = new Color(0.43f, 0.405f, 0.36f);
    public Color highlightColor = new Color(0.68f, 0.65f, 0.58f);
    public Color pipeColor = new Color(0.22f, 0.22f, 0.21f);

    [Header("Proportions")]
    public Sprite circleSprite;
    [Tooltip("Outer width of the first arm segment in world units.")]
    public float rootThickness = 0.32f;
    [Tooltip("Outer width of the end joint in world units.")]
    public float tipThickness = 0.12f;

    private MeshFilter meshFilter;
    private Mesh armMesh;
    private MeshRenderer meshRenderer;
    private readonly List<Vector3> vertices = new List<Vector3>(1024);
    private readonly List<Color> colors = new List<Color>(1024);
    private readonly List<int> triangles = new List<int>(2048);
    private SpriteRenderer[] jointOutlines;
    private SpriteRenderer[] jointFaces;
    private SpriteRenderer[] jointCenters;
    private Sprite fallbackCircle;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.sortingOrder = -10;

        armMesh = new Mesh();
        armMesh.name = "SS articulated arm";
        armMesh.MarkDynamic();
        meshFilter.sharedMesh = armMesh;

        if (circleSprite == null)
            fallbackCircle = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);

        if (oracleArm == null)
            return;

        int jointCount = oracleArm.segmentLengths == null ? 0 : oracleArm.segmentLengths.Length;
        jointOutlines = new SpriteRenderer[jointCount];
        jointFaces = new SpriteRenderer[jointCount];
        jointCenters = new SpriteRenderer[jointCount];
        for (int i = 0; i < jointCount; i++)
        {
            jointOutlines[i] = CreateJointSprite("JointOutline_" + i, shadowColor, -8);
            jointFaces[i] = CreateJointSprite("JointMetal_" + i, baseMetalColor, -7);
            jointCenters[i] = CreateJointSprite("JointCore_" + i, pipeColor, -6);
        }
    }

    private SpriteRenderer CreateJointSprite(string objectName, Color color, int sortingOrder)
    {
        GameObject jointObject = new GameObject(objectName);
        jointObject.transform.SetParent(transform, false);
        SpriteRenderer spriteRenderer = jointObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = circleSprite != null ? circleSprite : fallbackCircle;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        return spriteRenderer;
    }

    private void LateUpdate()
    {
        if (oracleArm == null || oracleArm.joints == null || oracleArm.segmentLengths == null ||
            oracleArm.joints.Length < 2 || armMesh == null)
            return;

        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        Vector2 root = oracleArm.joints[0];
        Vector2 first = oracleArm.joints[1];
        Vector2 armDirection = (first - root).sqrMagnitude > 0.0001f
            ? ((Vector2)first - root).normalized
            : Vector2.up;
        Vector2 tangent = TrackTangent(oracleArm.currentTrackPos);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = new Vector2(-armDirection.y, armDirection.x);

        // Flat room bracket: a small inset plate, not a rail trolley.
        Vector2 bracketCenter = root + armDirection * 0.14f;
        AddOrientedQuad(bracketCenter, tangent, armDirection, 0.78f, 0.46f, shadowColor, -0.03f);
        AddOrientedQuad(bracketCenter + armDirection * 0.015f, tangent, armDirection,
            0.63f, 0.31f, baseMetalColor, -0.02f);
        AddOrientedQuad(bracketCenter + armDirection * 0.025f, tangent, armDirection,
            0.43f, 0.035f, highlightColor, -0.01f);

        int boneCount = Mathf.Min(oracleArm.segmentLengths.Length, oracleArm.joints.Length - 1);
        for (int i = 0; i < boneCount; i++)
        {
            Vector2 start = oracleArm.joints[i];
            Vector2 end = oracleArm.joints[i + 1];
            DrawArmSegment(start, end, i, boneCount);
        }

        armMesh.Clear();
        armMesh.SetVertices(vertices);
        armMesh.SetColors(colors);
        armMesh.SetTriangles(triangles, 0);
        armMesh.RecalculateBounds();

        UpdateJoints(boneCount);
    }

    private void DrawArmSegment(Vector2 start, Vector2 end, int index, int count)
    {
        Vector2 direction = end - start;
        float length = direction.magnitude;
        if (length < 0.0001f)
            return;

        direction /= length;
        Vector2 side = new Vector2(-direction.y, direction.x);
        float blend = index / (float)Mathf.Max(1, count - 1);
        float halfStart = Mathf.Lerp(rootThickness, tipThickness, blend) * 0.5f;
        float halfEnd = Mathf.Lerp(rootThickness, tipThickness, (index + 1f) / count) * 0.5f;

        int firstVertex = vertices.Count;
        for (int row = 0; row < SegmentStations.Length; row++)
        {
            float t = SegmentStations[row];
            Vector2 center = Vector2.Lerp(start, end, t);
            float width = Mathf.Lerp(halfStart, halfEnd, t) * SegmentProfile[row];
            for (int col = 0; col < SegmentBands.Length; col++)
            {
                vertices.Add(ToLocal(center + side * width * SegmentBands[col], -0.015f));
                colors.Add(BandColor(col));
            }
        }

        for (int row = 0; row < SegmentStations.Length - 1; row++)
        {
            for (int col = 0; col < SegmentBands.Length - 1; col++)
            {
                int a = firstVertex + row * SegmentBands.Length + col;
                int b = a + 1;
                int c = a + SegmentBands.Length;
                int d = c + 1;
                AddTriangle(a, b, c);
                AddTriangle(c, b, d);
            }
        }

        // Narrow internal conduit and a few restrained plate seams match the source's
        // repeated metal-part sprites without adding oversized gears or bevels.
        AddRibbon(start + direction * (length * 0.12f) + side * halfStart * 0.1f,
            end - direction * (length * 0.12f) + side * halfEnd * 0.1f,
            Mathf.Max(0.012f, halfEnd * 0.1f), pipeColor, -0.005f);

        int seams = Mathf.Clamp(Mathf.RoundToInt(length / 0.42f), 2, 7);
        for (int seam = 1; seam < seams; seam++)
        {
            float t = seam / (float)seams;
            Vector2 point = Vector2.Lerp(start, end, t);
            float halfWidth = Mathf.Lerp(halfStart, halfEnd, t) * 0.78f;
            Vector2 seamStart = point - side * halfWidth;
            Vector2 seamEnd = point + side * halfWidth;
            AddRibbon(seamStart, seamEnd, 0.012f, shadowColor * 0.86f, -0.004f);
        }
    }

    private Color BandColor(int column)
    {
        switch (column)
        {
            case 0: return shadowColor;
            case 1: return baseMetalColor * 0.78f;
            case 2: return highlightColor * 0.9f;
            case 3: return baseMetalColor;
            case 4: return baseMetalColor * 0.72f;
            default: return shadowColor;
        }
    }

    private void AddRibbon(Vector2 start, Vector2 end, float halfWidth, Color color, float depth)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.000001f)
            return;
        direction.Normalize();
        Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
        AddQuad(start + side, start - side, end + side, end - side, color, depth);
    }

    private void AddOrientedQuad(Vector2 center, Vector2 axisX, Vector2 axisY,
        float width, float height, Color color, float depth)
    {
        axisX.Normalize();
        axisY.Normalize();
        Vector2 x = axisX * width * 0.5f;
        Vector2 y = axisY * height * 0.5f;
        AddQuad(center - x - y, center + x - y, center - x + y, center + x + y, color, depth);
    }

    private void AddQuad(Vector2 bottomLeft, Vector2 bottomRight, Vector2 topLeft, Vector2 topRight,
        Color color, float depth)
    {
        int first = vertices.Count;
        vertices.Add(ToLocal(bottomLeft, depth));
        vertices.Add(ToLocal(bottomRight, depth));
        vertices.Add(ToLocal(topLeft, depth));
        vertices.Add(ToLocal(topRight, depth));
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        AddTriangle(first, first + 1, first + 2);
        AddTriangle(first + 2, first + 1, first + 3);
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

    private Vector2 TrackTangent(float trackPosition)
    {
        Vector2 before = FramePosition(trackPosition - 0.001f);
        Vector2 after = FramePosition(trackPosition + 0.001f);
        return (after - before).normalized;
    }

    private Vector2 FramePosition(float trackPosition)
    {
        // The physics script owns the rounded SS room frame.
        return oracleArm.GetFramePositionForGraphics(trackPosition);
    }

    private void UpdateJoints(int boneCount)
    {
        if (jointOutlines == null)
            return;
        for (int i = 0; i < jointOutlines.Length; i++)
        {
            if (i >= oracleArm.joints.Length - 1 || i > boneCount)
            {
                jointOutlines[i].enabled = false;
                jointFaces[i].enabled = false;
                jointCenters[i].enabled = false;
                continue;
            }

            Vector3 position = oracleArm.joints[i];
            float width = Mathf.Lerp(rootThickness, tipThickness, i / (float)Mathf.Max(1, jointOutlines.Length - 1));
            float outer = width * (i == 0 ? 1.34f : 1.16f);
            jointOutlines[i].enabled = true;
            jointFaces[i].enabled = true;
            jointCenters[i].enabled = true;
            jointOutlines[i].transform.position = position + Vector3.back * 0.025f;
            jointFaces[i].transform.position = position + Vector3.back * 0.02f;
            jointCenters[i].transform.position = position + Vector3.back * 0.015f;
            jointOutlines[i].transform.localScale = Vector3.one * outer;
            jointFaces[i].transform.localScale = Vector3.one * outer * 0.7f;
            jointCenters[i].transform.localScale = Vector3.one * outer * 0.22f;
        }
    }

    private void OnDestroy()
    {
        if (armMesh != null)
            Destroy(armMesh);
        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            Destroy(meshRenderer.sharedMaterial);
        if (fallbackCircle != null)
            Destroy(fallbackCircle);
    }
}
