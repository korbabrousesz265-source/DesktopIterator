using UnityEngine;

// Runs after the arm solver and before both cable meshes are updated.
[DefaultExecutionOrder(-40)]
public class OracleUmbilical : MonoBehaviour
{
    [Header("Anchors")]
    [Tooltip("Optional manual anchor. The stock setup uses the upper-right room edge.")]
    public Transform floorAnchor;
    public bool useCameraAnchor = true;
    public OracleArm oracleArm;
    public Transform puppetBody;
    public Transform puppetHead;

    [Header("Main umbilical")]
    public int trunkSegments = 80;
    public int elbowIndex = 60;
    [Tooltip("Minimum rest spacing between samples in world units.")]
    public float trunkSegLen = 0.12f;
    public float trunkGravity = -0.65f;
    [Range(0.9f, 1f)] public float trunkDamping = 0.995f;
    [Range(2, 12)] public int solverIterations = 6;
    public Vector2 puppetConnectionOffset = new Vector2(0f, -0.5f);

    [Header("Fine cords")]
    public int smallCordCount = 14;
    public int smallCordSegments = 20;
    public float smallCordSegLen = 0.1f;
    public float smallCordGravity = -0.45f;
    [Range(0.9f, 1f)] public float smallCordDamping = 0.994f;
    [Tooltip("Assign the room's solid tile layers when the scene has terrain colliders.")]
    public LayerMask terrainCollisionMask;
    public float terrainCollisionRadius = 0.045f;

    [HideInInspector] public Vector2[] trunkPos;
    [HideInInspector] public Vector2[][] smallCordsPos;

    private Vector2[] trunkPrevious;
    private float[] trunkRestLengths;
    private Vector2[][] smallCordsPrevious;
    private float[][] smallCordRestLengths;
    private int[] smallCordAnchorIndices;
    private Vector2[] smallCordEndOffsets;
    private float[] smallCordTotalLengths;
    private readonly Collider2D[] collisionBuffer = new Collider2D[8];
    private Camera mainCam;

    private void Start()
    {
        mainCam = Camera.main;
        trunkSegments = Mathf.Max(3, trunkSegments);
        smallCordSegments = Mathf.Max(2, smallCordSegments);
        smallCordCount = Mathf.Max(0, smallCordCount);
        elbowIndex = Mathf.Clamp(elbowIndex, 1, trunkSegments - 2);
        AllocateState();
        InitializeCords();
    }

    private void Update()
    {
        if (puppetBody == null || trunkPos == null || trunkPos.Length < 3)
            return;
        if (mainCam == null)
            mainCam = Camera.main;

        if (oracleArm == null || oracleArm.joints == null || oracleArm.joints.Length < 2)
            return;

        float frameDelta = Mathf.Min(Time.deltaTime, 1f / 20f);
        int substeps = Mathf.Clamp(Mathf.CeilToInt(frameDelta / (1f / 60f)), 1, 3);
        float step = substeps > 0 ? frameDelta / substeps : 0f;
        for (int i = 0; i < substeps; i++)
            Simulate(step);
    }

