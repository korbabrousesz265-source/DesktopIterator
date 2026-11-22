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
    [Min(0)] public int smallCordCount = 14;
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
    private const float ReceiverLeash = 0.28f;
    private const float FineCordUnitsPerPixel = 10f / 540f;
    private const float FineCordOutletDistance = 1.63f;
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
    private SSOraclePetBrain puppetBrain;
    private FineCord[] fineCords;
    private Vector2 lastFineHead;

    private void Start()
    {
        trunkPos = new Vector2[Mathf.Max(3, trunkSegments)];
        previousPositions = new Vector2[trunkPos.Length];
        view = Camera.main;
        if (puppetBody != null)
            puppetBrain = puppetBody.GetComponent<SSOraclePetBrain>();
        smallCordsPos = new Vector2[Mathf.Max(0, smallCordCount)][];
        fineCords = new FineCord[smallCordsPos.Length];
        int pointCount = Mathf.Max(2, smallCordSegments);
        for (int i = 0; i < fineCords.Length; i++)
        {
            int pair = i / 2;
            bool shortCord = i % 2 == 0;
            int groupCount = (fineCords.Length + (shortCord ? 1 : 0)) / 2;
            // Sample the centers of the source distribution's quantiles so the
            // desktop bundle has repeatable lengths without taking both extremes.
            float rank = (pair + 0.5f) / groupCount;
            float lengthPixels = shortCord ? Mathf.Lerp(50f, 65f, rank)
                : Mathf.Lerp(50f, 200f, Mathf.Pow(rank, 1.5f));
            float length = lengthPixels * ((pointCount - 1f) / pointCount) * FineCordUnitsPerPixel;
            float side = (pair + i % 2) % 2 == 0 ? -1f : 1f;
            float heading = i * 2.4f;
            Vector2 socketBias = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * 0.72f;
            fineCords[i] = new FineCord(pointCount, length, side, socketBias);
            smallCordsPos[i] = fineCords[i].Points;
        }
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
            lastFineHead = head;
            foreach (FineCord cord in fineCords)
                cord.Reset(trunkPos[last], head, bounds);
            initialized = true;
        }

        accumulatedTime += Mathf.Min(0.05f, Time.deltaTime);
        int steps = Mathf.FloorToInt(accumulatedTime / StepSeconds);
        Vector2 look = puppetBrain == null
            ? Vector2.zero
            : ((Vector2)puppetBrain.GazePoint - head).normalized * puppetBrain.AttentionWeight;
        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            Vector2 steppedHead = Vector2.Lerp(lastFineHead, head, t);
            Simulate(outlet, guide, mount, Vector2.Lerp(lastGuide, guidePoint, t),
                Vector2.Lerp(lastReceiver, receiver, t), bounds);
            Vector2 trunkTangent = (trunkPos[last] - trunkPos[last - 1]).normalized;
            foreach (FineCord cord in fineCords)
                cord.Step(trunkPos[last], trunkTangent, steppedHead, look, bounds);
        }
        if (steps > 0)
        {
            accumulatedTime -= steps * StepSeconds;
            lastGuide = guidePoint;
            lastReceiver = receiver;
            lastFineHead = head;
        }
        trunkPos[0] = mount;
        trunkPos[guide] = guidePoint;

        // The render endpoints follow the sockets even on a frame without a physics tick.
        foreach (FineCord cord in fineCords)
            cord.Pin(trunkPos[last], head);
    }

    private sealed class FineCord
    {
        private const int ConstraintPasses = 32;
        public Vector2[] Points { get; }
        private readonly Vector2[] previous;
        private readonly Vector2[] directions;
        private readonly float[] upper;
        private readonly float[] tension;
        private readonly float restLength;
        private readonly float initialSide;
        private readonly Vector2 headBias;

        public FineCord(int pointCount, float restLength, float initialSide, Vector2 headBias)
        {
            Points = new Vector2[pointCount];
            previous = new Vector2[pointCount];
            directions = new Vector2[pointCount - 1];
            upper = new float[pointCount - 1];
            tension = new float[pointCount - 1];
            this.restLength = restLength;
            this.initialSide = initialSide;
            this.headBias = headBias;
        }

        public void Pin(Vector2 outlet, Vector2 head)
        {
            Points[0] = outlet;
            Points[Points.Length - 1] = head;
        }

        public void Reset(Vector2 outlet, Vector2 head, Rect bounds)
        {
            Vector2 span = head - outlet;
            Vector2 along = span.sqrMagnitude > 0.000001f ? span.normalized : Vector2.up;
            Vector2 normal = new Vector2(-along.y, along.x) * initialSide;
            float length = ReachableLength(span.magnitude);
            float height = Mathf.Sqrt(Mathf.Max(0f, length * length - span.sqrMagnitude)) * 0.44f;
            int last = Points.Length - 1;
            for (int i = 1; i < last; i++)
            {
                float t = i / (float)last;
                Vector2 sag = normal * (height * Mathf.Sin(t * Mathf.PI));
                Points[i] = KeepInside(Vector2.Lerp(outlet, head, t) + sag, bounds);
            }
            Pin(outlet, head);
            Points.CopyTo(previous, 0);
        }

        private float ReachableLength(float gap)
        {
            // The trunk can drift beyond a short cord's rest length. A small,
            // length-dependent allowance keeps the taut strands distinguishable.
            return Mathf.Max(restLength, gap + restLength * 0.035f);
        }

        public void Step(Vector2 outlet, Vector2 tangent, Vector2 head, Vector2 look, Rect bounds)
        {
            int last = Points.Length - 1;
            Vector2 span = head - outlet;
            Vector2 headward = span.normalized;
            Vector2 headExit = (-headward + headBias - look).normalized;
            Vector2 outletMove = outlet - Points[0];
            Vector2 headMove = head - Points[last];
            // A desktop position jump moves the whole bundle's reference frame.
            // Shift its history too so the jump does not become an impulse.
            bool jumped = Mathf.Max(outletMove.sqrMagnitude, headMove.sqrMagnitude) > 0.14f * 0.14f;
            for (int i = 1; i < last; i++)
            {
                if (jumped)
                {
                    Vector2 shift = Vector2.Lerp(outletMove, headMove, i / (float)last);
                    Points[i] += shift;
                    previous[i] += shift;
                }
                Vector2 position = Points[i];
                Vector2 displacement = position - previous[i];
                previous[i] = position;
                float speed = displacement.magnitude / StepSeconds;
                float damping = Mathf.Lerp(0.994f, 0.966f, Mathf.InverseLerp(0.8f, 2.4f, speed));
                Vector2 drive = tangent * (Mathf.Max(0, 4 - i) * 15f)
                    + headExit * (Mathf.Max(0, i - last + 3) * 10f);
                Points[i] = KeepInside(position + Vector2.ClampMagnitude(displacement * damping, 0.065f)
                    + drive * (StepSeconds * StepSeconds), bounds);
            }
            Pin(outlet, head);

            float spacing = ReachableLength(span.magnitude) / last;
            for (int pass = 0; pass < ConstraintPasses; pass++)
            {
                for (int i = 0; i < last - 1; i++)
                    Relax(i, i + 2, last, spacing * 1.67f, bendOnly: true);
                if ((pass & 1) == 0)
                    for (int i = 0; i < last - 3; i++)
                        Relax(i, i + 4, last, spacing * 3.24f, bendOnly: true);
                for (int i = 0; i < last; i++)
                {
                    int a = (pass & 1) == 0 ? i : last - 1 - i;
                    Relax(a, a + 1, last, spacing, bendOnly: false);
                }
                for (int i = 1; i < last; i++)
                    Points[i] = KeepInside(Points[i], bounds);
            }
            if (last > 1)
                ResolveTension(last, spacing, bounds);
        }

        private void ResolveTension(int last, float spacing, Rect bounds)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                float maxError = 0f;
                for (int i = 0; i < last; i++)
                {
                    Vector2 delta = Points[i + 1] - Points[i];
                    float length = delta.magnitude;
                    directions[i] = length > 0.00001f ? delta / length : Vector2.right;
                    tension[i] = spacing - length;
                    maxError = Mathf.Max(maxError, Mathf.Abs(tension[i]));
                }
                if (maxError <= spacing * 0.005f)
                    return;

                // Each link shares one free point with the next. Solve their
                // coupled distance corrections at once, with both sockets fixed.
                for (int i = 0; i < last; i++)
                {
                    float diagonal = i == 0 || i == last - 1 ? 1f : 2f;
                    float coupling = i + 1 < last ? -Vector2.Dot(directions[i], directions[i + 1]) : 0f;
                    if (i > 0)
                    {
                        float lower = -Vector2.Dot(directions[i], directions[i - 1]);
                        diagonal -= lower * upper[i - 1];
                        tension[i] -= lower * tension[i - 1];
                    }
                    float inverse = 1f / Mathf.Max(0.001f, diagonal);
                    upper[i] = coupling * inverse;
                    tension[i] *= inverse;
                }
                for (int i = last - 2; i >= 0; i--)
                    tension[i] -= upper[i] * tension[i + 1];
                for (int i = 1; i < last; i++)
                {
                    Vector2 movement = directions[i - 1] * tension[i - 1] - directions[i] * tension[i];
                    Points[i] = KeepInside(Points[i] + Vector2.ClampMagnitude(movement, spacing * 1.5f), bounds);
                }
            }
        }

        private void Relax(int a, int b, int last, float spacing, bool bendOnly)
        {
            float aWeight = a == 0 ? 0f : 1f;
            float bWeight = b == last ? 0f : 1f;
            float totalWeight = aWeight + bWeight;
            if (totalWeight == 0f)
                return;
            Vector2 delta = Points[b] - Points[a];
            float length = delta.magnitude;
            if (length < 0.00001f || (bendOnly && length >= spacing))
                return;
            float strength = bendOnly ? 0.30f : 1f;
            Vector2 correction = delta * ((1f - spacing / length) * (strength / totalWeight));
            Points[a] += correction * aWeight;
            Points[b] -= correction * bWeight;
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
        float reach = fineCords.Length > 0 ? FineCordOutletDistance : 0.55f;
        const float margin = ReceiverLeash + 0.03f;
        float sideOffset = reach * 0.965f;
        float x = Mathf.Clamp(head.x + receiverSide * sideOffset, bounds.xMin + margin, bounds.xMax - margin);
        float dx = x - head.x;
        float rise = Mathf.Sqrt(Mathf.Max(0f, reach * reach - dx * dx));
        float y = Mathf.Clamp(head.y + rise, bounds.yMin + margin, bounds.yMax - margin);
        Vector2 receiver = new Vector2(x, y);
        if ((receiver - head).sqrMagnitude < reach * reach * 0.8f)
        {
            // Near a corner the same-radius socket fits better on the other side.
            x = Mathf.Clamp(head.x - receiverSide * sideOffset, bounds.xMin + margin, bounds.xMax - margin);
            dx = x - head.x;
            rise = Mathf.Sqrt(Mathf.Max(0f, reach * reach - dx * dx));
            receiver = new Vector2(x, Mathf.Clamp(head.y + rise, bounds.yMin + margin, bounds.yMax - margin));
        }
        return receiver;
    }

    private void Simulate(int outlet, int guide, Vector2 mount, Vector2 armPoint, Vector2 receiver, Rect bounds)
    {
        int last = trunkPos.Length - 1;
        Vector2 tipOffset = trunkPos[last] - receiver;
        Vector2 tipShift = Vector2.ClampMagnitude(tipOffset, ReceiverLeash) - tipOffset;
        for (int i = outlet + 1; i <= last; i++)
        {
            if (i > guide && tipShift.sqrMagnitude > 0.14f * 0.14f)
            {
                Vector2 shift = tipShift * ((i - guide) / (float)(last - guide));
                trunkPos[i] += shift;
                previousPositions[i] += shift;
            }
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
}
