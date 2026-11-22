using UnityEngine;

[DefaultExecutionOrder(20)]
public class OracleUmbilical : MonoBehaviour
{
    public Transform floorAnchor;
    public bool useCameraAnchor = true;
    [Range(0f, 1f)] public float floorViewportX = 0.52f;
    public OracleArm oracleArm;
    public Transform puppetBody;
    public Transform puppetHead;

    [Min(3)] public int trunkSegments = 80;
    [Min(1)] public int elbowIndex = 60;
    [Min(0)] public int smallCordCount = 5;
    [Min(2)] public int smallCordSegments = 20;
    [Tooltip("Rest length as a multiple of the viewport reach. Rain World's 790 px cord in a " +
             "1200 x 800 room is about 0.65 of that diagonal; the desktop value is larger so a " +
             "dragged puppet can always reach the far corner.")]
    [Min(0.2f)] public float slackFactor = 1.07f;

    [HideInInspector] public Vector2[] trunkPos;
    [HideInInspector] public Vector2[][] smallCordsPos;

    private const float StepSeconds = 1f / 120f;
    private const int SolverIterations = 36;
    private const int SeedSamples = 192;
    private const float ReceiverLeash = 0.23f;
    private Vector2[] previousPositions;
    private readonly Vector2[] seedPoints = new Vector2[SeedSamples + 1];
    private readonly float[] seedDistances = new float[SeedSamples + 1];
    private Camera view;
    private Rect lastBounds;
    private Vector2 lastMount;
    private Vector2 lastGuide;
    private Vector2 lastReceiver;
    private float receiverSide;
    private float segmentLength;
    private float accumulatedTime;
    private bool initialized;

    private void Start()
    {
        trunkPos = new Vector2[Mathf.Max(3, trunkSegments)];
        previousPositions = new Vector2[trunkPos.Length];
        view = Camera.main;
        smallCordsPos = new Vector2[Mathf.Max(0, smallCordCount)][];
        for (int i = 0; i < smallCordsPos.Length; i++)
            smallCordsPos[i] = new Vector2[Mathf.Max(2, smallCordSegments)];
    }

