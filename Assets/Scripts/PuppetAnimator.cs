using UnityEngine;

public class PuppetAnimator : MonoBehaviour
{
    [Header("绑定的部件")]
    public Transform body;
    public Transform head;
    public Transform eyeLeft;
    public Transform eyeRight;
    public Transform shoulderLeft;
    public Transform shoulderRight;
    [SerializeField] private SpriteRenderer sleeveLeft;
    [SerializeField] private SpriteRenderer sleeveRight;
    [SerializeField] private Transform handLeft;
    [SerializeField] private Transform handRight;

    [Header("静置呼吸")]
    [Min(0f)] public float floatAmplitude = 0.012f;
    [Min(0f)] public float floatSpeed = 1.2f;

    [Header("注意玩家时的细微位移")]
    [Min(0f)] public float bodyLookRadius = 0.018f;
    [Min(0f)] public float shoulderLookRadius = 0.02f;
    [Min(0f)] public float headLookRadius = 0.025f;
    [Min(0f)] public float eyeLookRadius = 0.025f;
    [Min(0.01f)] public float lookSpeed = 5f;

    [Header("自然眨眼")]
    public bool blinkEnabled = true;
    [Min(0.5f)] public float blinkIntervalMin = 3.5f;
    [Min(0.5f)] public float blinkIntervalMax = 6f;
    [Min(0.05f)] public float blinkDuration = 0.16f;

    private Vector3 bodyStartLocalPos;
    private Vector3 headStartLocalPos;
    private Vector3 eyeLeftStartLocalPos;
    private Vector3 eyeRightStartLocalPos;
    private Vector3 shoulderLeftStartLocalPos;
    private Vector3 shoulderRightStartLocalPos;
    private Vector3 eyeLeftStartScale;
    private Vector3 eyeRightStartScale;
    private SSOraclePetBrain attentionBrain;
    private float blinkTimer;
    private float nextBlinkTime;
    private const float HandSimulationStep = 1f / 40f;
    private const float HandAirFriction = 0.98f;
    private PuppetBodyGraphics bodyGraphics;
    private float handSimulationAccumulator;
    private Vector2 leftHandPosition;
    private Vector2 leftHandLastPosition;
    private Vector2 leftHandVelocity;
    private Vector2 rightHandPosition;
    private Vector2 rightHandLastPosition;
    private Vector2 rightHandVelocity;
    private Vector2 lastBodyChunkPosition;
    private bool handsInitialized;

    public Transform HandLeft => handLeft;
    public Transform HandRight => handRight;
    public Transform ShoulderLeft => shoulderLeft;
    public Transform ShoulderRight => shoulderRight;

    private void Start()
    {
        if (body != null) bodyStartLocalPos = body.localPosition;
        if (head != null) headStartLocalPos = head.localPosition;
        if (eyeLeft != null)
        {
            eyeLeftStartLocalPos = eyeLeft.localPosition;
            eyeLeftStartScale = eyeLeft.localScale;
        }
        if (eyeRight != null)
        {
            eyeRightStartLocalPos = eyeRight.localPosition;
            eyeRightStartScale = eyeRight.localScale;
        }
        if (shoulderLeft != null) shoulderLeftStartLocalPos = shoulderLeft.localPosition;
        if (shoulderRight != null) shoulderRightStartLocalPos = shoulderRight.localPosition;

        attentionBrain = GetComponent<SSOraclePetBrain>();
        bodyGraphics = GetComponentInChildren<PuppetBodyGraphics>(true);
        if (attentionBrain != null && attentionBrain.head == null) attentionBrain.head = head;
        if (sleeveLeft != null) sleeveLeft.enabled = false;
        if (sleeveRight != null) sleeveRight.enabled = false;
        SetHandSortingOrder(handLeft, 1);
        SetHandSortingOrder(handRight, 1);
        SetHandColor(handLeft, new Color(1f, 0.4f, 203f / 255f, 1f));
        SetHandColor(handRight, new Color(1f, 0.4f, 203f / 255f, 1f));
        ScheduleNextBlink();
    }

