using UnityEngine;

// Rain World builds the oracle arm out of two kinds of parts:
//   * the long spans are a TriangleMesh built from the joint chords
//     (OracleGraphics.ArmJointGraphics.DrawSprites, the bezier band),
//   * the joints and the carriage are sprites layered on top of it.
//
// This component restores the second half with the sprites exported from the
// shipped atlas:
//   Circle20            joint discs, carriage side discs
//   deerEyeB            bolt caps on the discs, the base support knees
//   MirosLegSmallPart   toothed strut hanging back from every elbow
//   pixel               1 px bar for the cogs, the two rods and the supports
//
// Positions and directions follow ArmJointGraphics.DrawSprites, its
// InitiateSprites scales and ArmBase.DrawSprites / ArmBase.UpdateMesh. The one
// deliberate change is detailScale: the desktop shell is about twice as thick as
// the original 12 px band, so the sprite layers are enlarged with it to keep the
// original proportions against the arm.
[DefaultExecutionOrder(60)]
public class OracleArmSprites : MonoBehaviour
{
    public OracleArm oracleArm;
    [Tooltip("Rain World draws in pixels; the project maps 1 px to 1/pixelsPerUnit units.")]
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Tooltip("Enlargement of the sprite layers so they keep their proportions against the thicker desktop shell.")]
    [Min(0.1f)] public float detailScale = 1.35f;

    [Header("Palette")]
    [Tooltip("ArmJointGraphics.ApplyPalette: black palette colour mixed with the room texture.")]
    public Color metalColor = new Color(0.145f, 0.142f, 0.138f);
    [Tooltip("ArmBase.ApplyPalette: metal mixed 80% towards the arm base colour.")]
    public Color plateColor = new Color(0.335f, 0.318f, 0.288f);
    public Color plateHighlightColor = new Color(0.402f, 0.386f, 0.352f);

    [Header("Base plate (Rain World pixels)")]
    [Tooltip("How far the carriage sits inside the screen edge.")]
    [Min(0f)] public float baseInset = 6f;
    [Min(1f)] public float baseWidth = 30f;
    [Min(1f)] public float baseHeight = 17f;
    [Min(1f)] public float baseInnerWidth = 20f;
    [Min(1f)] public float baseWidthInner = 24f;
    [Min(1f)] public float baseHeightInner = 15f;
    [Min(1f)] public float baseInnerWidthInner = 30f;
    [Min(1f)] public float supportShortLength = 25f;
    [Min(1f)] public float supportLongLength = 45f;

    [Tooltip("Diagnostics only: log the solved layout once per second.")]
    public bool debugLayout = false;

    [Header("Sorting orders")]
    public int rodOrder = -14;
    public int cogOrder = -13;
    public int elbowDiscOrder = -12;
    public int elbowBoltOrder = -11;
    public int jointDiscOrder = -8;
    public int jointBoltOrder = -7;
    public int basePlateOrder = -6;
    public int baseSupportOrder = -5;
    public int baseDiscOrder = -4;
    public int tipOrder = -3;

    private Sprite circleSprite;
    private Sprite boltSprite;
    private Sprite strutSprite;
    private Sprite pixelSprite;
    private Material whiteMaterial;

    private JointVisuals[] jointVisuals;
    private SpriteRenderer[] baseSupports;
    private SpriteRenderer[] baseSupportBolts;
    private SpriteRenderer[] baseDiscs;
    private SpriteRenderer tipDisc;
    private Mesh basePlateMesh;
    private MeshRenderer basePlateRenderer;
    private readonly Vector3[] basePlateBuffer = new Vector3[36];
    private readonly Color[] basePlateColors = new Color[36];

    private bool ready;

    private class JointVisuals
    {
        public SpriteRenderer disc;
        public SpriteRenderer discBolt;
        public SpriteRenderer elbowDisc;
        public SpriteRenderer elbowBolt;
        public SpriteRenderer strut;
        public SpriteRenderer rodNear;
        public SpriteRenderer rodFar;
        public SpriteRenderer rodTip;
        public readonly SpriteRenderer[] cogs = new SpriteRenderer[3];
        public float lastChord = -1f;
        public float cogRotation;
    }

    private void Start()
    {
        LoadSprites();
        BuildBasePlate();
        whiteMaterial = new Material(Shader.Find("Sprites/Default"));

        if (basePlateRenderer != null)
            basePlateRenderer.sharedMaterial = whiteMaterial;

        baseSupports = new SpriteRenderer[4];
        baseSupportBolts = new SpriteRenderer[2];
        baseDiscs = new SpriteRenderer[4];
        for (int side = 0; side < 2; side++)
        {
            for (int part = 0; part < 2; part++)
                baseSupports[side * 2 + part] = CreateSprite("Base support " + side + "-" + part,
                    pixelSprite, Color.Lerp(metalColor, plateColor, 0.35f), baseSupportOrder);
            baseSupportBolts[side] = CreateSprite("Base support knee " + side,
                boltSprite, metalColor, baseSupportOrder + 1);
            baseDiscs[side * 2] = CreateSprite("Base disc " + side, circleSprite, plateColor, baseDiscOrder);
            baseDiscs[side * 2 + 1] = CreateSprite("Base disc lip " + side,
                circleSprite, plateHighlightColor, baseDiscOrder + 1);
        }

        tipDisc = CreateSprite("Puppet socket disc", circleSprite, plateColor, tipOrder);
        ready = true;
    }

    private void LoadSprites()
    {
        circleSprite = LoadSprite("Circle20", 20, true);
        boltSprite = LoadSprite("deerEyeB", 5, false);
        strutSprite = LoadSprite("MirosLegSmallPart", 25, false);
        pixelSprite = LoadSprite("pixel", 1, false);
    }

    private static Sprite LoadSprite(string name, int size, bool round)
    {
        Sprite sprite = Resources.Load<Sprite>("IteratorArm/" + name);
        if (sprite != null)
            return sprite;

        // Keep the arm readable if the exported sprites are missing.
        int width = name == "MirosLegSmallPart" ? 4 : size;
        int height = size;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = true;
                if (round)
                {
                    float dx = (x + 0.5f) / width * 2f - 1f;
                    float dy = (y + 0.5f) / height * 2f - 1f;
                    inside = dx * dx + dy * dy <= 1f;
                }
                pixels[y * width + x] = inside ? Color.white : Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect);
    }

    private SpriteRenderer CreateSprite(string label, Sprite sprite, Color color, int order)
    {
        GameObject created = new GameObject(label);
        created.transform.SetParent(transform, false);
        SpriteRenderer renderer = created.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void BuildBasePlate()
    {
        GameObject plate = new GameObject("Base plate");
        plate.transform.SetParent(transform, false);
        MeshFilter filter = plate.AddComponent<MeshFilter>();
        basePlateRenderer = plate.AddComponent<MeshRenderer>();
        basePlateRenderer.sortingOrder = basePlateOrder;
        basePlateRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        basePlateRenderer.receiveShadows = false;

        // Three copies of the ArmBase bracket: wide plate, highlight plate, wide plate.
        for (int layer = 0; layer < 3; layer++)
        {
            int vertexBase = layer * 12;
            Color layerColor = layer == 1 ? plateHighlightColor : plateColor;
            for (int i = 0; i < 12; i++)
                basePlateColors[vertexBase + i] = layerColor;
        }

        int[] pattern = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 4, 5, 7, 6, 5, 7, 11, 6, 7, 11, 7, 8, 11, 8, 9, 11, 9, 10 };
        int[] triangles = new int[pattern.Length * 3];
        for (int layer = 0; layer < 3; layer++)
        {
            for (int i = 0; i < pattern.Length; i++)
                triangles[layer * pattern.Length + i] = pattern[i] + layer * 12;
        }

        basePlateMesh = new Mesh { name = "Oracle arm base plate" };
        basePlateMesh.MarkDynamic();
        basePlateMesh.vertices = new Vector3[36];
        basePlateMesh.colors = basePlateColors;
        basePlateMesh.triangles = triangles;
        filter.sharedMesh = basePlateMesh;
    }

    private void LateUpdate()
    {
        if (!ready || oracleArm == null)
            return;

        Vector3[] joints = oracleArm.joints;
        Vector2[] elbows = oracleArm.Elbows;
        if (joints == null || joints.Length < 2 || elbows == null || elbows.Length + 1 != joints.Length)
            return;

        int spanCount = joints.Length - 1;
        if (jointVisuals == null || jointVisuals.Length != spanCount)
            CreateJointVisuals(spanCount);

        for (int i = 0; i < spanCount; i++)
            LayoutJoint(i, joints, elbows, spanCount);

        LayoutBase(joints, elbows);
    }

    private void CreateJointVisuals(int spanCount)
    {
        jointVisuals = new JointVisuals[spanCount];
        for (int i = 0; i < spanCount; i++)
        {
            JointVisuals visuals = new JointVisuals();
            visuals.disc = CreateSprite("Joint disc " + i, circleSprite, metalColor, jointDiscOrder);
            visuals.discBolt = CreateSprite("Joint bolt " + i, boltSprite,
                Color.Lerp(metalColor, plateColor, 0.5f), jointBoltOrder);
            visuals.elbowDisc = CreateSprite("Elbow disc " + i, circleSprite, metalColor, elbowDiscOrder);
            visuals.elbowBolt = CreateSprite("Elbow bolt " + i, boltSprite,
                Color.Lerp(metalColor, plateColor, 0.5f), elbowBoltOrder);
            visuals.strut = CreateSprite("Joint strut " + i, strutSprite, metalColor, rodOrder);
            visuals.rodNear = CreateSprite("Joint rod near " + i, pixelSprite, metalColor, rodOrder - 1);
            visuals.rodFar = CreateSprite("Joint rod far " + i, pixelSprite, metalColor, rodOrder - 1);
            visuals.rodTip = CreateSprite("Joint rod tip " + i, boltSprite, metalColor, rodOrder);
            for (int cog = 0; cog < 3; cog++)
                visuals.cogs[cog] = CreateSprite("Joint cog " + i + "-" + cog, pixelSprite, metalColor, cogOrder);
            jointVisuals[i] = visuals;
        }
    }

    private void LayoutJoint(int index, Vector3[] joints, Vector2[] elbows, int spanCount)
    {
        JointVisuals visuals = jointVisuals[index];
        Vector2 pos = joints[index];
        Vector2 next = joints[index + 1];
        Vector2 elbow = elbows[index];
        float spanLength = Mathf.Max(0.01f, Vector2.Distance(pos, next));
        float configuredLength = oracleArm.segmentLengths != null && index < oracleArm.segmentLengths.Length
            ? Mathf.Max(0.01f, oracleArm.segmentLengths[index]) : spanLength;

        Vector2 toElbow = elbow - pos;
        if (toElbow.sqrMagnitude < 1e-8f)
            toElbow = next - pos;
        if (toElbow.sqrMagnitude < 1e-8f)
            toElbow = Vector2.up;
        toElbow.Normalize();

        // InitiateSprites: the first joint keeps its disc 12 px off the pivot,
        // the rest sit 2 px off it. Scale is (7 - index * 0.5) / 10.
        Vector2 discPos = pos + toElbow * Detail(index == 0 ? 12f : 2f);
        float discScale = (7f - index * 0.5f) / 10f * detailScale;
        PlaceSprite(visuals.disc, discPos, 0f, new Vector2(discScale, discScale));
        PlaceSprite(visuals.discBolt, discPos, 0f, Vector2.one * detailScale);

        // Second disc sits on the solved elbow, scale (6 - index * 1.5) / 10.
        float elbowScale = Mathf.Max(0.05f, (6f - index * 1.5f) / 10f * detailScale);
        PlaceSprite(visuals.elbowDisc, elbow, 0f, new Vector2(elbowScale, elbowScale));
        PlaceSprite(visuals.elbowBolt, elbow, 0f, Vector2.one * detailScale);

        // Update: the cogs turn with the change of the chord length.
        float chord = Vector2.Distance(pos, next);
        if (visuals.lastChord < 0f)
            visuals.lastChord = chord;
        float delta = (visuals.lastChord - chord) / Mathf.Max(0.01f, configuredLength);
        visuals.cogRotation += delta * ((index % 2 == 0) ? -1f : 1f) * 2f;
        visuals.lastChord = chord;

        float baseAngle = Aim(toElbow);
        float cogSizeX = (5f - index * 0.5f) * detailScale;
        float cogSizeY = (18f - index * 2f) * detailScale;
        for (int cog = 0; cog < 3; cog++)
        {
            PlaceSprite(visuals.cogs[cog], discPos, baseAngle + 60f * cog + visuals.cogRotation * 360f,
                new Vector2(cogSizeX, cogSizeY));
        }

        // MirosLegSmallPart: anchorY = 1 in the original, so the toothed strut
        // hangs back from the elbow towards the 20% point of the chord.
        Vector2 chordGuide = Vector2.Lerp(pos, next, 0.2f);
        Vector2 strutDirection = elbow - chordGuide;
        if (strutDirection.sqrMagnitude < 1e-8f)
            strutDirection = -toElbow;
        strutDirection.Normalize();
        float strutScaleX = (index == 0 ? 1.5f : (index == 1 ? 1f : 0.8f)) * ((index % 2 == 0) ? -1f : 1f);
        float strutScaleY = (index == 0 ? 2f : (index == 1 ? 1f : 0.5f)) * detailScale * 0.75f;
        Vector2 strutCenter = elbow - strutDirection * (Detail(25f) * strutScaleY * 0.5f);
        PlaceSprite(visuals.strut, strutCenter, Aim(strutDirection),
            new Vector2(strutScaleX * detailScale, strutScaleY));

        // pixel rod from the elbow towards the 80% point, fixed length = span / 4.
        // The desktop shell is a cubic that pulls away from the chord near the
        // elbow, so the rod is trimmed to stay under the plates.
        float smallArmLength = configuredLength / 4f;
        Vector2 rodGuide = Vector2.Lerp(pos, next, 0.8f);
        Vector2 rodDirection = rodGuide - elbow;
        if (rodDirection.sqrMagnitude < 1e-8f)
            rodDirection = toElbow;
        float guideDistance = rodDirection.magnitude;
        rodDirection /= Mathf.Max(0.0001f, guideDistance);
        smallArmLength = Mathf.Min(smallArmLength, guideDistance * 0.9f);
        float rodWidth = (index == 0 ? 2f : 1.4f) * detailScale;
        Vector2 rodCenter = elbow + rodDirection * (smallArmLength * 0.5f);
        PlaceSprite(visuals.rodNear, rodCenter, Aim(rodDirection),
            new Vector2(rodWidth, smallArmLength * pixelsPerUnit));

        Vector2 rodEnd = elbow + rodDirection * smallArmLength;
        Vector2 farDirection = rodEnd - next;
        if (farDirection.sqrMagnitude < 1e-8f)
            farDirection = -toElbow;
        float farDistance = farDirection.magnitude;
        farDirection /= Mathf.Max(0.0001f, farDistance);
        // ArmBase and ArmJointGraphics let the rod overrun its guide point; the
        // desktop shell is wider, so the overrun is clipped to the second link.
        float farLength = Mathf.Min(farDistance, Vector2.Distance(next, elbow) * 1.02f);
        PlaceSprite(visuals.rodFar, next + farDirection * (farLength * 0.5f), Aim(farDirection),
            new Vector2(rodWidth, farLength * pixelsPerUnit));
        PlaceSprite(visuals.rodTip, rodEnd, 0f, Vector2.one * detailScale);

        if (index == spanCount - 1)
        {
            // Desktop socket pin where the last link meets the puppet cuff.
            float tipScale = 0.42f * detailScale;
            PlaceSprite(tipDisc, joints[index + 1], 0f, new Vector2(tipScale, tipScale));
        }
    }

    private void LayoutBase(Vector3[] joints, Vector2[] elbows)
    {
        if (elbows.Length == 0)
            return;

        Vector2 root = joints[0];
        Vector2 tangent = oracleArm.GetCarriageTangent();
        Vector2 inward = new Vector2(tangent.y, -tangent.x);

        // ArmBase.DrawSprites places the plate centre behind the root joint. On the
        // desktop the rail is the screen edge, so the plate is moved inside far
        // enough that its back face stays visible instead of clipping away.
        Vector2 center = root + inward * (Detail(baseHeight) * 0.8f + Detail(baseInset));

        SetBasePlateLayer(0, center, inward, tangent, baseWidth, baseHeight, baseInnerWidth);
        SetBasePlateLayer(1, center, inward, tangent, baseWidthInner, baseHeightInner, baseInnerWidthInner);
        SetBasePlateLayer(2, center, inward, tangent, baseWidth, baseHeight, baseInnerWidth);
        if (basePlateMesh != null)
        {
            basePlateMesh.vertices = basePlateBuffer;
            basePlateMesh.colors = basePlateColors;
            basePlateMesh.RecalculateBounds();
        }

        Vector2 elbowAnchor = Vector2.Lerp(root, joints[1], 0.35f);
        if (debugLayout && Time.frameCount % 60 == 0)
        {
            Debug.Log("ARMDBG joints=" + string.Join(";", System.Array.ConvertAll(joints, v => v.x.ToString("F3") + "," + v.y.ToString("F3")))
                + " root=" + root + " center=" + center + " anchor=" + elbowAnchor
                + " tangent=" + tangent + " inward=" + inward
                + " elbow0=" + elbows[0] + " j1=" + (Vector2)joints[1]);
        }
        // Both pistons reach the same point on the first link, like ArmBase. The
        // desktop plate sits inside the screen edge instead of inside a wall, so the
        // leg pair is fitted to the distance it actually has to cover.
        float shortLength = Detail(supportShortLength);
        float longLength = Detail(supportLongLength);
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            Vector2 foot = center + inward * Detail(11f) + tangent * Detail(17f * sign);
            float span = Vector2.Distance(foot, elbowAnchor);
            float fit = Mathf.Min(1f, span * 1.03f / Mathf.Max(0.0001f, shortLength + longLength));
            Vector2 knee = TwoBoneJoint(foot, elbowAnchor, shortLength * fit, longLength * fit,
                side == 0 ? 1f : -1f);

            PlaceBar(baseSupports[side * 2], foot, knee, Detail(3f), Detail(10f) * fit);
            PlaceBar(baseSupports[side * 2 + 1], knee, elbowAnchor, Detail(3f), 0f);
            PlaceSprite(baseSupportBolts[side], knee, 0f, Vector2.one * detailScale);

            Vector2 discPos = center + tangent * Detail(17f * sign);
            float discScale = 0.5f * detailScale;
            PlaceSprite(baseDiscs[side * 2], discPos, 0f, new Vector2(discScale, discScale));
            float lipScale = 0.45f * detailScale;
            PlaceSprite(baseDiscs[side * 2 + 1], discPos + new Vector2(-Detail(1f), Detail(1f)), 0f,
                new Vector2(lipScale, lipScale));
        }
    }

    private void SetBasePlateLayer(int layer, Vector2 pos, Vector2 dir, Vector2 perp,
        float width, float height, float innerWidth)
    {
        float w = Detail(width);
        float h = Detail(height);
        float inner = Detail(innerWidth);
        Vector3 v0 = pos - inner * 0.5f * perp - h * dir;
        Vector3 v1 = pos - w * perp - h * 0.75f * dir;
        Vector3 v2 = pos - w * perp + h * 0.75f * dir;
        Vector3 v3 = pos - w * 0.8f * perp + h * dir;
        Vector3 v4 = pos - w * 0.5f * perp + h * dir;
        Vector3 v5 = pos - w * 0.3f * perp - h * 0.1f * dir;
        Vector3 v6 = pos + w * 0.3f * perp - h * 0.1f * dir;
        Vector3 v7 = pos + w * 0.5f * perp + h * dir;
        Vector3 v8 = pos + w * 0.8f * perp + h * dir;
        Vector3 v9 = pos + w * perp + h * 0.75f * dir;
        Vector3 v10 = pos + w * perp - h * 0.75f * dir;
        Vector3 v11 = pos + inner * 0.5f * perp - h * dir;

        int start = layer * 12;
        basePlateBuffer[start + 0] = transform.InverseTransformPoint(v0);
        basePlateBuffer[start + 1] = transform.InverseTransformPoint(v1);
        basePlateBuffer[start + 2] = transform.InverseTransformPoint(v2);
        basePlateBuffer[start + 3] = transform.InverseTransformPoint(v3);
        basePlateBuffer[start + 4] = transform.InverseTransformPoint(v4);
        basePlateBuffer[start + 5] = transform.InverseTransformPoint(v5);
        basePlateBuffer[start + 6] = transform.InverseTransformPoint(v6);
        basePlateBuffer[start + 7] = transform.InverseTransformPoint(v7);
        basePlateBuffer[start + 8] = transform.InverseTransformPoint(v8);
        basePlateBuffer[start + 9] = transform.InverseTransformPoint(v9);
        basePlateBuffer[start + 10] = transform.InverseTransformPoint(v10);
        basePlateBuffer[start + 11] = transform.InverseTransformPoint(v11);
    }

    private void PlaceBar(SpriteRenderer renderer, Vector2 start, Vector2 end, float width, float extraLength)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
        {
            renderer.enabled = false;
            return;
        }
        renderer.enabled = true;
        Vector2 direction = delta / distance;
        float length = distance + extraLength;
        PlaceSprite(renderer, start + direction * (length * 0.5f), Aim(direction),
            new Vector2(width * pixelsPerUnit, length * pixelsPerUnit));
    }

    private void PlaceSprite(SpriteRenderer renderer, Vector2 worldPosition, float zRotationDegrees, Vector2 scale)
    {
        Transform child = renderer.transform;
        child.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
        child.localRotation = Quaternion.Euler(0f, 0f, zRotationDegrees);
        child.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private float Pixels(float value)
    {
        return value / Mathf.Max(1f, pixelsPerUnit);
    }

    // Rain World pixel count -> world units, enlarged by detailScale.
    private float Detail(float pixels)
    {
        return Pixels(pixels * detailScale);
    }

    // Unity rotates counter clockwise; a sprite whose local +Y should follow
    // "direction" needs the angle minus 90 degrees.
    private static float Aim(Vector2 direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }

    // Two bone solve matching Custom.InverseKinematic(start, target, len1, len2, side).
    private static Vector2 TwoBoneJoint(Vector2 start, Vector2 target, float len1, float len2, float side)
    {
        Vector2 delta = target - start;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
            return start + new Vector2(0f, len1);
        float clamped = Mathf.Clamp(distance, Mathf.Abs(len1 - len2) + 0.0001f, len1 + len2 - 0.0001f);
        Vector2 direction = delta / distance;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float along = (len1 * len1 - len2 * len2 + clamped * clamped) / (2f * clamped);
        float rise = Mathf.Sqrt(Mathf.Max(0f, len1 * len1 - along * along));
        return start + direction * along + perpendicular * (rise * Mathf.Sign(side));
    }

    private void OnDestroy()
    {
        if (basePlateMesh != null)
            Destroy(basePlateMesh);
        if (whiteMaterial != null)
            Destroy(whiteMaterial);
    }
}
