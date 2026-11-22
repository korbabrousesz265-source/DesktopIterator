using UnityEngine;
using UnityEngine.Rendering;

// Recreates the Five Pebbles body layers from OracleGraphics: two body chunks,
// an untextured 11x11 gown mesh, and two seven-segment hand ribbons.
[DefaultExecutionOrder(45)]
public sealed class PuppetBodyGraphics : MonoBehaviour
{
    [Header("Rain World simulation scale")]
    [Min(1f)] public float pixelsPerUnit = 100f;
    [Min(0f)] public float roomGravity = 0f;

    [Header("Sorting")]
    public int armSortingOrder = 0;
    public int robeSortingOrder = -1;

    private const int GownDivisions = 11;
    private const int ArmSections = 7;
    private const int ArmVerticesPerSection = 4;
    private const float SimulationStep = 1f / 40f;
    private const float BodyChunkRadius = 6f;
    private const float BodyChunkSeparation = 9f;

    private static readonly Vector2Int[] NeighborDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1)
    };

    private PuppetAnimator pose;
    private Transform puppetRoot;
    private Material clothMaterial;
    private Mesh robeMesh;
    private Mesh leftArmMesh;
    private Mesh rightArmMesh;
    private SpriteRenderer[] bodyChunks;

    // Rain World's Gown stores [cross-axis, length-axis, last/current/velocity].
    private Vector2[,] robePosition;
    private Vector2[,] robeLastPosition;
    private Vector2[,] robeVelocity;
    private Vector2[,] robeIdealPosition;
    private Vector3[] robeVertices;
    private Color[] robeColors;
    private Vector3[] leftArmVertices;
    private Vector3[] rightArmVertices;
    private Color[] leftArmColors;
    private Color[] rightArmColors;
    private float simulationAccumulator;
    private bool initialized;

    private void Awake()
    {
        pose = GetComponentInParent<PuppetAnimator>();
        if (pose == null || pose.body == null)
            return;

        puppetRoot = pose.transform;
        SpriteRenderer authoredRobe = GetComponent<SpriteRenderer>();
        if (authoredRobe != null)
            authoredRobe.enabled = false;
        pose.DisableLegacySleeves();

        clothMaterial = CreateClothMaterial();
        if (clothMaterial == null)
            return;

        int pointCount = GownDivisions * GownDivisions;
        robePosition = new Vector2[GownDivisions, GownDivisions];
        robeLastPosition = new Vector2[GownDivisions, GownDivisions];
        robeVelocity = new Vector2[GownDivisions, GownDivisions];
        robeIdealPosition = new Vector2[GownDivisions, GownDivisions];
        robeVertices = new Vector3[pointCount];
        robeColors = new Color[pointCount];
        leftArmVertices = new Vector3[ArmSections * ArmVerticesPerSection];
        rightArmVertices = new Vector3[leftArmVertices.Length];
        leftArmColors = new Color[leftArmVertices.Length];
        rightArmColors = new Color[rightArmVertices.Length];

        robeMesh = CreateMeshLayer("Five Pebbles gown mesh", robeSortingOrder,
            robeVertices, robeColors, BuildGownTriangles());
        leftArmMesh = CreateMeshLayer("Five Pebbles left arm mesh", armSortingOrder,
            leftArmVertices, leftArmColors, BuildArmTriangles());
        rightArmMesh = CreateMeshLayer("Five Pebbles right arm mesh", armSortingOrder,
            rightArmVertices, rightArmColors, BuildArmTriangles());

        CreateBodyChunkSprites();
        SetSourceVertexColors();
    }

    private void LateUpdate()
    {
        if (pose == null || puppetRoot == null || robeMesh == null)
            return;

        GetBodyBasis(out Vector2 bodyPosition, out Vector2 bodyDirection, out Vector2 perpendicular);
        Vector2 lowerBodyPosition = bodyPosition - bodyDirection * Pixel(BodyChunkSeparation);
        ComputeGownIdealPositions(bodyPosition, bodyDirection, perpendicular);
        UpdateBodyChunkSprites(bodyPosition, lowerBodyPosition);

        if (!initialized)
        {
            for (int cross = 0; cross < GownDivisions; cross++)
            for (int length = 0; length < GownDivisions; length++)
            {
                Vector2 ideal = robeIdealPosition[cross, length];
                robePosition[cross, length] = ideal;
                robeLastPosition[cross, length] = ideal;
                robeVelocity[cross, length] = Vector2.zero;
            }
            initialized = true;
        }
        else
        {
            simulationAccumulator = Mathf.Min(
                simulationAccumulator + Mathf.Min(Time.unscaledDeltaTime, 0.05f), 0.1f);
            while (simulationAccumulator >= SimulationStep)
            {
                SimulateGown(bodyDirection, lowerBodyPosition);
                simulationAccumulator -= SimulationStep;
            }
        }

        UpdateGownMesh();
        UpdateArmMesh(leftArmMesh, leftArmVertices, leftArmColors,
            pose.HandLeft, bodyPosition, bodyDirection, perpendicular, 0);
        UpdateArmMesh(rightArmMesh, rightArmVertices, rightArmColors,
            pose.HandRight, bodyPosition, bodyDirection, perpendicular, 1);
    }

    private void ComputeGownIdealPositions(Vector2 bodyPosition, Vector2 direction, Vector2 perpendicular)
    {
        for (int cross = 0; cross < GownDivisions; cross++)
        {
            float crossT = cross / (float)(GownDivisions - 1);
            for (int length = 0; length < GownDivisions; length++)
            {
                float lengthT = length / (float)(GownDivisions - 1);
                float halfWidth = Mathf.Lerp(5f, 11f, lengthT);
                float axisOffset = Mathf.Lerp(5f, -18f, lengthT) *
                    (1f + Mathf.Sin(Mathf.PI * crossT) * 0.35f * Mathf.Lerp(-1f, 1f, lengthT));

                robeIdealPosition[cross, length] = bodyPosition +
                    perpendicular * (Mathf.Lerp(-1f, 1f, crossT) * Pixel(halfWidth)) +
                    direction * Pixel(axisOffset);
            }
        }
    }

    private void SimulateGown(Vector2 direction, Vector2 lowerBodyPosition)
    {
        for (int cross = 0; cross < GownDivisions; cross++)
        for (int length = 0; length < GownDivisions; length++)
        {
            Vector2 current = robePosition[cross, length];
            Vector2 velocity = robeVelocity[cross, length];
            Vector2 ideal = robeIdealPosition[cross, length];
            float t = length / (float)(GownDivisions - 1);

            robeLastPosition[cross, length] = current;
            current += velocity;
            velocity *= 0.999f;
            velocity.y -= Pixel(0.9f * roomGravity);

            Vector2 towardIdeal = SafeDirection(lowerBodyPosition, ideal);
            Vector3 guidedDirection = Vector3.Slerp(
                new Vector3(-direction.x, -direction.y, 0f),
                new Vector3(towardIdeal.x, towardIdeal.y, 0f), t);
            velocity += new Vector2(guidedDirection.x, guidedDirection.y) * Pixel(0.02f);

            float distanceToIdeal = Vector2.Distance(current, ideal);
            float maxDistance = Pixel(9f * t);
            Vector2 directionToIdeal = SafeDirection(current, ideal);
            if (distanceToIdeal > maxDistance)
            {
                float correction = maxDistance - distanceToIdeal;
                current -= correction * directionToIdeal;
                velocity -= correction * directionToIdeal;
            }

            robePosition[cross, length] = current;
            robeVelocity[cross, length] = velocity;

            // OracleGraphics relaxes the four cardinal neighbours by changing
            // velocity only; it does not run a spring or move the mesh points here.
            for (int k = 0; k < NeighborDirections.Length; k++)
            {
                Vector2Int offset = NeighborDirections[k];
                int neighborCross = cross + offset.x;
                int neighborLength = length + offset.y;
                if (neighborCross < 0 || neighborLength < 0 ||
                    neighborCross >= GownDivisions || neighborLength >= GownDivisions)
                    continue;

                Vector2 neighbor = robePosition[neighborCross, neighborLength];
                float actualDistance = Vector2.Distance(current, neighbor);
                Vector2 between = SafeDirection(current, neighbor);
                float idealDistance = Vector2.Distance(ideal,
                    robeIdealPosition[neighborCross, neighborLength]);
                Vector2 correctionVector = between * ((idealDistance - actualDistance) * 0.05f);
                robeVelocity[cross, length] -= correctionVector;
                robeVelocity[neighborCross, neighborLength] += correctionVector;
            }
        }
    }

    private void UpdateGownMesh()
    {
        int index = 0;
        float timeStacker = Mathf.Clamp01(simulationAccumulator / SimulationStep);
        for (int cross = 0; cross < GownDivisions; cross++)
        for (int length = 0; length < GownDivisions; length++)
        {
            Vector2 point = Vector2.Lerp(robeLastPosition[cross, length],
                robePosition[cross, length], timeStacker);
            robeVertices[index++] = ToRootLocal(point);
        }

        robeMesh.vertices = robeVertices;
        robeMesh.RecalculateBounds();
    }

    private void UpdateArmMesh(Mesh mesh, Vector3[] vertices, Color[] colors,
        Transform hand, Vector2 bodyPosition, Vector2 direction, Vector2 perpendicular, int side)
    {
        if (mesh == null || hand == null)
            return;

        float sideSign = side == 1 ? -1f : 1f;
        Vector2 start = bodyPosition + perpendicular * Pixel(4f * sideSign);
        Vector2 handPosition = hand.position;
        Vector2 handToStart = SafeDirection(handPosition, start);
        Vector2 controlB = handPosition + handToStart * Pixel(3f) + direction * Pixel(1f);
        Vector2 controlA = start + perpendicular * Pixel(5f * sideSign);
        Vector2 previous = start - perpendicular * Pixel(2f * sideSign);
        float width = Pixel(4f);

        // Rain World's HSL palette is authored in sRGB. Mesh.colors are read
        // as linear vertex data in this project's Linear color space.
        Color highlightSide = FivePebblesGownColor(0.4f).linear;
        Color shadowSide = FivePebblesGownColor(0f).linear;
        for (int section = 0; section < ArmSections; section++)
        {
            float t = section / (float)(ArmSections - 1);
            Vector2 point = CubicBezier(start, controlA, handPosition, controlB, t);
            Vector2 tangent = SafeDirection(previous, point);
            Vector2 sideVector = Perpendicular(tangent) * (side == 0 ? -1f : 1f);
            float rearward = Vector2.Distance(previous, point) * 0.3f;
            int vertex = section * ArmVerticesPerSection;

            vertices[vertex] = ToRootLocal(point - tangent * rearward - sideVector * width);
            vertices[vertex + 1] = ToRootLocal(point - tangent * rearward + sideVector * width);
            vertices[vertex + 2] = ToRootLocal(point - sideVector * width);
            vertices[vertex + 3] = ToRootLocal(point + sideVector * width);
            colors[vertex] = highlightSide;
            colors[vertex + 1] = shadowSide;
            colors[vertex + 2] = highlightSide;
            colors[vertex + 3] = shadowSide;
            previous = point;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    private void CreateBodyChunkSprites()
    {
        Sprite circle = Resources.Load<Sprite>("IteratorArm/Circle20");
        if (circle == null)
            return;

        bodyChunks = new SpriteRenderer[2];
        Color skin = new Color(1f, 0.4f, 203f / 255f, 1f);
        for (int i = 0; i < bodyChunks.Length; i++)
        {
            GameObject chunk = new GameObject("Five Pebbles body chunk " + i);
            chunk.transform.SetParent(puppetRoot, false);
            chunk.transform.localScale = Vector3.one * (BodyChunkRadius / 10f);
            SpriteRenderer renderer = chunk.AddComponent<SpriteRenderer>();
            renderer.sprite = circle;
            renderer.color = skin;
            renderer.sortingOrder = robeSortingOrder - 2;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            bodyChunks[i] = renderer;
        }
    }

    private void UpdateBodyChunkSprites(Vector2 firstChunk, Vector2 secondChunk)
    {
        if (bodyChunks == null)
            return;
        bodyChunks[0].transform.position = new Vector3(firstChunk.x, firstChunk.y, puppetRoot.position.z);
        bodyChunks[1].transform.position = new Vector3(secondChunk.x, secondChunk.y, puppetRoot.position.z);
    }

    private void SetSourceVertexColors()
    {
        for (int cross = 0; cross < GownDivisions; cross++)
        for (int length = 0; length < GownDivisions; length++)
        {
            // Gown.InitiateSprite writes Color(cross) through a transposed mesh
            // index; DrawSprite then writes points in cross-major order. The
            // visible Rain World gradient is therefore along the cloth length.
            robeColors[cross * GownDivisions + length] =
                FivePebblesGownColor(length / (float)(GownDivisions - 1)).linear;
        }
        robeMesh.colors = robeColors;
    }

    private void GetBodyBasis(out Vector2 bodyPosition, out Vector2 direction, out Vector2 perpendicular)
    {
        bodyPosition = pose.body.position;
        direction = pose.head != null ? (Vector2)pose.head.position - bodyPosition : (Vector2)puppetRoot.up;
        if (direction.sqrMagnitude < 1e-8f)
            direction = puppetRoot.up;
        direction.Normalize();
        perpendicular = Perpendicular(direction);
    }

    private float Pixel(float value) => value / Mathf.Max(1f, pixelsPerUnit);

    private Vector3 ToRootLocal(Vector2 point)
    {
        return puppetRoot.InverseTransformPoint(new Vector3(point.x, point.y, puppetRoot.position.z));
    }

    private static Vector2 SafeDirection(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        return delta.sqrMagnitude > 1e-10f ? delta.normalized : Vector2.zero;
    }

    private static Vector2 Perpendicular(Vector2 direction) => new Vector2(-direction.y, direction.x);

    private static Vector2 CubicBezier(Vector2 start, Vector2 controlA, Vector2 end, Vector2 controlB, float t)
    {
        // Rain World's Custom.Bezier argument order is (start, controlA, end, controlB).
        Vector2 betweenControls = Vector2.Lerp(controlA, controlB, t);
        Vector2 startToControl = Vector2.Lerp(start, controlA, t);
        Vector2 controlToEnd = Vector2.Lerp(controlB, end, t);
        startToControl = Vector2.Lerp(startToControl, betweenControls, t);
        controlToEnd = Vector2.Lerp(betweenControls, controlToEnd, t);
        return Vector2.Lerp(startToControl, controlToEnd, t);
    }

    private static Color FivePebblesGownColor(float position)
    {
        float t = Mathf.Clamp01(position);
        return HslToRgb(Mathf.Lerp(0.08f, 0.02f, t * t),
            Mathf.Lerp(1f, 0.8f, t), 0.5f);
    }

    private static Color HslToRgb(float hue, float saturation, float lightness)
    {
        hue = Mathf.Repeat(hue, 1f);
        if (saturation <= 0f)
            return new Color(lightness, lightness, lightness, 1f);

        float q = lightness < 0.5f
            ? lightness * (1f + saturation)
            : lightness + saturation - lightness * saturation;
        float p = 2f * lightness - q;
        return new Color(HueToRgb(p, q, hue + 1f / 3f),
            HueToRgb(p, q, hue), HueToRgb(p, q, hue - 1f / 3f), 1f);
    }

    private static float HueToRgb(float p, float q, float t)
    {
        if (t < 0f) t += 1f;
        if (t > 1f) t -= 1f;
        if (t < 1f / 6f) return p + (q - p) * 6f * t;
        if (t < 1f / 2f) return q;
        if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
        return p;
    }

    private Material CreateClothMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "Five Pebbles untextured gown and arm material",
            hideFlags = HideFlags.DontSave
        };
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        return material;
    }

    private Mesh CreateMeshLayer(string label, int sortingOrder, Vector3[] vertices,
        Color[] colors, int[] triangles)
    {
        GameObject layer = new GameObject(label);
        layer.transform.SetParent(puppetRoot, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale = Vector3.one;

        MeshFilter filter = layer.AddComponent<MeshFilter>();
        MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = clothMaterial;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Mesh mesh = new Mesh { name = label + " mesh" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        return mesh;
    }

    private static int[] BuildGownTriangles()
    {
        int quadsPerSide = GownDivisions - 1;
        int[] triangles = new int[quadsPerSide * quadsPerSide * 6];
        int index = 0;
        for (int cross = 0; cross < quadsPerSide; cross++)
        for (int length = 0; length < quadsPerSide; length++)
        {
            int a = cross * GownDivisions + length;
            int alongLength = a + 1;
            int nextCross = (cross + 1) * GownDivisions + length;
            int farCorner = nextCross + 1;
            triangles[index++] = a;
            triangles[index++] = alongLength;
            triangles[index++] = nextCross;
            triangles[index++] = alongLength;
            triangles[index++] = nextCross;
            triangles[index++] = farCorner;
        }
        return triangles;
    }

    private static int[] BuildArmTriangles()
    {
        int triangleCount = (ArmSections - 1) * 4 + 2;
        int[] triangles = new int[triangleCount * 3];
        int index = 0;
        for (int section = 0; section < ArmSections - 1; section++)
        {
            int start = section * ArmVerticesPerSection;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                triangles[index++] = start + vertex;
                triangles[index++] = start + vertex + 1;
                triangles[index++] = start + vertex + 2;
            }
        }
        int tip = (ArmSections - 1) * ArmVerticesPerSection;
        triangles[index++] = tip;
        triangles[index++] = tip + 1;
        triangles[index++] = tip + 2;
        triangles[index++] = tip + 1;
        triangles[index++] = tip + 2;
        triangles[index] = tip + 3;
        return triangles;
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(robeMesh);
        DestroyRuntimeObject(leftArmMesh);
        DestroyRuntimeObject(rightArmMesh);
        DestroyRuntimeObject(clothMaterial);
    }

    private static void DestroyRuntimeObject(Object value)
    {
        if (value == null)
            return;
        if (Application.isPlaying)
            Destroy(value);
        else
            DestroyImmediate(value);
    }
}