    private void Update()
    {
        if (head == null || body == null) return;

        float dt = Time.deltaTime;
        float attention = attentionBrain != null ? attentionBrain.AttentionWeight : 0f;
        Vector3 lookDirection = Vector3.zero;
        if (attentionBrain != null)
        {
            lookDirection = (attentionBrain.GazePoint - head.position).normalized;
        }

        // Exponential damping gives the same response at different frame rates.
        float bodyLerp = 1f - Mathf.Exp(-lookSpeed * 0.6f * dt);
        float headLerp = 1f - Mathf.Exp(-lookSpeed * dt);
        float shoulderLerp = 1f - Mathf.Exp(-lookSpeed * 0.8f * dt);
        float eyeLerp = 1f - Mathf.Exp(-lookSpeed * 1.5f * dt);

        float breath = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        Vector3 floatOffset = Vector3.up * breath;

        body.localPosition = Vector3.Lerp(
            body.localPosition,
            bodyStartLocalPos + floatOffset + lookDirection * (bodyLookRadius * attention),
            bodyLerp);

        head.localPosition = Vector3.Lerp(
            head.localPosition,
            headStartLocalPos + floatOffset + lookDirection * (headLookRadius * attention),
            headLerp);

        Vector3 shoulderOffset = floatOffset + lookDirection * (shoulderLookRadius * attention);
        if (shoulderLeft != null)
            shoulderLeft.localPosition = Vector3.Lerp(shoulderLeft.localPosition, shoulderLeftStartLocalPos + shoulderOffset, shoulderLerp);
        if (shoulderRight != null)
            shoulderRight.localPosition = Vector3.Lerp(shoulderRight.localPosition, shoulderRightStartLocalPos + shoulderOffset, shoulderLerp);

        Vector3 eyeOffset = GetEyeLocalOffset(lookDirection) * attention;
        float blink = UpdateBlink(dt);
        if (eyeLeft != null)
        {
            eyeLeft.localPosition = Vector3.Lerp(eyeLeft.localPosition, eyeLeftStartLocalPos + eyeOffset, eyeLerp);
            eyeLeft.localScale = new Vector3(eyeLeftStartScale.x,
                eyeLeftStartScale.y * Mathf.Lerp(1f, 0.08f, blink), eyeLeftStartScale.z);
        }
        if (eyeRight != null)
        {
            eyeRight.localPosition = Vector3.Lerp(eyeRight.localPosition, eyeRightStartLocalPos + eyeOffset, eyeLerp);
            eyeRight.localScale = new Vector3(eyeRightStartScale.x,
                eyeRightStartScale.y * Mathf.Lerp(1f, 0.08f, blink), eyeRightStartScale.z);
        }
    }

    private void LateUpdate()
    {
        SimulateHands();
    }

    public void DisableLegacySleeves()
    {
        if (sleeveLeft != null) sleeveLeft.enabled = false;
        if (sleeveRight != null) sleeveRight.enabled = false;
    }

    private static void SetHandSortingOrder(Transform hand, int sortingOrder)
    {
        if (hand == null) return;
        SpriteRenderer renderer = hand.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.sortingOrder = sortingOrder;
    }

    private static void SetHandColor(Transform hand, Color color)
    {
        if (hand == null)
            return;
        SpriteRenderer renderer = hand.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = color;
    }

    private void SimulateHands()
    {
        if (body == null || handLeft == null || handRight == null)
            return;

        if (!handsInitialized)
            InitializeHands();

        handSimulationAccumulator = Mathf.Min(
            handSimulationAccumulator + Mathf.Min(Time.unscaledDeltaTime, 0.05f), 0.1f);
        while (handSimulationAccumulator >= HandSimulationStep)
        {
            Vector2 bodyPosition = body.position;
            Vector2 bodyVelocity = bodyPosition - lastBodyChunkPosition;
            GetBodyAxis(out Vector2 up, out Vector2 left);
            SimulateHand(ref leftHandPosition, ref leftHandLastPosition,
                ref leftHandVelocity, bodyPosition, bodyVelocity, up, left, 0);
            SimulateHand(ref rightHandPosition, ref rightHandLastPosition,
                ref rightHandVelocity, bodyPosition, bodyVelocity, up, left, 1);
            lastBodyChunkPosition = bodyPosition;
            handSimulationAccumulator -= HandSimulationStep;
        }

        float timeStacker = Mathf.Clamp01(handSimulationAccumulator / HandSimulationStep);
        Vector2 leftPosition = Vector2.Lerp(leftHandLastPosition, leftHandPosition, timeStacker);
        Vector2 rightPosition = Vector2.Lerp(rightHandLastPosition, rightHandPosition, timeStacker);
        handLeft.position = new Vector3(leftPosition.x, leftPosition.y, body.position.z);
        handRight.position = new Vector3(rightPosition.x, rightPosition.y, body.position.z);
    }

