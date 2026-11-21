using UnityEngine;

// The room-frame and the joints move before their graphics and umbilical are updated.
[DefaultExecutionOrder(-50)]
public class OracleArm : MonoBehaviour
{
    [Header("Oracle target")]
    public Transform puppetBody;
    public Vector2 connectionOffset = new Vector2(0f, -0.25f);

    [Header("Rain World room frame")]
    [Tooltip("SSOracleBehavior's standard room frame, expressed in 20 px tiles.")]
    public Vector2Int roomSizeInTiles = new Vector2Int(48, 35);
    public float roomPixelsPerUnit = 100f;
    [Tooltip("Base movement speed in frame-track units per second.")]
    public float baseSlideSpeed = 0.38f;
    public float currentTrackPos = 2.5f;

    [Header("Four original joint lengths (300, 150, 90, 30 px at 100 px/unit)")]
    public float[] segmentLengths = { 3f, 1.5f, 0.9f, 0.3f };
    public float jointGravity = 1.6f;
    public float jointDamping = 2.4f;
    public int inverseKinematicIterations = 18;
    public LayerMask terrainCollisionMask;
    public float jointCollisionRadius = 0.12f;

    // Includes the target point after the four Rain World joints.
    [HideInInspector] public Vector3[] joints;

    private readonly Vector2[] cornerPositions = new Vector2[4];
    private Vector2[] solvedPositions;
    private Vector2[] preSolvePositions;
    private Vector2[] jointVelocities;
    private Camera mainCam;
    private bool initialized;
    private readonly Collider2D[] collisionBuffer = new Collider2D[8];


    private void Start()
    {
        mainCam = Camera.main;
        int linkCount = segmentLengths == null ? 0 : segmentLengths.Length;
        joints = new Vector3[linkCount + 1];
        solvedPositions = new Vector2[linkCount + 1];
        preSolvePositions = new Vector2[linkCount + 1];
        jointVelocities = new Vector2[linkCount + 1];
        RefreshRoomFrame();

        currentTrackPos = ClosestTrackPosition(GetBodyTarget());
        Vector2 root = FramePosition(currentTrackPos);
        Vector2 target = GetBodyTarget();
        InitializeChain(root, target);
        SolveArm(root, target, 0f);
        initialized = true;
    }

    private void Update()
    {
        if (puppetBody == null || segmentLengths == null || segmentLengths.Length == 0)
            return;

        if (mainCam == null)
            mainCam = Camera.main;

        RefreshRoomFrame();
        if (joints == null || joints.Length != segmentLengths.Length + 1)
        {
            joints = new Vector3[segmentLengths.Length + 1];
            solvedPositions = new Vector2[joints.Length];
            preSolvePositions = new Vector2[joints.Length];
            jointVelocities = new Vector2[joints.Length];
            initialized = false;
        }

        Vector2 target = GetBodyTarget();
        float desiredTrack = ClosestTrackPosition(target);
        if (!initialized)
        {
            currentTrackPos = desiredTrack;
            InitializeChain(FramePosition(currentTrackPos), target);
            initialized = true;
        }

        currentTrackPos = MoveOnTrack(currentTrackPos, desiredTrack, baseSlideSpeed * Time.deltaTime);
        Vector2 root = FramePosition(currentTrackPos);
        // The carriage must catch up when a dragged puppet would otherwise pull the
        // rigid arm beyond its reach.
        if (Vector2.Distance(root, target) > TotalLength() - 0.01f)
        {
            currentTrackPos = desiredTrack;
            root = FramePosition(currentTrackPos);
        }
        SolveArm(root, target, Time.deltaTime);
    }

    private void RefreshRoomFrame()
    {
        if (mainCam != null && mainCam.orthographic)
        {
            Vector3 bottomLeft = mainCam.ViewportToWorldPoint(new Vector3(0f, 0f, -mainCam.transform.position.z));
            Vector3 topRight = mainCam.ViewportToWorldPoint(new Vector3(1f, 1f, -mainCam.transform.position.z));
            cornerPositions[0] = new Vector2(bottomLeft.x, topRight.y);
            cornerPositions[1] = new Vector2(topRight.x, topRight.y);
            cornerPositions[2] = new Vector2(topRight.x, bottomLeft.y);
            cornerPositions[3] = new Vector2(bottomLeft.x, bottomLeft.y);
            return;
        }
        float pixelScale = 1f / Mathf.Max(1f, roomPixelsPerUnit);
        float tileSize = 20f * pixelScale;

        Vector2 roomCenter = mainCam != null
            ? (Vector2)mainCam.transform.position
            : Vector2.zero;
        roomCenter.x -= roomSizeInTiles.x * tileSize * 0.5f;
        roomCenter.y -= roomSizeInTiles.y * tileSize * 0.5f;

        // These are the room-tile anchors used by OracleArm's standard SS frame.
        cornerPositions[0] = roomCenter + new Vector2(10.5f, 31.5f) * tileSize;
        cornerPositions[1] = roomCenter + new Vector2(38.5f, 31.5f) * tileSize;
        cornerPositions[2] = roomCenter + new Vector2(38.5f, 3.5f) * tileSize;
        cornerPositions[3] = roomCenter + new Vector2(10.5f, 3.5f) * tileSize;
    }

