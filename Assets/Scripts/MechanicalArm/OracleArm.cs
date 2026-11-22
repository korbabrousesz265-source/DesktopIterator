using UnityEngine;

// Read the animated torso in Update before the arm and cable render in LateUpdate.
[DefaultExecutionOrder(10)]
public class OracleArm : MonoBehaviour
{
    [Header("Oracle target")]
    public Transform puppetBody;
    [SerializeField] private Transform backAttachment;
    [Tooltip("World offset from the puppet root when there is no back attachment.")]
    public Vector2 connectionOffset = new Vector2(0f, -0.17f);
    public Vector2 AttachmentPosition => backAttachment != null
        ? (Vector2)backAttachment.position
        : (Vector2)puppetBody.position + connectionOffset;

    [Header("Rain World room frame")]
    [Tooltip("SSOracleBehavior's standard room frame, expressed in 20 px tiles.")]
    public Vector2Int roomSizeInTiles = new Vector2Int(48, 35);
    public float roomPixelsPerUnit = 100f;
    [Tooltip("Carriage speed along the edge in world units per second.")]
    [Min(0.1f)] public float carriageSpeed = 9f;
    [SerializeField, Min(0f)] private float carriagePivotInset = 0.27f;
    public float currentTrackPos = 2.5f;
    public Vector2 CarriageContact => FramePosition(currentTrackPos);

    [Header("Extended span lengths")]
    public float[] segmentLengths = { 3f, 1.5f, 0.9f, 0.3f };
    public float jointGravity = 1.6f;
    public float jointDamping = 5.2f;
    public int inverseKinematicIterations = 18;
    public LayerMask terrainCollisionMask;
    public float jointCollisionRadius = 0.12f;

    // The carriage pivot, three animated joints and the puppet socket.
    [HideInInspector] public Vector3[] joints;
    public Vector2[] Elbows { get; private set; }

    private readonly Vector2[] cornerPositions = new Vector2[4];
    private Vector2[] solvedPositions;
    private Vector2[] preSolvePositions;
    private Vector2[] jointVelocities;
    private float[] spanLengths;
    private Camera mainCam;
    private bool initialized;
    private bool carriageMoving;
    private float carriageGoal;
    private float candidateTrack;
    private float candidateAge;
    private Vector3? dragTarget;
    private readonly Collider2D[] collisionBuffer = new Collider2D[8];

    private const float BaseGoalDwell = 0.18f;
    private const float BaseGoalTolerance = 0.30f;
    private const float BaseStartDistance = 1.45f;
    private const float BaseStopDistance = 0.22f;
    private const float FoldedSpanRatio = 0.74f;
    private const float ExtensionSlack = 0.30f;

    private void Start()
    {
        mainCam = Camera.main;
        if (puppetBody == null || segmentLengths == null || segmentLengths.Length == 0)
            return;

        AllocateChain();
        RefreshRoomFrame();

        currentTrackPos = ClosestTrackPosition(AttachmentPosition);
        candidateTrack = currentTrackPos;
        carriageGoal = currentTrackPos;
        Vector2 root = CarriagePivot(currentTrackPos);
        Vector2 target = AttachmentPosition;
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
            AllocateChain();
            initialized = false;
        }

        Vector2 target = AttachmentPosition;
        if (!initialized)
        {
            currentTrackPos = ClosestTrackPosition(target);
            candidateTrack = currentTrackPos;
            carriageGoal = currentTrackPos;
            candidateAge = 0f;
            carriageMoving = false;
            InitializeChain(CarriagePivot(currentTrackPos), target);
            initialized = true;
        }