    private void InitializeHands()
    {
        Vector2 bodyPosition = body.position;
        // GenericBodyPart.Reset(connection.pos) starts Oracle hands within a
        // one-pixel radial offset of body chunk 0. Their pose then comes from
        // Update + ConnectToPoint and the source drift forces below, rather
        // than from authored left/right shoulder offsets.
        leftHandPosition = bodyPosition;
        rightHandPosition = bodyPosition;
        leftHandLastPosition = leftHandPosition;
        rightHandLastPosition = rightHandPosition;
        leftHandVelocity = Vector2.zero;
        rightHandVelocity = Vector2.zero;
        lastBodyChunkPosition = bodyPosition;
        handsInitialized = true;
    }

    private void SimulateHand(ref Vector2 position, ref Vector2 lastPosition,
        ref Vector2 velocity, Vector2 bodyPosition, Vector2 bodyVelocity,
        Vector2 up, Vector2 left, int side)
    {
        lastPosition = position;
        position += velocity;
        velocity *= HandAirFriction;
        ConnectHandToBody(ref position, ref velocity, bodyPosition, bodyVelocity,
            Pixel(15f), 0.3f, 0.01f);

        // OracleGraphics.Update: room-world gravity plus the body-axis and
        // side-specific drift applied after GenericBodyPart.ConnectToPoint.
        Vector2 down = -up;
        Vector2 downPerpendicular = new Vector2(-down.y, down.x);
        velocity.y -= Pixel(0.5f);
        velocity += down * Pixel(0.3f);
        velocity += downPerpendicular * Pixel(0.3f * (side == 0 ? -1f : 1f));
    }

    private static void ConnectHandToBody(ref Vector2 position, ref Vector2 velocity,
        Vector2 bodyPosition, Vector2 bodyVelocity, float connectionRadius,
        float adaptVelocity, float exaggerateVelocity)
    {
        Vector2 towardBody = bodyPosition - position;
        float distance = towardBody.magnitude;
        Vector2 direction = distance > 1e-6f ? towardBody / distance : Vector2.zero;
        velocity += bodyVelocity * exaggerateVelocity;
        if (distance >= connectionRadius)
        {
            Vector2 correction = direction * (connectionRadius - distance);
            position -= correction;
            velocity -= correction;
        }
        velocity -= bodyVelocity;
        velocity *= 1f - adaptVelocity;
        velocity += bodyVelocity;
    }

    private void GetBodyAxis(out Vector2 up, out Vector2 left)
    {
        up = head != null ? (Vector2)head.position - (Vector2)body.position : (Vector2)transform.up;
        if (up.sqrMagnitude < 1e-8f)
            up = transform.up;
        up.Normalize();
        left = new Vector2(-up.y, up.x);
    }

    private float Pixel(float value) => value / Mathf.Max(1f, bodyGraphics != null ? bodyGraphics.pixelsPerUnit : 100f);

    private Vector3 GetEyeLocalOffset(Vector3 worldDirection)
    {
        if (eyeLeft == null || eyeLeft.parent == null) return worldDirection * eyeLookRadius;
        Vector3 localDirection = eyeLeft.parent.InverseTransformDirection(worldDirection);
        localDirection.z = 0f;
        if (localDirection.sqrMagnitude > 1f) localDirection.Normalize();
        return localDirection * eyeLookRadius;
    }

    private float UpdateBlink(float dt)
    {
        if (!blinkEnabled) return 0f;
        if (blinkTimer < 0f)
        {
            nextBlinkTime -= dt;
            if (nextBlinkTime <= 0f) blinkTimer = 0f;
            else return 0f;
        }

        blinkTimer += dt;
        float t = Mathf.Clamp01(blinkTimer / Mathf.Max(0.05f, blinkDuration));
        float amount = Mathf.Sin(t * Mathf.PI);
        if (t >= 1f)
        {
            blinkTimer = -1f;
            ScheduleNextBlink();
        }
        return amount;
    }

    private void ScheduleNextBlink()
    {
        blinkTimer = -1f;
        float min = Mathf.Max(0.5f, blinkIntervalMin);
        float max = Mathf.Max(min, blinkIntervalMax);
        nextBlinkTime = Random.Range(min, max);
    }
}
