using UnityEngine;

[DefaultExecutionOrder(20)]
public class OracleUmbilical : MonoBehaviour
{
    public Transform floorAnchor;
    public bool useCameraAnchor = true;
    public OracleArm oracleArm;
    public Transform puppetBody;
    public Transform puppetHead;

    [Min(3)] public int trunkSegments = 80;
    [Min(1)] public int elbowIndex = 60;
    public Vector2 puppetConnectionOffset = new Vector2(0f, -0.18f);
    [Min(0f)] public float upperSag = 0.8f;
    [Min(0f)] public float lowerSag = 0.24f;
    [Min(0)] public int smallCordCount = 5;
    [Min(2)] public int smallCordSegments = 20;

    [HideInInspector] public Vector2[] trunkPos;
    [HideInInspector] public Vector2[][] smallCordsPos;

    private Vector2 previousTail;
    private Vector2 sway;
    private bool hasPreviousTail;

    private void Start()
    {
        trunkPos = new Vector2[Mathf.Max(3, trunkSegments)];
        smallCordsPos = new Vector2[Mathf.Max(0, smallCordCount)][];
        for (int i = 0; i < smallCordsPos.Length; i++)
            smallCordsPos[i] = new Vector2[Mathf.Max(2, smallCordSegments)];
    }

    private void LateUpdate()
    {
        if (puppetBody == null || oracleArm == null ||
            oracleArm.joints == null || oracleArm.joints.Length < 2)
            return;

        Vector2 mount = !useCameraAnchor && floorAnchor != null
            ? (Vector2)floorAnchor.position : oracleArm.GetBoundaryAnchor();
        Vector2 elbow = oracleArm.GetArmElbowAnchor();
        Vector2 tail = (Vector2)puppetBody.position + puppetConnectionOffset;
        Vector2 head = puppetHead != null
            ? (Vector2)puppetHead.position : tail + Vector2.up * 0.35f;

        float dt = Time.deltaTime;
        Vector2 movement = hasPreviousTail && dt > 0f ? (tail - previousTail) / dt : Vector2.zero;
        Vector2 targetSway = Vector2.ClampMagnitude(-movement * 0.024f, 0.16f);
        sway = Vector2.Lerp(sway, targetSway, 1f - Mathf.Exp(-10f * dt));
        previousTail = tail;
        hasPreviousTail = true;

        int last = trunkPos.Length - 1;
        int guide = Mathf.Clamp(elbowIndex, 1, last - 1);

        // The screen mount feeds the trunk as the arm moves. The two exposed
        // spans keep a bounded sag and pass through the mechanical elbow guide.
        Vector2 upperBow = Vector2.down * upperSag + sway * 0.4f;
        Vector2 lowerBow = Vector2.down * lowerSag + sway;
        for (int i = 0; i <= guide; i++)
            trunkPos[i] = SampleSpan(mount, elbow, upperBow, i / (float)guide);
        for (int i = guide + 1; i <= last; i++)
            trunkPos[i] = SampleSpan(elbow, tail, lowerBow, (i - guide) / (float)(last - guide));

        float tailDistance = Vector2.Distance(elbow, tail);
        for (int cord = 0; cord < smallCordsPos.Length; cord++)
        {
            float spread = smallCordsPos.Length <= 1 ? 0.5f : cord / (float)(smallCordsPos.Length - 1);
            float u = 1f - Mathf.Min(0.55f, Mathf.Lerp(0.34f, 0.15f, spread) / Mathf.Max(0.01f, tailDistance));
            float sample = Mathf.Lerp(guide, last, u);
            int first = Mathf.FloorToInt(sample);
            Vector2 start = Vector2.Lerp(trunkPos[first], trunkPos[Mathf.Min(first + 1, last)], sample - first);
            Vector2 end = head + new Vector2(Mathf.Lerp(-0.035f, 0.035f, spread), -0.02f);
            Vector2 bow = new Vector2(Mathf.Lerp(-0.11f, 0.11f, spread), -0.025f) + sway * 0.25f;

            Vector2[] points = smallCordsPos[cord];
            for (int i = 0; i < points.Length; i++)
                points[i] = SampleSpan(start, end, bow, i / (float)(points.Length - 1));
        }
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
