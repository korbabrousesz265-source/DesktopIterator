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
        if (attentionBrain != null && attentionBrain.head == null) attentionBrain.head = head;
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
