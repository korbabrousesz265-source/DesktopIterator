using UnityEngine;

public class PerlinJitter : MonoBehaviour
{
    [Header("抖动参数")]
    [Tooltip("抖动的剧烈程度（频率）")]
    public float jitterSpeed = 15f;
    [Tooltip("抖动的物理范围（振幅）")]
    public float jitterAmount = 0.05f;

    private Vector3 startLocalPos;
    private float randomSeedX;
    private float randomSeedY;

    void Start()
    {
        // 记录初始本地坐标
        startLocalPos = transform.localPosition;

        // 为每个零件生成不同的随机种子，防止所有零件同频共振
        randomSeedX = Random.Range(0f, 1000f);
        randomSeedY = Random.Range(0f, 1000f);
    }

    void Update()
    {
        // 计算柏林噪声 (取值范围原本是 0~1，减去 0.5 让它在 -0.5~0.5 之间震荡)
        float noiseX = Mathf.PerlinNoise(Time.time * jitterSpeed + randomSeedX, 0f) - 0.5f;
        float noiseY = Mathf.PerlinNoise(0f, Time.time * jitterSpeed + randomSeedY) - 0.5f;

        // 应用偏移
        transform.localPosition = startLocalPos + new Vector3(noiseX, noiseY, 0) * jitterAmount * 2f;
    }
}