        Vector3? drag = dragTarget;
        dragTarget = null;
        // Preserve the current torso pose when translating the puppet to the drag target.
        Vector2 attachmentOffset = target - (Vector2)puppetBody.position;
        Vector2 requestedTip = drag.HasValue ? (Vector2)drag.Value + attachmentOffset : target;
        float maxReach = Mathf.Max(0.01f, TotalLength() - 0.018f);
        float minReach = Mathf.Min(maxReach, MinimumLength() + 0.005f);
        Vector2 root = MoveCarriage(requestedTip, maxReach, minReach, drag.HasValue);
        if (drag.HasValue)
        {
            target = LimitReach(root, requestedTip, minReach, maxReach);
            Vector2 bodyPosition = target - attachmentOffset;
            puppetBody.position = new Vector3(bodyPosition.x, bodyPosition.y, drag.Value.z);
        }
        SolveArm(root, target, Time.deltaTime);
    }

    private void AllocateChain()
    {
        int linkCount = segmentLengths.Length;
        joints = new Vector3[linkCount + 1];
        solvedPositions = new Vector2[linkCount + 1];
        preSolvePositions = new Vector2[linkCount + 1];
        jointVelocities = new Vector2[linkCount + 1];
        spanLengths = new float[linkCount];
        Elbows = new Vector2[linkCount];
    }

    private Vector2 MoveCarriage(Vector2 target, float maxReach, float minReach, bool dragging)
    {
        Vector2 root = CarriagePivot(currentTrackPos);
        float distance = Vector2.Distance(root, target);
        float goal = ClosestTrackPosition(target);

        if (Mathf.Abs(TrackDelta(candidateTrack, goal)) > BaseGoalTolerance)
        {
            candidateTrack = goal;
            candidateAge = 0f;
        }
        else
        {
            candidateAge = Mathf.Min(BaseGoalDwell, candidateAge + Time.deltaTime);
        }

        // Hold the base during small pose changes or a flick across the rail.
        // At full reach the carriage can start immediately to follow a held drag.
        bool atReachLimit = dragging && distance > maxReach;
        bool goalIsStable = candidateAge >= BaseGoalDwell;
        if (atReachLimit)
        {
            carriageGoal = goal;
            carriageMoving = true;
        }
        else if (goalIsStable)
        {
            float threshold = carriageMoving ? BaseStopDistance : BaseStartDistance;
            carriageMoving = Mathf.Abs(TrackDelta(currentTrackPos, goal)) > threshold;
            if (carriageMoving)
                carriageGoal = goal;
        }

        if (!dragging && distance > maxReach + 0.03f)
        {
            // The viewport or the puppet may be repositioned from outside the
            // drag loop. Recover the attachment in the new frame.
            currentTrackPos = goal;
            carriageGoal = goal;
            carriageMoving = false;
            return CarriagePivot(goal);
        }

        if (!carriageMoving)
            return root;

        float nextTrack = MoveOnTrack(currentTrackPos, carriageGoal, carriageSpeed * Time.deltaTime);
        Vector2 nextRoot = CarriagePivot(nextTrack);
        float nextDistance = Vector2.Distance(nextRoot, target);
        if (!dragging && (nextDistance > maxReach + 0.001f || nextDistance < minReach))
        {
            carriageMoving = false;
            return root;
        }
        currentTrackPos = nextTrack;
        if (Mathf.Abs(TrackDelta(currentTrackPos, carriageGoal)) < 0.001f)
            carriageMoving = false;
        return nextRoot;
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

    private void InitializeChain(Vector2 root, Vector2 target)
    {
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

        Vector2 rootToTarget = target - root;
        float totalLength = PrepareSpanLengths(rootToTarget.magnitude);
        if (rootToTarget.magnitude >= totalLength)
        {
            Vector2 direction = rootToTarget.sqrMagnitude < 0.0001f ? Vector2.down : rootToTarget.normalized;
            solvedPositions[0] = root;
            for (int i = 0; i < segmentLengths.Length; i++)
                solvedPositions[i + 1] = solvedPositions[i] + direction * spanLengths[i];
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
                        direction.normalized * spanLengths[i];
                }

                solvedPositions[0] = root;
                for (int i = 0; i < pointCount - 1; i++)
                {
                    Vector2 direction = solvedPositions[i + 1] - solvedPositions[i];
                    if (direction.sqrMagnitude < 0.0001f)
                        direction = Vector2.down;
                    solvedPositions[i + 1] = solvedPositions[i] +
                        direction.normalized * spanLengths[i];
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

        for (int i = 0; i < pointCount; i++)
        {
            if (dt > 0f && i > 0 && i < pointCount - 1)
                jointVelocities[i] = Vector2.ClampMagnitude((solvedPositions[i] - preSolvePositions[i]) / dt, 8f);
            joints[i] = new Vector3(solvedPositions[i].x, solvedPositions[i].y, 0f);
        }
        for (int i = 0; i < Elbows.Length; i++)
            Elbows[i] = SolveElbow(i, solvedPositions[i], solvedPositions[i + 1]);
    }

    private float PrepareSpanLengths(float distance)
    {
        float extended = TotalLength();
        float travel = Mathf.Clamp(distance + ExtensionSlack, extended * FoldedSpanRatio, extended);
        if (spanLengths.Length == 1)
            travel = Mathf.Clamp(distance, 0.001f, extended);

        // The solver joins the main pivots by chords. The two pieces between
        // each pair of pivots keep their lengths as their shared elbow folds.
        float ratio = travel / extended;
        for (int i = 0; i < spanLengths.Length; i++)
            spanLengths[i] = Mathf.Max(0.01f, segmentLengths[i]) * ratio;
        return travel;
    }

    private float GetFirstPieceLength(int span)
    {
        float proportion = span == segmentLengths.Length - 1 ? 0.5f : 2f / 3f;
        return Mathf.Max(0.01f, segmentLengths[span]) * proportion;
    }

    private Vector2 SolveElbow(int span, Vector2 start, Vector2 end)
    {
        float firstLength = GetFirstPieceLength(span);
        float secondLength = Mathf.Max(0.01f, segmentLengths[span]) - firstLength;
        Vector2 chord = end - start;
        float distance = chord.magnitude;
        if (distance < 0.00001f)
            return start + Vector2.up * firstLength;

        Vector2 axis = chord / distance;
        Vector2 normal = new Vector2(-axis.y, axis.x);
        float along = (firstLength * firstLength - secondLength * secondLength + distance * distance)
            / (2f * distance);
        along = Mathf.Clamp(along, -firstLength, firstLength);
        float rise = Mathf.Sqrt(Mathf.Max(0f, firstLength * firstLength - along * along));
        return start + axis * along + normal * ((span % 2 == 0 ? 1f : -1f) * rise);
    }

    private void CloseChain(Vector2 root, Vector2 target)
    {
        Vector2 current = root;
        for (int i = 0; i < segmentLengths.Length - 1; i++)
        {
            float length = spanLengths[i];
            float remaining = 0f;
            float longest = 0f;
            for (int j = i + 1; j < segmentLengths.Length; j++)
            {
                float nextLength = spanLengths[j];
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

        float lastLength = spanLengths[spanLengths.Length - 1];
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

    private float MinimumLength()
    {
        if (segmentLengths.Length == 1)
            return 0f;

        float longest = 0f;
        foreach (float length in segmentLengths)
            longest = Mathf.Max(longest, length);
        return Mathf.Max(0f, (2f * longest - TotalLength()) * FoldedSpanRatio);
    }

    private static Vector2 LimitReach(Vector2 root, Vector2 target, float minimum, float maximum)
    {
        Vector2 delta = target - root;
        float distance = delta.magnitude;
        if (distance >= minimum && distance <= maximum)
            return target;
        Vector2 direction = distance > 0.00001f ? delta / distance : Vector2.up;
        return root + direction * Mathf.Clamp(distance, minimum, maximum);
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
        float delta = TrackDelta(current, target);
        return TrackAtDistance(DistanceOnTrack(current) + Mathf.Clamp(delta, -distance, distance));
    }

    private float TrackDelta(float from, float to)
    {
        float perimeter = TrackLength();
        return Mathf.Repeat(DistanceOnTrack(to) - DistanceOnTrack(from) + perimeter * 0.5f, perimeter)
            - perimeter * 0.5f;
    }

    private float EdgeLength(int edge)
    {
        return Vector2.Distance(cornerPositions[edge], cornerPositions[(edge + 1) % 4]);
    }

    private float TrackLength()
    {
        return Mathf.Max(0.001f, 2f * (EdgeLength(0) + EdgeLength(1)));
    }

    private float DistanceOnTrack(float track)
    {
        track = Mathf.Repeat(track, 4f);
        int edge = Mathf.FloorToInt(track);
        float distance = (track - edge) * EdgeLength(edge);
        for (int i = 0; i < edge; i++)
            distance += EdgeLength(i);
        return distance;
    }

    private float TrackAtDistance(float distance)
    {
        distance = Mathf.Repeat(distance, TrackLength());
        for (int edge = 0; edge < 4; edge++)
        {
            float edgeLength = EdgeLength(edge);
            if (distance < edgeLength)
                return edge + distance / edgeLength;
            distance -= edgeLength;
        }
        return 0f;
    }

    private Vector2 FramePosition(float trackPosition)
    {
        float track = Mathf.Repeat(trackPosition, 4f);
        int trackEdge = Mathf.FloorToInt(track);
        return Vector2.Lerp(cornerPositions[trackEdge], cornerPositions[(trackEdge + 1) % 4], track - trackEdge);
    }

    public void RequestDragPosition(Vector3 position)
    {
        dragTarget = position;
    }

    public Vector2 GetCarriageTangent() => CarriageTangent(currentTrackPos);

    private Vector2 CarriagePivot(float track)
    {
        Vector2 tangent = CarriageTangent(track);
        Vector2 inward = new Vector2(tangent.y, -tangent.x);
        return FramePosition(track) + inward * carriagePivotInset;
    }

    private Vector2 CarriageTangent(float track)
    {
        // Blend the carriage orientation while its rail contact follows the corner.
        const float blendDistance = 0.42f;
        float distance = DistanceOnTrack(track);
        Vector2 before = FramePosition(TrackAtDistance(distance - blendDistance));
        Vector2 after = FramePosition(TrackAtDistance(distance + blendDistance));
        return (after - before).normalized;
    }

    public Vector2 GetArmElbowAnchor()
    {
        // The existing trunk guide is tied to the first joint after the base.
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