    private Vector2 GetBodyTarget()
    {
        return (Vector2)puppetBody.position + connectionOffset;
    }

    private void InitializeChain(Vector2 root, Vector2 target)
    {
        if (solvedPositions == null || solvedPositions.Length != segmentLengths.Length + 1)
        {
            solvedPositions = new Vector2[segmentLengths.Length + 1];
            preSolvePositions = new Vector2[segmentLengths.Length + 1];
        }
        if (preSolvePositions == null || preSolvePositions.Length != solvedPositions.Length)
            preSolvePositions = new Vector2[solvedPositions.Length];
        if (jointVelocities == null || jointVelocities.Length != solvedPositions.Length)
            jointVelocities = new Vector2[solvedPositions.Length];

        Vector2 direction = target - root;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;
        direction.Normalize();
        Vector2 bend = new Vector2(-direction.y, direction.x);
        if (Vector2.Dot(bend, Vector2.down) < 0f)
            bend = -bend;

        float totalLength = TotalLength();
        for (int i = 0; i < solvedPositions.Length; i++)
        {
            float t = i / (float)(solvedPositions.Length - 1);
            solvedPositions[i] = Vector2.Lerp(root, target, t) +
                bend * (Mathf.Sin(t * Mathf.PI) * Mathf.Min(0.45f, totalLength * 0.08f));
            jointVelocities[i] = Vector2.zero;
        }

        solvedPositions[0] = root;
        solvedPositions[solvedPositions.Length - 1] = target;
        for (int i = 0; i < joints.Length; i++)
            joints[i] = solvedPositions[i];
    }

    private void SolveArm(Vector2 root, Vector2 target, float dt)
    {
        int pointCount = solvedPositions.Length;
        if (pointCount < 2)
            return;

        dt = Mathf.Clamp(dt, 0f, 1f / 20f);
        for (int i = 0; i < pointCount; i++)
            preSolvePositions[i] = solvedPositions[i];

        for (int i = 1; i < pointCount - 1; i++)
        {
            jointVelocities[i] += Vector2.down * jointGravity * dt;
            jointVelocities[i] *= Mathf.Exp(-jointDamping * dt);
            solvedPositions[i] += jointVelocities[i] * dt;
        }

        float totalLength = TotalLength();
        Vector2 rootToTarget = target - root;
        if (rootToTarget.magnitude >= totalLength)
        {
            Vector2 direction = rootToTarget.sqrMagnitude < 0.0001f ? Vector2.down : rootToTarget.normalized;
            solvedPositions[0] = root;
            for (int i = 0; i < segmentLengths.Length; i++)
                solvedPositions[i + 1] = solvedPositions[i] + direction * Mathf.Max(0.01f, segmentLengths[i]);
        }
        else
        {
            int iterations = Mathf.Max(inverseKinematicIterations, 64);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                solvedPositions[pointCount - 1] = target;
                for (int i = pointCount - 2; i >= 0; i--)
                {
                    Vector2 direction = solvedPositions[i] - solvedPositions[i + 1];
                    if (direction.sqrMagnitude < 0.0001f)
                        direction = Vector2.left;
                    solvedPositions[i] = solvedPositions[i + 1] +
                        direction.normalized * Mathf.Max(0.01f, segmentLengths[i]);
                }

                solvedPositions[0] = root;
                for (int i = 0; i < pointCount - 1; i++)
                {
                    Vector2 direction = solvedPositions[i + 1] - solvedPositions[i];
                    if (direction.sqrMagnitude < 0.0001f)
                        direction = Vector2.down;
                    solvedPositions[i + 1] = solvedPositions[i] +
                        direction.normalized * Mathf.Max(0.01f, segmentLengths[i]);
                }

                for (int i = 1; i < pointCount - 1; i++)
                    solvedPositions[i] = ResolveTerrainCollision(solvedPositions[i]);
                if (Vector2.Distance(solvedPositions[pointCount - 1], target) < 0.0001f)
                    break;
            }

            if (terrainCollisionMask.value == 0 &&
                Vector2.Distance(solvedPositions[pointCount - 1], target) > 0.0001f)
                CloseChain(root, target);
        }

