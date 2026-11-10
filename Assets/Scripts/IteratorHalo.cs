using UnityEngine;

public class IteratorHalo : MonoBehaviour
{
    [Header("符文资源")]
    [Tooltip("把图集里切好的各种小方块/符文拖到这个数组里")]
    public Sprite[] glyphSprites;

    [Header("光环设置")]
    public Color haloColor = new Color(1f, 0.6f, 0f, 0.8f); // 五卵石经典的橙黄色
    public float baseRadius = 1.2f; // 光环的基础半径大小

    // 还原源码的 3 个环设置 (为了性能和视觉比例稍微缩减了数量)
    private int[] ringCounts = { 10, 24, 40 };
    private float[] ringRadiusMultipliers = { 1.0f, 1.4f, 1.8f };

    // 内存位阵列
    private Transform[][] bits;
    private SpriteRenderer[][] bitRenderers;
    private float[][] bitTargetAlphas;

    // 还原源码的停顿旋转逻辑
    private float[] ringRotations = new float[3];
    private float[] ringTargetRotations = new float[3];
    private float[] ringRotSpeeds = new float[3];

    void Start()
    {
        bits = new Transform[3][];
        bitRenderers = new SpriteRenderer[3][];
        bitTargetAlphas = new float[3][];

        for (int i = 0; i < 3; i++)
        {
            bits[i] = new Transform[ringCounts[i]];
            bitRenderers[i] = new SpriteRenderer[ringCounts[i]];
            bitTargetAlphas[i] = new float[ringCounts[i]];

            // 为每一层环随机设定初始目标角度
            ringTargetRotations[i] = Random.Range(0f, 360f);

            for (int j = 0; j < ringCounts[i]; j++)
            {
                // 用代码自动生成符文节点
                GameObject bitObj = new GameObject($"MemoryBit_{i}_{j}");
                bitObj.transform.SetParent(this.transform);

                SpriteRenderer sr = bitObj.AddComponent<SpriteRenderer>();
                sr.sprite = glyphSprites[Random.Range(0, glyphSprites.Length)]; // 随机抽选一个符文
                sr.color = haloColor;
                sr.sortingOrder = -5; // 确保光环渲染在脑袋和身体的后方

                bits[i][j] = bitObj.transform;
                bitRenderers[i][j] = sr;
                bitTargetAlphas[i][j] = Random.value;
            }
        }
    }

    void Update()
    {
        for (int i = 0; i < 3; i++)
        {
            // 1. 还原源码的“神经质停顿旋转”
            ringRotations[i] = Mathf.Lerp(ringRotations[i], ringTargetRotations[i], Time.deltaTime * ringRotSpeeds[i]);

            // 当接近目标角度时，停顿一下，然后设定下一个突然转动的目标
            if (Mathf.Abs(ringRotations[i] - ringTargetRotations[i]) < 1f)
            {
                if (Random.value < 0.05f) // 低概率触发下一次转动
                {
                    // 随机正转或反转，转动 15 到 150 度 (完全复刻源码逻辑)
                    float rotAmount = Random.Range(15f, 150f) * ((Random.value < 0.5f) ? -1f : 1f);
                    ringTargetRotations[i] += rotAmount;
                    ringRotSpeeds[i] = Random.Range(2f, 10f); // 随机这一下转动的速度
                }
            }

            // 2. 更新每个符文的位置和闪烁
            float currentRadius = baseRadius * ringRadiusMultipliers[i];

            for (int j = 0; j < ringCounts[i]; j++)
            {
                // 计算圆形排列的角度
                float angle = (j * (360f / ringCounts[i])) + ringRotations[i];
                float rad = angle * Mathf.Deg2Rad;

                // 极坐标转换为 XY 坐标
                Vector3 targetPos = new Vector3(Mathf.Cos(rad) * currentRadius, Mathf.Sin(rad) * currentRadius, 0);
                bits[i][j].localPosition = targetPos;
                bits[i][j].localRotation = Quaternion.Euler(0, 0, angle + 90f); // 让符文朝向圆心

                // 3. 还原源码的数据流闪烁 (Fill)
                if (Random.value < 0.02f)
                {
                    // 极高频率地在“完全亮起”和“半透明/不可见”之间切换
                    bitTargetAlphas[i][j] = (Random.value < 0.3f) ? 1f : Random.Range(0f, 0.3f);
                }

                Color c = bitRenderers[i][j].color;
                c.a = Mathf.Lerp(c.a, bitTargetAlphas[i][j], Time.deltaTime * 15f);
                bitRenderers[i][j].color = c;
            }
        }
    }
}