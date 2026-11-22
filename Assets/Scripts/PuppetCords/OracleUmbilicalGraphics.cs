using System.Collections.Generic;
using UnityEngine;

// Rain World draws the umbilical trunk as a thin long mesh plus two
// CentipedeSegment sprites per sample (OracleGraphics.UbilicalCord):
//
//   TriangleMesh over 80 samples, half width 1.2 px, colour metalColor
//   SegmentSprite(i, 0): CentipedeSegment, scale 0.5 x 0.3, colour
//                        Lerp(BaseColor, metalColor, 0.5)
//   SegmentSprite(i, 1): CentipedeSegment, scale 0.4 x 0.15, colour
//                        Lerp(HighLightColor, metalColor, 0.35)
//   both rotated so the sprite's long axis follows the cord
//
// Space the desktop bands in arc length. The pitch is calibrated against the
// visible room height in the footage, independently of the sprite dimensions.
[DefaultExecutionOrder(60)]
public class OracleUmbilicalGraphics : MonoBehaviour
{
    public OracleUmbilical umbilicalPhysics;
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Tooltip("Enlargement of the cord layers, matching OracleArmSprites.detailScale.")]
    [Min(0.1f)] public float detailScale = 1.35f;
    [Tooltip("Distance between band centers along the desktop cable, before applying detailScale.")]
    [Min(1f)] public float bandSpacingPx = 18f;
    [Tooltip("Trunk mesh width. The game uses a 1.2 px half width.")]
    [Min(0.1f)] public float trunkHalfWidthPx = 1.2f;

    [Header("Palette")]
    [Tooltip("ArmJointGraphics.metalColor, also used for the trunk mesh.")]
    public Color trunkColor = new Color(0.145f, 0.142f, 0.138f);
    [Tooltip("Lerp(BaseColor, metalColor, 0.5)")]
    public Color bandColor = new Color(0.240f, 0.230f, 0.213f);
    [Tooltip("Lerp(HighLightColor, metalColor, 0.35)")]
    public Color bandLightColor = new Color(0.312f, 0.303f, 0.283f);

    [Header("Fine cords")]
    public Color[] cordColors =
    {
        new Color(0.46f, 0.36f, 0.28f),
        new Color(0.30f, 0.38f, 0.40f),
        new Color(0.41f, 0.27f, 0.25f)
    };
    [Min(0f)] public float cordWidth = 0.015f;

    [Header("Sorting orders")]
    public int trunkOrder = -17;
    public int bandOrder = -16;
    public int cordOrder = -14;

    private const int MaxBands = 400;

    private class Band
    {
        public SpriteRenderer dark;
        public SpriteRenderer light;
    }

    private Material cableMaterial;
    private Mesh trunkMesh;
    private Vector3[] trunkVertices;
    private Color[] trunkColors;
    private int trunkRows;
    private Sprite bandSprite;
    private readonly List<Band> bands = new List<Band>();
    private LineRenderer[] cords;

    private void Start()
    {
        cableMaterial = new Material(Shader.Find("Sprites/Default"));
        bandSprite = Resources.Load<Sprite>("IteratorArm/CentipedeSegment");

        GameObject trunk = new GameObject("Trunk mesh");
        trunk.transform.SetParent(transform, false);
        trunkMesh = new Mesh { name = "Umbilical trunk" };
        trunkMesh.MarkDynamic();
        trunk.AddComponent<MeshFilter>().sharedMesh = trunkMesh;
        MeshRenderer renderer = trunk.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = cableMaterial;
        renderer.sortingOrder = trunkOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (umbilicalPhysics == null || umbilicalPhysics.trunkPos == null)
            return;

        Vector2[] path = umbilicalPhysics.trunkPos;
        DrawTrunk(path);
        LayoutBands(path);

        Vector2[][] paths = umbilicalPhysics.smallCordsPos;
        if (cords == null)
        {
            cords = new LineRenderer[paths.Length];
            for (int i = 0; i < cords.Length; i++)
            {
                Color color = cordColors != null && cordColors.Length > 0
                    ? cordColors[i % cordColors.Length] : bandLightColor;
                cords[i] = CreateLine("Data cord " + (i + 1), cordWidth, color, cordOrder);
            }
        }
        for (int i = 0; i < cords.Length; i++)
            SetPath(cords[i], paths[i]);
    }

    private void DrawTrunk(Vector2[] path)
    {
        int rows = path.Length;
        if (trunkVertices == null || trunkRows != rows)
        {
            trunkVertices = new Vector3[rows * 2];
            trunkColors = new Color[rows * 2];
            for (int i = 0; i < trunkColors.Length; i++)
                trunkColors[i] = trunkColor;
            trunkRows = rows;
            trunkMesh.Clear();
        }

        float halfWidth = Pixels(trunkHalfWidthPx);
        for (int i = 0; i < rows; i++)
        {
            Vector2 normal = NormalAt(path, i) * halfWidth;
            trunkVertices[i * 2] = transform.InverseTransformPoint(path[i] - normal);
            trunkVertices[i * 2 + 1] = transform.InverseTransformPoint(path[i] + normal);
        }

        trunkMesh.vertices = trunkVertices;
        trunkMesh.colors = trunkColors;
        if (trunkMesh.triangles == null || trunkMesh.triangles.Length != (rows - 1) * 6)
        {
            int[] triangles = new int[(rows - 1) * 6];
            for (int i = 0; i < rows - 1; i++)
            {
                int v = i * 2;
                triangles[i * 6] = v;
                triangles[i * 6 + 1] = v + 2;
                triangles[i * 6 + 2] = v + 1;
                triangles[i * 6 + 3] = v + 1;
                triangles[i * 6 + 4] = v + 2;
                triangles[i * 6 + 5] = v + 3;
            }
            trunkMesh.triangles = triangles;
        }
        trunkMesh.RecalculateBounds();
    }

    // One CentipedeSegment pair every bandSpacingPx of cord length. The sprite's
    // local +X follows the cord, which is what rotation = VecToDeg(dir) does in
    // the original (the local +Y is perpendicular to the cord).
    private void LayoutBands(Vector2[] path)
    {
        int last = path.Length - 1;
        if (last < 1 || bandSprite == null)
        {
            HideBands(0);
            return;
        }

        float spacing = Mathf.Max(0.0001f, Pixels(bandSpacingPx));
        float spacingDarkX = 0.5f * detailScale;
        float spacingDarkY = 0.3f * detailScale;
        float spacingLightX = 0.4f * detailScale;
        float spacingLightY = 0.15f * detailScale;

        int used = 0;
        float travelled = 0f;
        float nextBand = spacing * 0.5f;
        for (int i = 0; i < last && used < MaxBands; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[i + 1];
            float length = Vector2.Distance(a, b);
            if (length < 0.000001f)
                continue;
            Vector2 direction = (b - a) / length;
            while (nextBand <= travelled + length && used < MaxBands)
            {
                float t = (nextBand - travelled) / length;
                Vector2 position = Vector2.Lerp(a, b, t);
                PlaceBand(used, position, direction, spacingDarkX, spacingDarkY,
                    spacingLightX, spacingLightY);
                used++;
                nextBand += spacing;
            }
            travelled += length;
        }
        HideBands(used);
    }

    private void PlaceBand(int index, Vector2 position, Vector2 direction,
        float darkX, float darkY, float lightX, float lightY)
    {
        while (bands.Count <= index)
            bands.Add(CreateBand());
        Band band = bands[index];
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        band.dark.enabled = true;
        band.light.enabled = true;
        PlaceSprite(band.dark, position, rotation, new Vector2(darkX, darkY));
        PlaceSprite(band.light, position, rotation, new Vector2(lightX, lightY));
    }

    private Band CreateBand()
    {
        Band band = new Band
        {
            dark = CreateSprite("Cord band", bandColor, bandOrder),
            light = CreateSprite("Cord band highlight", bandLightColor, bandOrder + 1)
        };
        return band;
    }

    private void HideBands(int used)
    {
        for (int i = used; i < bands.Count; i++)
        {
            bands[i].dark.enabled = false;
            bands[i].light.enabled = false;
        }
    }

    private SpriteRenderer CreateSprite(string label, Color color, int order)
    {
        GameObject created = new GameObject(label);
        created.transform.SetParent(transform, false);
        SpriteRenderer renderer = created.AddComponent<SpriteRenderer>();
        renderer.sprite = bandSprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void PlaceSprite(SpriteRenderer renderer, Vector2 worldPosition,
        float zRotationDegrees, Vector2 scale)
    {
        Transform child = renderer.transform;
        child.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        child.localRotation = Quaternion.Euler(0f, 0f, zRotationDegrees);
        child.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private LineRenderer CreateLine(string label, float width, Color color, int order)
    {
        GameObject lineObject = new GameObject(label);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = cableMaterial;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 3;
        line.numCornerVertices = 4;
        line.sortingOrder = order;
        return line;
    }

    private static void SetPath(LineRenderer line, Vector2[] points)
    {
        line.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, points[i]);
    }

    private static Vector2 NormalAt(Vector2[] path, int i)
    {
        Vector2 tangent = path[Mathf.Min(path.Length - 1, i + 1)] - path[Mathf.Max(0, i - 1)];
        if (tangent.sqrMagnitude < 0.000001f)
            return Vector2.right;
        return new Vector2(-tangent.y, tangent.x).normalized;
    }

    private float Pixels(float value)
    {
        return value * detailScale / Mathf.Max(1f, pixelsPerUnit);
    }

    private void OnDestroy()
    {
        if (trunkMesh != null)
            Destroy(trunkMesh);
        if (cableMaterial != null)
            Destroy(cableMaterial);
    }
}
