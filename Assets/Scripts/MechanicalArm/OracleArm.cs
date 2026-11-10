using UnityEngine;

// 保证物理在渲染之前计算
[DefaultExecutionOrder(-50)]
public class OracleArm : MonoBehaviour
{
    [Header("追踪目标")]
    public Transform puppetBody; // 人偶身体

    [Header("原版骨骼参数 (按比例缩小)")]
    // 对应源码的 300f, 150f, 90f, 30f
    public float[] segmentLengths = { 3.0f, 1.5f, 0.9f, 0.3f };
    public float baseSlideSpeed = 2f;
    public Vector2 connectionOffset = new Vector2(0, 0.8f); // 对准人偶后背

    // 暴露给视觉脚本读取的坐标数组
    [HideInInspector] public Vector3[] joints;

    private Camera mainCam;
    private Vector2[] corners = new Vector2[4];
    public float currentTrackPos = 0f;
    private float totalLength;
    private bool initialized;

    void Start()
    {
        mainCam = Camera.main;
        int nodeCount = segmentLengths.Length + 1;
        joints = new Vector3[nodeCount];

        totalLength = 0f;
        foreach (float len in segmentLengths) totalLength += len;

        if (mainCam != null && puppetBody != null)
        {
            CalculateBounds();
            Vector3 targetPos = puppetBody.position + (Vector3)connectionOffset;
            currentTrackPos = GetClosestTrackPos(targetPos);
            joints[0] = GetPosFromTrack(currentTrackPos);
            InitializeChain(joints[0], targetPos);
            initialized = true;
        }
    }

    void Update()
    {
        if (puppetBody == null || mainCam == null || joints == null || joints.Length < 2) return;

        // 1. 复刻源码：计算房间四角与 0~4 的一维轨道 (FramePos)
        CalculateBounds();
        Vector3 targetPos = puppetBody.position + (Vector3)connectionOffset;
        float targetTrackPos = GetClosestTrackPos(targetPos);

        if (!initialized)
        {
            currentTrackPos = targetTrackPos;
            joints[0] = GetPosFromTrack(currentTrackPos);
            InitializeChain(joints[0], targetPos);
            initialized = true;
        }

        // 使用角度插值保证轨道滑行绝对平滑，不穿模
        float currentAngle = currentTrackPos * 90f;
        float targetAngle = targetTrackPos * 90f;
        currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, 1f - Mathf.Exp(-baseSlideSpeed * Time.deltaTime));
        if (currentAngle < 0f) currentAngle += 360f;
        currentTrackPos = (currentAngle % 360f) / 90f;

        // 基座真实坐标
        joints[0] = GetPosFromTrack(currentTrackPos);

        // 2. 复刻源码：刚体逆向运动学 (Inverse Kinematics)
        int n = joints.Length;
        // 施加原版的重力下垂倾向
        for (int i = 1; i < n - 1; i++) joints[i].y -= 4f * Time.deltaTime;

        float currentDist = Vector3.Distance(joints[0], targetPos);

        if (currentDist >= totalLength)
        {
            // 手臂到达伸展极限时拉直；原版不会因此移动 Oracle 本体。
            Vector3 dir = (targetPos - joints[0]).normalized;
            targetPos = joints[0] + dir * totalLength;

            for (int i = 1; i < n; i++)
                joints[i] = joints[i - 1] + dir * segmentLengths[i - 1];
        }
        else
        {
            // 刚体 FABRIK 折叠逼近
            for (int iter = 0; iter < 15; iter++)
            {
                joints[n - 1] = targetPos;
                for (int i = n - 2; i >= 0; i--)
                {
                    Vector3 dir = (joints[i] - joints[i + 1]).normalized;
                    joints[i] = joints[i + 1] + dir * segmentLengths[i];
                }

                joints[0] = GetPosFromTrack(currentTrackPos);
                for (int i = 1; i < n; i++)
                {
                    Vector3 dir = (joints[i] - joints[i - 1]).normalized;
                    joints[i] = joints[i - 1] + dir * segmentLengths[i - 1];
                }
            }
            joints[n - 1] = targetPos; // 终极吸附，消灭残影
        }
    }

    private void InitializeChain(Vector3 basePosition, Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - basePosition).normalized;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.down;
        for (int i = 1; i < joints.Length; i++)
            joints[i] = joints[i - 1] + direction * segmentLengths[i - 1];
    }

    // --- 轨道数学换算 (严谨对齐屏幕边缘) ---
    // --- 轨道数学换算 (严谨对齐屏幕边缘) ---
    void CalculateBounds()
    {
        // 1. 获取摄像机在世界空间中的真实坐标！
        Vector2 camPos = mainCam.transform.position;

        float h = mainCam.orthographicSize;
        float w = h * mainCam.aspect;

        // 2. 将视野长宽叠加到摄像机的坐标上，得出绝对正确的屏幕边缘
        corners[0] = new Vector2(camPos.x - w, camPos.y + h); // 左上
        corners[1] = new Vector2(camPos.x + w, camPos.y + h); // 右上
        corners[2] = new Vector2(camPos.x + w, camPos.y - h); // 右下
        corners[3] = new Vector2(camPos.x - w, camPos.y - h); // 左下
    }

    float GetClosestTrackPos(Vector2 pos)
    {
        float dTop = Mathf.Abs(pos.y - corners[0].y), dRight = Mathf.Abs(pos.x - corners[1].x);
        float dBot = Mathf.Abs(pos.y - corners[2].y), dLeft = Mathf.Abs(pos.x - corners[0].x);
        float min = Mathf.Min(dTop, dRight, dBot, dLeft);

        if (min == dTop) return 0f + Mathf.InverseLerp(corners[0].x, corners[1].x, pos.x);
        if (min == dRight) return 1f + Mathf.InverseLerp(corners[1].y, corners[2].y, pos.y);
        if (min == dBot) return 2f + Mathf.InverseLerp(corners[2].x, corners[3].x, pos.x);
        return 3f + Mathf.InverseLerp(corners[3].y, corners[0].y, pos.y);
    }

    Vector2 GetPosFromTrack(float t)
    {
        if (t < 1f) return Vector2.Lerp(corners[0], corners[1], t);
        if (t < 2f) return Vector2.Lerp(corners[1], corners[2], t - 1f);
        if (t < 3f) return Vector2.Lerp(corners[2], corners[3], t - 2f);
        return Vector2.Lerp(corners[3], corners[0], t - 3f);
    }
}
