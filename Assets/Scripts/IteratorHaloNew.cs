using UnityEngine;

public class IteratorHaloNew : MonoBehaviour
{
    [Header("全息科技设置")]
    [Tooltip("原版在亮背景下是暗色，在暗背景下建议用半透明的青蓝色或纯白色")]
    public Color haloColor = new Color(0.8f, 1f, 1f, 0.4f);
    public float baseRadius = 2.5f; // 整体光环半径
    public float bitThickness = 0.04f; // 刻度线的粗细

    // 还原源码的刻度盘设置
    private int[] ringCounts = { 15, 36, 72 };
    private float[] ringRadiusMultipliers = { 0.6f, 1.0f, 1.4f };

    private Transform[][] bits;
    private SpriteRenderer[][] bitRenderers;
    private float[][] bitTargetFills; // 控制刻度的长度变化
    private float[][] bitTargetAlphas;

    // 停顿旋转逻辑
    private float[] ringRotations = new float[3];
    private float[] ringTargetRotations = new float[3];
    private float[] ringRotSpeeds = new float[3];

    // 同心圆环
    private LineRenderer[] baseRings;

    void Start()
    {
        // 1. 动态生成一个 1x1 的纯白像素，完全复刻源码的 FSprite("pixel")
        Texture2D pixelTex = Texture2D.whiteTexture;
        // 强制将 PPU 设为 1，让它恢复正常的 1x1 世界单位大小
        Sprite pixelSprite = Sprite.Create(pixelTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        bits = new Transform[3][];
        bitRenderers = new SpriteRenderer[3][];
        bitTargetFills = new float[3][];
        bitTargetAlphas = new float[3][];
        baseRings = new LineRenderer[2];

        // 2. 生成底层的雷达同心圆 (复刻 VectorCircle)
        for (int i = 0; i < 2; i++)
        {
            GameObject ringObj = new GameObject($"BaseRing_{i}");
            ringObj.transform.SetParent(this.transform);
            ringObj.transform.localPosition = Vector3.zero;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 60; // 圆的平滑度
            lr.startWidth = bitThickness * 0.5f;
            lr.endWidth = bitThickness * 0.5f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(haloColor.r, haloColor.g, haloColor.b, haloColor.a * 0.3f);
            lr.endColor = lr.startColor;
            lr.sortingOrder = -6;

            baseRings[i] = lr;
        }

        // 3. 生成数据刻度阵列
        for (int i = 0; i < 3; i++)
        {
            bits[i] = new Transform[ringCounts[i]];
            bitRenderers[i] = new SpriteRenderer[ringCounts[i]];
            bitTargetFills[i] = new float[ringCounts[i]];
            bitTargetAlphas[i] = new float[ringCounts[i]];

            ringTargetRotations[i] = Random.Range(0f, 360f);

            for (int j = 0; j < ringCounts[i]; j++)
            {
                GameObject bitObj = new GameObject($"MemoryBit_{i}_{j}");
                bitObj.transform.SetParent(this.transform);

                SpriteRenderer sr = bitObj.AddComponent<SpriteRenderer>();
                sr.sprite = pixelSprite; // 使用动态生成的纯像素
                sr.color = haloColor;
                sr.sortingOrder = -5;

                bits[i][j] = bitObj.transform;
                bitRenderers[i][j] = sr;
                bitTargetFills[i][j] = 1f;
                bitTargetAlphas[i][j] = Random.value;
            }
        }
    }

    void Update()
    {
        // 1. 更新底层同心圆的半径
        DrawCircle(baseRings[0], baseRadius * ringRadiusMultipliers[0]);
        DrawCircle(baseRings[1], baseRadius * ringRadiusMultipliers[1]);

        for (int i = 0; i < 3; i++)
        {
            // 2. 停顿旋转逻辑
            ringRotations[i] = Mathf.Lerp(ringRotations[i], ringTargetRotations[i], Time.deltaTime * ringRotSpeeds[i]);

            if (Mathf.Abs(ringRotations[i] - ringTargetRotations[i]) < 1f)
            {
                if (Random.value < 0.05f)
                {
                    float rotAmount = Random.Range(15f, 150f) * ((Random.value < 0.5f) ? -1f : 1f);
                    ringTargetRotations[i] += rotAmount;
                    ringRotSpeeds[i] = Random.Range(2f, 10f);
                }
            }

            float currentRadius = baseRadius * ringRadiusMultipliers[i];

            for (int j = 0; j < ringCounts[i]; j++)
            {
                float angle = (j * (360f / ringCounts[i])) + ringRotations[i];
                float rad = angle * Mathf.Deg2Rad;

                Vector3 targetPos = new Vector3(Mathf.Cos(rad) * currentRadius, Mathf.Sin(rad) * currentRadius, 0);
                bits[i][j].localPosition = targetPos;
                bits[i][j].localRotation = Quaternion.Euler(0, 0, angle + 90f);

                // 3. 科技感核心：极其神经质的数据流拉伸 (Fill) 与闪烁
                if (Random.value < 0.05f)
                {
                    // 高频切换透明度
                    bitTargetAlphas[i][j] = (Random.value < 0.3f) ? 1f : Random.Range(0f, 0.2f);
                    // 高频切换刻度线的长度 (模仿数据读写)
                    bitTargetFills[i][j] = Random.Range(0.1f, 1.0f);
                }

                Color c = bitRenderers[i][j].color;
                c.a = Mathf.Lerp(c.a, bitTargetAlphas[i][j], Time.deltaTime * 15f);
                bitRenderers[i][j].color = c;

                // 修改缩放：X 代表粗细(固定)，Y 代表刻度长度(动态拉伸)
                float currentFill = Mathf.Lerp(bits[i][j].localScale.y, bitTargetFills[i][j] * (baseRadius * 0.2f), Time.deltaTime * 20f);
                bits[i][j].localScale = new Vector3(bitThickness, currentFill, 1f);
            }
        }
    }

    // 辅助方法：用 LineRenderer 画出完美的雷达圆环
    void DrawCircle(LineRenderer lr, float radius)
    {
        int segments = lr.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * 360f / segments);
            lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0));
        }
    }
}