        // The forward pass preserves every rigid link. Forcing the final point to
        // the target here would stretch the last link when the solver has not converged.
        for (int i = 0; i < pointCount; i++)
        {
            if (dt > 0f && i > 0 && i < pointCount - 1)
                jointVelocities[i] = (solvedPositions[i] - preSolvePositions[i]) / dt;
            joints[i] = new Vector3(solvedPositions[i].x, solvedPositions[i].y, 0f);
        }
    }

    private void CloseChain(Vector2 root, Vector2 target)
    {
        Vector2 current = root;
        for (int i = 0; i < segmentLengths.Length - 1; i++)
        {
            float length = Mathf.Max(0.01f, segmentLengths[i]);
            float remaining = 0f;
            float longest = 0f;
            for (int j = i + 1; j < segmentLengths.Length; j++)
            {
                float nextLength = Mathf.Max(0.01f, segmentLengths[j]);
                remaining += nextLength;
                longest = Mathf.Max(longest, nextLength);
            }

            Vector2 preferred = (solvedPositions[i + 1] - current).normalized;
            Vector2 toTarget = target - current;
            float distance = toTarget.magnitude;
            Vector2 direction = preferred.sqrMagnitude > 0f ? preferred : Vector2.down;
            if (distance > 0.00001f)
            {
                // Limit the angle so the following links can still reach the tip.
                float minimum = Mathf.Max(0f, 2f * longest - remaining);
                float lengthSquares = distance * distance + length * length;
                float denominator = 2f * distance * length;
                float minCosine = Mathf.Clamp((lengthSquares - remaining * remaining) / denominator, -1f, 1f);
                float maxCosine = Mathf.Clamp((lengthSquares - minimum * minimum) / denominator, -1f, 1f);
                Vector2 axis = toTarget / distance;
                Vector2 normal = new Vector2(-axis.y, axis.x);
                float cosine = Mathf.Clamp(Vector2.Dot(direction, axis), minCosine, maxCosine);
                float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
                direction = axis * cosine + normal * (sine * Mathf.Sign(Vector2.Dot(direction, normal)));
            }

            current += direction * length;
            solvedPositions[i + 1] = current;
        }

        float lastLength = Mathf.Max(0.01f, segmentLengths[segmentLengths.Length - 1]);
        solvedPositions[solvedPositions.Length - 1] = Vector2.MoveTowards(current, target, lastLength);
    }

    private Vector2 ResolveTerrainCollision(Vector2 point)
    {
        if (terrainCollisionMask.value == 0 || jointCollisionRadius <= 0f)
            return point;

        int hitCount = Physics2D.OverlapCircleNonAlloc(point, jointCollisionRadius,
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
            point = closest + outward.normalized * jointCollisionRadius;
        }
        return point;
    }

    private bool IsPuppetCollider(Collider2D collider)
    {
        if (puppetBody == null)
            return false;
        Transform colliderTransform = collider.transform;
        return colliderTransform == puppetBody || colliderTransform.IsChildOf(puppetBody);
    }

    private float TotalLength()
    {
        float total = 0f;
        for (int i = 0; i < segmentLengths.Length; i++)
            total += Mathf.Max(0.01f, segmentLengths[i]);
        return total;
    }

    private float ClosestTrackPosition(Vector2 position)
    {
        float bestDistance = float.PositiveInfinity;
        float bestTrack = currentTrackPos;
        for (int i = 0; i < cornerPositions.Length; i++)
        {
            Vector2 start = cornerPositions[i];
            Vector2 end = cornerPositions[(i + 1) % cornerPositions.Length];
            Vector2 segment = end - start;
            float t = segment.sqrMagnitude < 0.0001f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(position - start, segment) / segment.sqrMagnitude);
            float distance = (position - (start + segment * t)).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTrack = i + t;
            }
        }
        return Mathf.Repeat(bestTrack, 4f);
    }

    private float MoveOnTrack(float current, float target, float distance)
    {
        float delta = Mathf.DeltaAngle(current * 90f, target * 90f);
        return Mathf.Repeat(current + Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta) / 90f, distance), 4f);
    }

    private Vector2 FramePosition(float trackPosition)
    {
        float track = Mathf.Repeat(trackPosition, 4f);
        int trackEdge = Mathf.FloorToInt(track);
        return Vector2.Lerp(cornerPositions[trackEdge], cornerPositions[(trackEdge + 1) % 4], track - trackEdge);
    }

    public Vector3 ConstrainDragPosition(Vector3 position)
    {
        RefreshRoomFrame();
        Vector2 target = (Vector2)position + connectionOffset;
        Vector2 root = FramePosition(ClosestTrackPosition(target));
        Vector2 delta = target - root;
        float reach = Mathf.Max(0.01f, TotalLength() - 0.02f);
        if (delta.magnitude > reach)
        {
            Vector2 constrained = root + delta.normalized * reach - connectionOffset;
            position.x = constrained.x;
            position.y = constrained.y;
        }
        return position;
    }

    public Vector2 GetBoundaryAnchor()
    {
        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam == null || !mainCam.orthographic)
            return cornerPositions[1];

        Vector2 center = mainCam.transform.position;
        float halfWidth = mainCam.orthographicSize * mainCam.aspect;
        float halfHeight = mainCam.orthographicSize;
        return center + new Vector2(halfWidth * 0.94f, halfHeight);
    }

    public Vector2 GetFramePositionForGraphics(float trackPosition)
    {
        return FramePosition(trackPosition);
    }

    public Vector2 GetArmElbowAnchor()
    {
        if (joints != null && joints.Length > 1)
            return joints[1];
        return transform.position;
    }

    public Vector2 GetSecondJointAnchor()
    {
        if (joints != null && joints.Length > 2)
            return joints[2];
        return GetArmElbowAnchor();
    }
}
