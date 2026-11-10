using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SSOraclePetBrain : MonoBehaviour
{
    public enum AttentionState
    {
        Idle,
        Noticed,
        Watching,
        Returning
    }

    [Header("目标与感知范围")]
    public Transform head;
    public Camera targetCamera;
    [Min(0.1f)] public float noticeRadius = 1.35f;
    [Min(0.1f)] public float trackedRadius = 2.1f;

    [Header("注意节奏（首轮估值）")]
    [Min(0f)] public float noticeDuration = 0.22f;
    [Min(0f)] public float leaveGraceDuration = 0.65f;
    [Min(0.01f)] public float returnDuration = 0.7f;

    public AttentionState State { get; private set; } = AttentionState.Idle;
    public float AttentionWeight { get; private set; }
    public Vector3 GazePoint { get; private set; }

    private float stateTimer;
    private float lostPlayerTimer;
    private Vector3 returningFromPoint;

    private void Awake()
    {
        if (head == null) head = transform;
        if (targetCamera == null) targetCamera = Camera.main;
        GazePoint = head.position;
        returningFromPoint = GazePoint;
    }

    private void Update()
    {
        if (head == null || targetCamera == null) return;

        float dt = Time.deltaTime;
        Vector3 cursorPoint = GetCursorWorldPoint();
        bool currentlyNear = Vector2.Distance(head.position, cursorPoint) <= noticeRadius;
        bool stillTracked = Vector2.Distance(head.position, cursorPoint) <= Mathf.Max(noticeRadius, trackedRadius);

        if (State != AttentionState.Idle && State != AttentionState.Returning && stillTracked)
            GazePoint = cursorPoint;

        switch (State)
        {
            case AttentionState.Idle:
                AttentionWeight = 0f;
                if (currentlyNear) EnterNoticed(cursorPoint);
                break;

            case AttentionState.Noticed:
                stateTimer += dt;
                AttentionWeight = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, noticeDuration)));
                UpdateLostPlayerTimer(stillTracked, dt);
                if (lostPlayerTimer >= leaveGraceDuration)
                    EnterReturning();
                else if (stateTimer >= noticeDuration)
                {
                    State = AttentionState.Watching;
                    stateTimer = 0f;
                    AttentionWeight = 1f;
                }
                break;

            case AttentionState.Watching:
                AttentionWeight = 1f;
                UpdateLostPlayerTimer(stillTracked, dt);
                if (lostPlayerTimer >= leaveGraceDuration)
                    EnterReturning();
                break;

            case AttentionState.Returning:
                if (currentlyNear)
                {
                    EnterNoticed(cursorPoint);
                    break;
                }

                stateTimer += dt;
                float returnProgress = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, returnDuration));
                AttentionWeight = 1f - Mathf.SmoothStep(0f, 1f, returnProgress);
                GazePoint = Vector3.Lerp(returningFromPoint, head.position, Mathf.SmoothStep(0f, 1f, returnProgress));
                if (returnProgress >= 1f)
                {
                    State = AttentionState.Idle;
                    AttentionWeight = 0f;
                    GazePoint = head.position;
                }
                break;
        }
    }

    private Vector3 GetCursorWorldPoint()
    {
        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(head.position.z - targetCamera.transform.position.z);
        Vector3 world = targetCamera.ScreenToWorldPoint(screen);
        world.z = head.position.z;
        return world;
    }

    private void EnterNoticed(Vector3 cursorPoint)
    {
        State = AttentionState.Noticed;
        stateTimer = 0f;
        lostPlayerTimer = 0f;
        GazePoint = cursorPoint;
    }

    private void EnterReturning()
    {
        State = AttentionState.Returning;
        stateTimer = 0f;
        lostPlayerTimer = 0f;
        returningFromPoint = GazePoint;
    }

    private void UpdateLostPlayerTimer(bool stillTracked, float dt)
    {
        if (stillTracked) lostPlayerTimer = 0f;
        else lostPlayerTimer += dt;
    }
}
