using UnityEngine;

// 确保在机械臂物理(-50)之后，但在视觉渲染之前执行！
[DefaultExecutionOrder(-40)]
public class OracleUmbilical : MonoBehaviour
{
    [Header("物理锚点链接")]
    public Transform floorAnchor;    // 屏幕外的地面基座
    public OracleArm oracleArm;      // 直接读取机械臂的数据！(不需要Tracker了)
    public Transform puppetBody;     // 人偶身体

    [Header("灰白主干电缆 (UbilicalCord)")]
    public int trunkSegments = 30;
    public int elbowIndex = 20;      // 挂在机械臂手肘的第几节
    public float trunkSegLen = 0.3f;
    public float structuralRigidity = 0.25f; // 工业硬度
    public float maxTailDistance = 2.5f;     // 尾巴防扯断锁死距离
    public float trunkGravity = -15f;

    [Header("红蓝神经线群 (SmallCords)")]
    public int smallCordCount = 3;   // 红、蓝、灰 3根
    public int smallCordSegments = 15;
    public float smallCordSegLen = 0.15f;
    public float smallCordGravity = -10f;
    [Tooltip("连接人偶身体时的偏移量，让它们插在下摆")]
    public Vector2 puppetConnectionOffset = new Vector2(0, -0.5f);

    // 暴露给视觉脚本读取的坐标矩阵！
    [HideInInspector] public Vector2[] trunkPos;
    [HideInInspector] public Vector2[][] smallCordsPos;

    private Vector2[] trunkVel;
    private Vector2[][] smallCordsVel;

    void Start()
    {
        trunkSegments = Mathf.Max(3, trunkSegments);
        smallCordSegments = Mathf.Max(2, smallCordSegments);
        elbowIndex = Mathf.Clamp(elbowIndex, 1, trunkSegments - 2);

        // 1. 初始化主干数组
        trunkPos = new Vector2[trunkSegments];
        trunkVel = new Vector2[trunkSegments];
        Vector2 start = floorAnchor != null ? (Vector2)floorAnchor.position : (Vector2)transform.position;
        Vector2 elbow = GetElbowPosition(start);
        Vector2 tail = puppetBody != null ? (Vector2)puppetBody.position + puppetConnectionOffset : elbow;
        for (int i = 0; i < trunkSegments; i++)
        {
            if (i <= elbowIndex)
                trunkPos[i] = Vector2.Lerp(start, elbow, i / (float)elbowIndex);
            else
                trunkPos[i] = Vector2.Lerp(elbow, tail, (i - elbowIndex) / (float)(trunkSegments - 1 - elbowIndex));
        }

        // 2. 初始化神经线矩阵
        smallCordsPos = new Vector2[smallCordCount][];
        smallCordsVel = new Vector2[smallCordCount][];
        for (int c = 0; c < smallCordCount; c++)
        {
            smallCordsPos[c] = new Vector2[smallCordSegments];
            smallCordsVel[c] = new Vector2[smallCordSegments];
            for (int i = 0; i < smallCordSegments; i++)
            {
                float t = i / (float)(smallCordSegments - 1);
                smallCordsPos[c][i] = Vector2.Lerp(tail, elbow, t);
            }
        }
    }

    void Update()
    {
        if (floorAnchor == null || puppetBody == null) return;
        float dt = Time.deltaTime;

        // ================= 1. 运算灰白主干电缆 =================
        for (int i = 0; i < trunkSegments; i++)
        {
            trunkVel[i].y += trunkGravity * dt;
            trunkPos[i] += trunkVel[i] * dt;
            trunkVel[i] *= Mathf.Pow(0.9f, dt * 60f); // 与 60 fps 时的原阻尼一致
        }

        for (int iter = 0; iter < 25; iter++)
        {
            trunkPos[0] = floorAnchor.position; // 钉死地面

            // 【神级联动】直接读取机械臂的手肘关节！不再需要 Tracker 物体！
            if (oracleArm != null && oracleArm.joints != null && oracleArm.joints.Length > 1)
            {
                trunkPos[elbowIndex] = oracleArm.joints[1];
            }

            // 长度约束
            for (int i = 0; i < trunkSegments - 1; i++)
            {
                Vector2 diff = trunkPos[i] - trunkPos[i + 1];
                float dist = diff.magnitude;
                if (dist > 0.0001f)
                {
                    Vector2 offset = diff.normalized * ((dist - trunkSegLen) * 0.5f);
                    if (i != 0 && i != elbowIndex) trunkPos[i] -= offset;
                    if (i + 1 != elbowIndex) trunkPos[i + 1] += offset;
                }
            }

            // 硬度约束
            if (structuralRigidity > 0f)
            {
                for (int i = 0; i < trunkSegments - 2; i++)
                {
                    Vector2 diff = trunkPos[i] - trunkPos[i + 2];
                    float dist = diff.magnitude;
                    if (dist > 0.0001f)
                    {
                        Vector2 offset = diff.normalized * ((dist - trunkSegLen * 2f) * 0.5f * structuralRigidity);
                        if (i != 0 && i != elbowIndex) trunkPos[i] -= offset;
                        if (i + 2 != elbowIndex) trunkPos[i + 2] += offset;
                    }
                }
            }

            // 防扯断锁死
            Vector2 tailPos = trunkPos[trunkSegments - 1];
            Vector2 bodyTarget = (Vector2)puppetBody.position + puppetConnectionOffset;
            Vector2 tailToBody = tailPos - bodyTarget;
            if (tailToBody.magnitude > maxTailDistance)
            {
                trunkPos[trunkSegments - 1] = bodyTarget + tailToBody.normalized * maxTailDistance;
            }
        }

        // ================= 2. 运算红蓝神经线群 =================
        for (int c = 0; c < smallCordCount; c++)
        {
            for (int i = 0; i < smallCordSegments; i++)
            {
                smallCordsVel[c][i].y += smallCordGravity * dt;
                smallCordsPos[c][i] += smallCordsVel[c][i] * dt;
                smallCordsVel[c][i] *= Mathf.Pow(0.85f, dt * 60f);
            }

            for (int iter = 0; iter < 15; iter++)
            {
                // 一头钉在人偶下摆，另一头死死钉在主干电缆的最后一节！
                smallCordsPos[c][0] = (Vector2)puppetBody.position + puppetConnectionOffset;
                smallCordsPos[c][smallCordSegments - 1] = trunkPos[trunkSegments - 1];

                for (int i = 0; i < smallCordSegments - 1; i++)
                {
                    Vector2 diff = smallCordsPos[c][i] - smallCordsPos[c][i + 1];
                    float dist = diff.magnitude;
                    if (dist > 0.0001f)
                    {
                        // 让不同的线长度稍微有一点点不同，错落有致
                        float customLen = smallCordSegLen + (c * 0.02f);
                        Vector2 offset = diff.normalized * ((dist - customLen) * 0.5f);
                        if (i != 0) smallCordsPos[c][i] -= offset;
                        if (i + 1 != smallCordSegments - 1) smallCordsPos[c][i + 1] += offset;
                    }
                }
            }
        }
    }

    private Vector2 GetElbowPosition(Vector2 fallback)
    {
        if (oracleArm != null && oracleArm.joints != null && oracleArm.joints.Length > 1)
            return oracleArm.joints[1];
        return fallback;
    }
}