    private void AllocateState()
    {
        trunkPos = new Vector2[trunkSegments];
        trunkPrevious = new Vector2[trunkSegments];
        trunkRestLengths = new float[trunkSegments - 1];

        smallCordsPos = new Vector2[smallCordCount][];
        smallCordsPrevious = new Vector2[smallCordCount][];
        smallCordRestLengths = new float[smallCordCount][];
        smallCordAnchorIndices = new int[smallCordCount];
        smallCordEndOffsets = new Vector2[smallCordCount];
        smallCordTotalLengths = new float[smallCordCount];

        for (int cord = 0; cord < smallCordCount; cord++)
        {
            smallCordsPos[cord] = new Vector2[smallCordSegments];
            smallCordsPrevious[cord] = new Vector2[smallCordSegments];
            smallCordRestLengths[cord] = new float[smallCordSegments - 1];

            float spread = smallCordCount <= 1 ? 0.5f : cord / (float)(smallCordCount - 1);
            smallCordAnchorIndices[cord] = Mathf.RoundToInt(
                Mathf.Lerp(elbowIndex + 2, trunkSegments - 2, spread));

            // Stable per-strand variation, matching the source's randomized cord lengths
            // without touching Unity's shared Random state.
            float seed = cord * 2.3999632f;
            float randomLength = Mathf.Repeat(Mathf.Sin(seed + 1.7f) * 43758.5f, 1f);
            float randomAngle = Mathf.Repeat(Mathf.Sin(seed + 4.1f) * 43758.5f, 1f) * Mathf.PI * 2f;
            float randomReach = Mathf.Repeat(Mathf.Sin(seed + 8.3f) * 43758.5f, 1f);
            smallCordTotalLengths[cord] = Mathf.Lerp(0.5f, 2f, Mathf.Pow(randomLength, 1.5f));
            float reach = Mathf.Lerp(0.05f, 0.38f, randomReach);
            smallCordEndOffsets[cord] = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)) * reach;
        }
    }

    private void InitializeCords()
    {
        Vector2 anchor = GetRoomAnchor();
        Vector2 elbow = GetElbowAnchor();
        Vector2 tail = GetBodyTarget();

        for (int i = 0; i < trunkSegments; i++)
        {
            Vector2 start;
            Vector2 end;
            float t;
            if (i <= elbowIndex)
            {
                start = anchor;
                end = elbow;
                t = i / (float)elbowIndex;
            }
            else
            {
                start = elbow;
                end = tail;
                t = (i - elbowIndex) / (float)(trunkSegments - 1 - elbowIndex);
            }

            Vector2 sag = Vector2.down * (0.14f * Mathf.Sin(t * Mathf.PI));
            trunkPos[i] = Vector2.Lerp(start, end, t) + sag;
            trunkPrevious[i] = trunkPos[i];
        }
        SetTrunkPins();
        for (int i = 0; i < trunkRestLengths.Length; i++)
            trunkRestLengths[i] = Mathf.Max(trunkSegLen, Vector2.Distance(trunkPos[i], trunkPos[i + 1]) * 1.035f);

        for (int cord = 0; cord < smallCordCount; cord++)
        {
            Vector2 start = GetSmallCordAnchor(cord);
            Vector2 end = GetSmallCordEnd(cord);
            float slack = Mathf.Lerp(0.08f, 0.3f, Mathf.Repeat(cord * 0.381966f, 1f));
            for (int i = 0; i < smallCordSegments; i++)
            {
                float t = i / (float)(smallCordSegments - 1);
                Vector2 curve = Vector2.down * (slack * Mathf.Sin(t * Mathf.PI));
                smallCordsPos[cord][i] = Vector2.Lerp(start, end, t) + curve;
                smallCordsPrevious[cord][i] = smallCordsPos[cord][i];
            }

            float targetSegmentLength = smallCordTotalLengths[cord] / (smallCordSegments - 1);
            for (int i = 0; i < smallCordRestLengths[cord].Length; i++)
                smallCordRestLengths[cord][i] = Mathf.Max(
                    Mathf.Max(smallCordSegLen, targetSegmentLength),
                    Vector2.Distance(smallCordsPos[cord][i], smallCordsPos[cord][i + 1]) * 1.04f);
        }
    }

    private void Simulate(float dt)
    {
        if (dt <= 0f)
            return;

        Integrate(trunkPos, trunkPrevious, trunkGravity, trunkDamping, dt);
        SetTrunkPins();
        for (int iteration = 0; iteration < solverIterations; iteration++)
        {
            ConstrainSpan(trunkPos, trunkRestLengths, 0, elbowIndex, true, true);
            ConstrainSpan(trunkPos, trunkRestLengths, elbowIndex, trunkSegments - 1, true, true);
            ClampToView(trunkPos, trunkPrevious, 1, elbowIndex - 1);
            ClampToView(trunkPos, trunkPrevious, elbowIndex + 1, trunkSegments - 2);
            SetTrunkPins();
        }
        ResolveTerrain(trunkPos, trunkPrevious, 1, elbowIndex - 1);
        ResolveTerrain(trunkPos, trunkPrevious, elbowIndex + 1, trunkSegments - 2);

        for (int cord = 0; cord < smallCordCount; cord++)
        {
            Integrate(smallCordsPos[cord], smallCordsPrevious[cord], smallCordGravity,
                smallCordDamping, dt);
            SetSmallCordPins(cord);
            for (int iteration = 0; iteration < Mathf.Max(3, solverIterations - 1); iteration++)
            {
                ConstrainSpan(smallCordsPos[cord], smallCordRestLengths[cord], 0,
                    smallCordSegments - 1, true, true);
                ClampToView(smallCordsPos[cord], smallCordsPrevious[cord], 1,
                    smallCordSegments - 2);
                SetSmallCordPins(cord);
            }
            ResolveTerrain(smallCordsPos[cord], smallCordsPrevious[cord], 1,
                smallCordSegments - 2);
        }
    }

    private void Integrate(Vector2[] positions, Vector2[] previous, float gravity, float damping, float dt)
    {
        float friction = Mathf.Pow(damping, dt * 60f);
        Vector2 acceleration = Vector2.up * gravity;
        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 current = positions[i];
            Vector2 velocity = (current - previous[i]) * friction;
            previous[i] = current;
            positions[i] = current + velocity + acceleration * (dt * dt);
        }
    }

    private void ConstrainSpan(Vector2[] positions, float[] restLengths, int first, int last,
        bool pinFirst, bool pinLast)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            for (int step = 0; step < last - first; step++)
            {
                int i = pass == 0 ? first + step : last - 1 - step;
                Vector2 difference = positions[i + 1] - positions[i];
                float distance = difference.magnitude;
                if (distance < 0.00001f)
                    continue;

                float rest = restLengths[i];
                Vector2 correction = difference * ((distance - rest) / distance);
                bool firstPinned = pinFirst && i == first;
                bool secondPinned = pinLast && i + 1 == last;

                if (firstPinned && !secondPinned)
                    positions[i + 1] -= correction;
                else if (!firstPinned && secondPinned)
                    positions[i] += correction;
                else if (!firstPinned && !secondPinned)
                {
                    positions[i] += correction * 0.5f;
                    positions[i + 1] -= correction * 0.5f;
                }
            }
        }
    }

    private void SetTrunkPins()
    {
        trunkPos[0] = GetRoomAnchor();
        trunkPos[elbowIndex] = GetElbowAnchor();
        trunkPos[trunkSegments - 1] = GetBodyTarget();
        trunkPrevious[0] = trunkPos[0];
        trunkPrevious[elbowIndex] = trunkPos[elbowIndex];
        trunkPrevious[trunkSegments - 1] = trunkPos[trunkSegments - 1];
    }

    private void SetSmallCordPins(int cord)
    {
        smallCordsPos[cord][0] = GetSmallCordAnchor(cord);
        smallCordsPos[cord][smallCordSegments - 1] = GetSmallCordEnd(cord);
        smallCordsPrevious[cord][0] = smallCordsPos[cord][0];
        smallCordsPrevious[cord][smallCordSegments - 1] =
            smallCordsPos[cord][smallCordSegments - 1];
    }

    private void ClampToView(Vector2[] positions, Vector2[] previous, int first, int last)
    {
        if (mainCam == null || !mainCam.orthographic || first > last)
            return;

        Vector2 center = mainCam.transform.position;
        float halfWidth = mainCam.orthographicSize * mainCam.aspect - 0.12f;
        float halfHeight = mainCam.orthographicSize - 0.12f;
        for (int i = first; i <= last; i++)
        {
            Vector2 point = positions[i];
            Vector2 clamped = new Vector2(
                Mathf.Clamp(point.x, center.x - halfWidth, center.x + halfWidth),
                Mathf.Clamp(point.y, center.y - halfHeight, center.y + halfHeight));
            if (clamped != point)
            {
                positions[i] = clamped;
                previous[i] = clamped;
            }
        }
    }

    private void ResolveTerrain(Vector2[] positions, Vector2[] previous, int first, int last)
    {
        if (terrainCollisionMask.value == 0 || first > last || terrainCollisionRadius <= 0f)
            return;

        for (int i = first; i <= last; i++)
        {
            Vector2 point = positions[i];
            int hitCount = Physics2D.OverlapCircleNonAlloc(point, terrainCollisionRadius,
                collisionBuffer, terrainCollisionMask);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider2D hit = collisionBuffer[hitIndex];
                if (hit == null || IsPuppetCollider(hit))
                    continue;

                Vector2 closest = hit.ClosestPoint(point);
                Vector2 outward = point - closest;
                if (outward.sqrMagnitude < 0.000001f)
                    outward = point - (Vector2)hit.bounds.center;
                if (outward.sqrMagnitude < 0.000001f)
                    outward = Vector2.up;

                Vector2 corrected = closest + outward.normalized * terrainCollisionRadius;
                positions[i] = corrected;
                previous[i] = corrected;
                point = corrected;
            }
        }
    }

    private bool IsPuppetCollider(Collider2D collider)
    {
        if (puppetBody == null)
            return false;
        Transform colliderTransform = collider.transform;
        return colliderTransform == puppetBody || colliderTransform.IsChildOf(puppetBody);
    }

    private Vector2 GetRoomAnchor()
    {
        if (!useCameraAnchor && floorAnchor != null)
            return floorAnchor.position;
        if (oracleArm != null)
            return oracleArm.GetBoundaryAnchor();
        if (floorAnchor != null)
            return floorAnchor.position;
        return transform.position;
    }

    private Vector2 GetElbowAnchor()
    {
        return oracleArm != null ? oracleArm.GetArmElbowAnchor() : GetRoomAnchor();
    }

    private Vector2 GetBodyTarget()
    {
        return (Vector2)puppetBody.position + puppetConnectionOffset;
    }

    private Vector2 GetSmallCordAnchor(int cord)
    {
        int index = smallCordAnchorIndices[cord];
        index = Mathf.Clamp(index, 0, trunkSegments - 1);
        float t = index;
        int first = Mathf.FloorToInt(t);
        int second = Mathf.Min(first + 1, trunkSegments - 1);
        return Vector2.Lerp(trunkPos[first], trunkPos[second], t - first);
    }

    private Vector2 GetSmallCordEnd(int cord)
    {
        Vector2 head = puppetHead != null
            ? (Vector2)puppetHead.position
            : (Vector2)puppetBody.position + Vector2.up * 0.35f;
        return head + smallCordEndOffsets[cord];
    }
}
