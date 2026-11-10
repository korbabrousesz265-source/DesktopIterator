using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(60)] // 在物理计算完成后最后渲染！
public class OracleUmbilicalGraphics : MonoBehaviour
{
    [Header("数据源")]
    public OracleUmbilical umbilicalPhysics;

    [Header("主干视觉参数")]
    public Color trunkBaseColor = new Color(0.2f, 0.2f, 0.22f);
    public Color trunkHighlightColor = new Color(0.6f, 0.65f, 0.7f);
    public float trunkWidth = 0.18f;
    public float trunkHeight = 0.06f;

    [Header("神经线视觉参数")]
    public Color[] cordColors = {
        new Color(0.6f, 0.15f, 0.15f), // 经典暗红
        new Color(0.15f, 0.25f, 0.5f), // 深海蓝
        new Color(0.3f, 0.3f, 0.35f)   // 暗灰色
    };
    public float cordWidth = 0.04f;

    private Sprite pixelSprite;

    // 生成的装甲片与线条组件
    private List<SpriteRenderer> trunkBases = new List<SpriteRenderer>();
    private List<SpriteRenderer> trunkHighlights = new List<SpriteRenderer>();
    private List<LineRenderer> smallCordRenderers = new List<LineRenderer>();

    void Start()
    {
        if (umbilicalPhysics == null) return;

        // 生成一个纯白像素，复刻原版的 "pixel" 贴图魔法
        Texture2D tex = Texture2D.whiteTexture;
        pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        // 1. 全自动组装主干蜈蚣线的外壳
        for (int i = 0; i < umbilicalPhysics.trunkSegments; i++)
        {
            GameObject baseObj = new GameObject($"TrunkBase_{i}");
            baseObj.transform.SetParent(this.transform);
            SpriteRenderer bSr = baseObj.AddComponent<SpriteRenderer>();
            bSr.sprite = pixelSprite;
            bSr.color = trunkBaseColor;
            bSr.sortingOrder = -6; // 压在身体后面
            trunkBases.Add(bSr);

            GameObject highObj = new GameObject($"TrunkHigh_{i}");
            highObj.transform.SetParent(baseObj.transform);
            SpriteRenderer hSr = highObj.AddComponent<SpriteRenderer>();
            hSr.sprite = pixelSprite;
            hSr.color = trunkHighlightColor;
            hSr.sortingOrder = -5;
            trunkHighlights.Add(hSr);
        }

        // 2. 全自动生成 3 根极细的红蓝数据线
        Material defaultMat = new Material(Shader.Find("Sprites/Default"));
        for (int c = 0; c < umbilicalPhysics.smallCordCount; c++)
        {
            GameObject lineObj = new GameObject($"SmallCord_{c}");
            lineObj.transform.SetParent(this.transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = defaultMat;
            lr.startColor = lr.endColor = cordColors[c % cordColors.Length];
            lr.startWidth = lr.endWidth = cordWidth;
            lr.positionCount = umbilicalPhysics.smallCordSegments;
            lr.sortingOrder = -7; // 压在最底层
            smallCordRenderers.Add(lr);
        }
    }

    void LateUpdate()
    {
        if (umbilicalPhysics == null) return;

        // 1. 刷新主干蜈蚣线
        for (int i = 0; i < umbilicalPhysics.trunkSegments; i++)
        {
            Vector2 pos = umbilicalPhysics.trunkPos[i];
            trunkBases[i].transform.position = pos;

            // 计算蜈蚣节段的扭动朝向
            Vector2 dir = Vector2.up;
            if (i < umbilicalPhysics.trunkSegments - 1) dir = (umbilicalPhysics.trunkPos[i + 1] - pos).normalized;
            else if (i > 0) dir = (pos - umbilicalPhysics.trunkPos[i - 1]).normalized;

            trunkBases[i].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);

            // 立体感缩放
            trunkBases[i].transform.localScale = new Vector3(trunkWidth, trunkHeight, 1f);
            trunkHighlights[i].transform.localPosition = new Vector3(0, trunkHeight * 0.15f, 0); // 高光倒角偏移
            trunkHighlights[i].transform.localScale = new Vector3(0.7f, 0.4f, 1f);
        }

        // 2. 刷新神经线
        for (int c = 0; c < umbilicalPhysics.smallCordCount; c++)
        {
            for (int i = 0; i < umbilicalPhysics.smallCordSegments; i++)
            {
                smallCordRenderers[c].SetPosition(i, umbilicalPhysics.smallCordsPos[c][i]);
            }
        }
    }
}