    private void LateUpdate()
    {
        if (puppetBody == null || oracleArm == null ||
            oracleArm.joints == null || oracleArm.joints.Length < 2)
            return;

        if (view == null)
            view = Camera.main;

        Rect bounds = VisibleBounds();
        bool screenMount = useCameraAnchor || floorAnchor == null;
        Vector2 mount = screenMount
            ? new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, floorViewportX), bounds.yMin)
            : (Vector2)floorAnchor.position;
        Vector2 guidePoint = ArmGuide();
        Vector2 head = puppetHead != null
            ? (Vector2)puppetHead.position : (Vector2)puppetBody.position + Vector2.up * 0.35f;

        int last = trunkPos.Length - 1;
        int guide = Mathf.Clamp(elbowIndex, 1, last - 1);
        int outlet = screenMount ? Mathf.Min(2, guide - 1) : 0;
        if (!initialized)
            receiverSide = guidePoint.x > head.x ? 1f : -1f;
        Vector2 receiver = ReceiverNearHead(head, bounds);
        if (!initialized || bounds != lastBounds || (mount - lastMount).sqrMagnitude > 0.04f)
        {
            // Size once for the viewport so the full rail remains usable and
            // every centimetre of slack has to fold when the arm comes closer.
            float farX = Mathf.Max(Mathf.Abs(bounds.xMin - mount.x), Mathf.Abs(bounds.xMax - mount.x));
            float farY = Mathf.Max(Mathf.Abs(bounds.yMin - mount.y), Mathf.Abs(bounds.yMax - mount.y));
            float reach = new Vector2(farX, farY).magnitude;
            segmentLength = Mathf.Max(reach * slackFactor / guide,
                Vector2.Distance(guidePoint, receiver) * 1.15f / (last - guide));
            for (int i = 0; i < outlet; i++)
                trunkPos[i] = mount + Vector2.up * (i * segmentLength);
            SeedSpan(outlet, guide, mount + Vector2.up * (outlet * segmentLength), guidePoint, bounds);
            SeedSpan(guide, last, guidePoint, receiver, bounds);
            trunkPos.CopyTo(previousPositions, 0);
            lastGuide = guidePoint;
            lastReceiver = receiver;
            lastMount = mount;
            lastBounds = bounds;
            initialized = true;
        }

        accumulatedTime += Mathf.Min(0.05f, Time.deltaTime);
        int steps = Mathf.FloorToInt(accumulatedTime / StepSeconds);
        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            Simulate(outlet, guide, mount, Vector2.Lerp(lastGuide, guidePoint, t),
                Vector2.Lerp(lastReceiver, receiver, t), bounds);
        }
        if (steps > 0)
        {
            accumulatedTime -= steps * StepSeconds;
            lastGuide = guidePoint;
            lastReceiver = receiver;
        }
        trunkPos[0] = mount;
        trunkPos[guide] = guidePoint;

        // Keep the thin lines on the moving end of the trunk near the head.
        for (int cord = 0; cord < smallCordsPos.Length; cord++)
        {
            float spread = smallCordsPos.Length <= 1 ? 0.5f : cord / (float)(smallCordsPos.Length - 1);
            Vector2 start = Vector2.Lerp(trunkPos[last - 1], trunkPos[last], spread);
            Vector2 end = head + new Vector2(Mathf.Lerp(-0.035f, 0.035f, spread), -0.02f);
            Vector2 bow = new Vector2(0f, Mathf.Lerp(-0.09f, 0.12f, spread));

            Vector2[] points = smallCordsPos[cord];
            for (int i = 0; i < points.Length; i++)
                points[i] = SampleSpan(start, end, bow, i / (float)(points.Length - 1));
        }
    }

    private Vector2 ArmGuide()
    {
        Vector2 joint = oracleArm.GetArmElbowAnchor();
        if (oracleArm.Elbows == null || oracleArm.Elbows.Length < 2)
            return joint;

        Vector2 towardElbow = oracleArm.Elbows[1] - joint;
        Vector2 along = towardElbow.normalized;
        return joint + towardElbow * 0.37f + new Vector2(-along.y, along.x) * 0.04f;
    }

    private Rect VisibleBounds()
    {
        if (view == null)
            return new Rect(-8f, -5f, 16f, 10f);

        Vector2 lower = view.ViewportToWorldPoint(new Vector3(0f, 0f, -view.transform.position.z));
        Vector2 upper = view.ViewportToWorldPoint(new Vector3(1f, 1f, -view.transform.position.z));
        return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
    }

    private Vector2 ReceiverNearHead(Vector2 head, Rect bounds)
    {
        const float reach = 0.55f;
        const float margin = ReceiverLeash + 0.03f;
        float x = Mathf.Clamp(head.x + receiverSide * 0.53f, bounds.xMin + margin, bounds.xMax - margin);
        float rise = Mathf.Sqrt(Mathf.Max(0.12f * 0.12f, reach * reach - (x - head.x) * (x - head.x)));
        float y = Mathf.Clamp(head.y + rise, bounds.yMin + margin, bounds.yMax - margin);
        Vector2 receiver = new Vector2(x, y);
        if ((receiver - head).sqrMagnitude < 0.48f * 0.48f)
            receiver.x = Mathf.Clamp(head.x - receiverSide * 0.53f, bounds.xMin + margin, bounds.xMax - margin);
        return receiver;
    }

    private void Simulate(int outlet, int guide, Vector2 mount, Vector2 armPoint, Vector2 receiver, Rect bounds)
    {
        int last = trunkPos.Length - 1;
        for (int i = outlet + 1; i <= last; i++)
        {
            Vector2 point = trunkPos[i];
            Vector2 velocity = Vector2.ClampMagnitude((point - previousPositions[i]) * 0.983f, 0.105f);
            previousPositions[i] = point;
            // A slight lift near the floor leaves the rest of the cable weightless.
            float lift = 0.65f * (1f - i / (float)last) * StepSeconds * StepSeconds;
            trunkPos[i] = point + velocity + Vector2.up * lift;
        }
        trunkPos[0] = mount;
        trunkPos[guide] = armPoint;
        trunkPos[last] += (receiver - trunkPos[last]) * (12f * StepSeconds * StepSeconds);
        for (int pass = 0; pass < SolverIterations; pass++)
        {
            // Resist creases across neighbouring links and small coils across a longer span.
            for (int i = Mathf.Max(0, outlet - 1); i < last - 1; i++)
                ConstrainDistance(i, i + 2, outlet, guide, segmentLength * 1.94f, bendOnly: true);
            for (int i = Mathf.Max(0, outlet - 1); i <= last - 6; i++)
                ConstrainDistance(i, i + 6, outlet, guide, segmentLength * 4.85f, bendOnly: true);

            // Alternate the direction so both runs share changes in reach.
            for (int i = 0; i < last; i++)
            {
                int a = pass % 2 == 0 ? i : last - i - 1;
                ConstrainDistance(a, a + 1, outlet, guide, segmentLength, bendOnly: false);
            }

            for (int i = outlet + 1; i <= last; i++)
                if (i != guide)
                    trunkPos[i] = KeepInside(trunkPos[i], bounds);
            trunkPos[last] = receiver + Vector2.ClampMagnitude(trunkPos[last] - receiver, ReceiverLeash);
        }
    }

    private void ConstrainDistance(int a, int b, int outlet, int guide, float length, bool bendOnly)
    {
        bool fixedA = a <= outlet || a == guide;
        bool fixedB = b <= outlet || b == guide;
        if ((fixedA && fixedB) || (bendOnly && a < guide && b > guide))
            return;

        Vector2 delta = trunkPos[b] - trunkPos[a];
        float distance = delta.magnitude;
        if (distance < 0.0001f || (bendOnly && distance >= length))
            return;

        float weight = fixedA || fixedB ? 1f : 0.5f;
        Vector2 correction = delta * ((distance - length) / distance * weight);
        if (bendOnly)
            correction *= 0.34f;

        if (!fixedA)
            trunkPos[a] += correction;
        if (!fixedB)
            trunkPos[b] -= correction;
    }

    private void SeedSpan(int first, int last, Vector2 start, Vector2 end, Rect bounds)
    {
        float desiredLength = (last - first) * segmentLength;
        Vector2 chord = (end - start).normalized;
        Vector2 normal = new Vector2(-chord.y, chord.x);
        float low = 0f;
        float high = desiredLength * 0.55f;
        for (int pass = 0; pass < 9; pass++)
        {
            float amplitude = (low + high) * 0.5f;
            seedPoints[0] = start;
            for (int i = 1; i <= SeedSamples; i++)
            {
                float t = i / (float)SeedSamples;
                Vector2 point = Vector2.Lerp(start, end, t) + normal * (Mathf.Sin(t * 2f * Mathf.PI) * amplitude);
                seedPoints[i] = i == SeedSamples ? end : KeepInside(point, bounds);
                seedDistances[i] = seedDistances[i - 1] + Vector2.Distance(seedPoints[i - 1], seedPoints[i]);
            }
            if (seedDistances[SeedSamples] < desiredLength)
                low = amplitude;
            else
                high = amplitude;
        }

        trunkPos[first] = start;
        int sample = 1;
        for (int i = first + 1; i < last; i++)
        {
            float distance = seedDistances[SeedSamples] * ((i - first) / (float)(last - first));
            while (sample < SeedSamples && seedDistances[sample] < distance)
                sample++;
            float t = Mathf.InverseLerp(seedDistances[sample - 1], seedDistances[sample], distance);
            trunkPos[i] = Vector2.Lerp(seedPoints[sample - 1], seedPoints[sample], t);
        }
        trunkPos[last] = end;
    }

    private static Vector2 KeepInside(Vector2 point, Rect bounds)
    {
        const float clearance = 0.03f;
        return new Vector2(Mathf.Clamp(point.x, bounds.xMin + clearance, bounds.xMax - clearance),
            Mathf.Clamp(point.y, bounds.yMin + clearance, bounds.yMax - clearance));
    }

    private static Vector2 SampleSpan(Vector2 start, Vector2 end, Vector2 bow, float t)
    {
        Vector2 chord = end - start;
        float length = chord.magnitude;
        if (length < 0.0001f)
            return Vector2.Lerp(start, end, t);

        // The curve only offsets across the chord. Samples always progress
        // toward the attachment, even during an abrupt change of direction.
        Vector2 normal = new Vector2(-chord.y, chord.x) / length;
        float offset = Mathf.Clamp(Vector2.Dot(bow, normal), -length * 0.32f, length * 0.32f);
        return Vector2.Lerp(start, end, t) + normal * (Mathf.Sin(Mathf.PI * t) * offset);
    }
}
