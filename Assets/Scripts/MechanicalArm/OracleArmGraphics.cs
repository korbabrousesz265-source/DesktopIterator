using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OracleArmGraphics : MonoBehaviour
{
    [Header("物理数据源")]
    public OracleArm oracleArm;

    [Header("原版《雨世界》色盘")]
    public Color shadowColor = new Color(0.08f, 0.09f, 0.12f);
    public Color baseMetalColor = new Color(0.25f, 0.28f, 0.32f);
    public Color highlightColor = new Color(0.65f, 0.7f, 0.75f);
    public Color pipeColor = new Color(0.05f, 0.05f, 0.06f);

    [Header("尺寸与贴图")]
    public Sprite circleSprite;
    public float rootThickness = 0.8f;
    public float tipThickness = 0.25f;

    private MeshFilter meshFilter;
    private Mesh armMesh;
    private List<SpriteRenderer> jointOuter = new List<SpriteRenderer>();
    private List<SpriteRenderer> jointMid = new List<SpriteRenderer>();
    private List<SpriteRenderer> jointHole = new List<SpriteRenderer>();
    private List<LineRenderer> hydraulicPipes = new List<LineRenderer>();

    private Sprite pixelSprite;
    private SpriteRenderer sledgeShadow, sledgeMain, sledgeGroove, sledgeHighlight;

    // === 新增：手肘处的柔性弧形盖板 ===
    private SpriteRenderer elbowShroudShadow, elbowShroudMain, elbowShroudHighlight;

    void Start()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        meshFilter = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default"));
        mr.sortingOrder = -10;

        armMesh = new Mesh();
        meshFilter.mesh = armMesh;

        Texture2D tex = Texture2D.whiteTexture;
        pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        sledgeShadow = CreateRect("SledgeShadow", shadowColor, -14);
        sledgeMain = CreateRect("SledgeMain", baseMetalColor, -13);
        sledgeGroove = CreateRect("SledgeGroove", Color.black, -12);
        sledgeHighlight = CreateRect("SledgeHighlight", highlightColor, -11);

        // === 初始化手肘弧形盖板 (层级覆盖在手臂网格之上) ===
        elbowShroudShadow = CreateCircle("ElbowShroudShadow", shadowColor, -9);
        elbowShroudMain = CreateCircle("ElbowShroudMain", baseMetalColor, -8);
        elbowShroudHighlight = CreateCircle("ElbowShroudHighlight", highlightColor, -7);

        if (oracleArm == null) return;

        int nodeCount = oracleArm.segmentLengths.Length + 1;

        for (int i = 0; i < nodeCount; i++)
        {
            // 刚性齿轮层级降低，以便被手肘的柔性盖板遮挡
            jointOuter.Add(CreateCircle($"JointOuter_{i}", shadowColor, -11));
            jointMid.Add(CreateCircle($"JointMid_{i}", baseMetalColor, -10));
            jointHole.Add(CreateCircle($"JointHole_{i}", Color.black, -5)); // 黑洞永远在最顶层
        }

        for (int i = 0; i < oracleArm.segmentLengths.Length; i++)
        {
            LineRenderer pipeLr = new GameObject($"Pipe_{i}").AddComponent<LineRenderer>();
            pipeLr.transform.SetParent(this.transform);
            pipeLr.material = mr.material;
            pipeLr.startColor = pipeLr.endColor = pipeColor;
            pipeLr.sortingOrder = -6;
            pipeLr.positionCount = 2;
            hydraulicPipes.Add(pipeLr);
        }
    }

    SpriteRenderer CreateCircle(string name, Color c, int order)
    {
        SpriteRenderer sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(this.transform);
        sr.sprite = circleSprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    SpriteRenderer CreateRect(string name, Color c, int order)
    {
        SpriteRenderer sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(this.transform);
        sr.sprite = pixelSprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    void LateUpdate()
    {
        if (oracleArm == null || oracleArm.joints == null || oracleArm.joints.Length < 2) return;

        int nodeCount = oracleArm.joints.Length;
        int boneCount = nodeCount - 1;

        // 1. 渲染滑车基座 (保持不变)
        int wallIndex = Mathf.FloorToInt(oracleArm.currentTrackPos) % 4;
        float angle = 0f;
        if (wallIndex == 0) angle = 0f; else if (wallIndex == 1) angle = -90f; else if (wallIndex == 2) angle = 180f; else if (wallIndex == 3) angle = 90f;
        Quaternion baseRot = Quaternion.Euler(0, 0, angle);
        Vector3 basePos = oracleArm.joints[0];
        float bW = rootThickness * 5.5f, bH = rootThickness * 2.8f;
        sledgeShadow.transform.position = basePos + baseRot * new Vector3(0, -rootThickness * 0.2f, 0); sledgeShadow.transform.rotation = baseRot; sledgeShadow.transform.localScale = new Vector3(bW, bH, 1f);
        sledgeMain.transform.position = basePos + baseRot * new Vector3(0, -rootThickness * 0.2f, 0); sledgeMain.transform.rotation = baseRot; sledgeMain.transform.localScale = new Vector3(bW * 0.92f, bH * 0.85f, 1f);
        sledgeGroove.transform.position = basePos; sledgeGroove.transform.rotation = baseRot; sledgeGroove.transform.localScale = new Vector3(bW * 0.92f, bH * 0.15f, 1f);
        sledgeHighlight.transform.position = basePos + baseRot * new Vector3(0, -bH * 0.4f, 0); sledgeHighlight.transform.rotation = baseRot; sledgeHighlight.transform.localScale = new Vector3(bW * 0.92f, bH * 0.08f, 1f);

        // ==========================================
        // 2. 差异化装甲网格渲染
        // ==========================================
        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();
        int vertIndex = 0;

        for (int i = 0; i < boneCount; i++)
        {
            Vector3 p1 = oracleArm.joints[i];
            Vector3 p2 = oracleArm.joints[i + 1];
            Vector3 dir = (p2 - p1).normalized;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0);

            float t1 = Mathf.Lerp(rootThickness, tipThickness, (float)i / boneCount);
            float t2 = Mathf.Lerp(rootThickness, tipThickness, (float)(i + 1) / boneCount);

            float dist = Vector3.Distance(p1, p2);

            // 默认的刚性收缩倒角 (暴露出底下的齿轮)
            float taperStart = Mathf.Min(dist * 0.15f, rootThickness * 0.6f);
            float taperEnd = taperStart;

            // 【终极细节：区分柔性与刚性】
            // 第一节和第二节之间的手肘是“连续包裹”的，所以我们在手肘处的网格坚决不收缩！
            if (i == 0) taperEnd = 0f;   // 第一节靠近手肘的地方不收缩
            if (i == 1) taperStart = 0f; // 第二节靠近手肘的地方不收缩

            Vector3[] stations = { p1 + dir * taperStart, p1 + dir * taperStart, p2 - dir * taperEnd, p2 - dir * taperEnd };
            if (i == 0) stations[1] = p1 + dir * Mathf.Min(dist * 0.2f, rootThickness * 0.5f);
            else if (i == 1) stations[2] = p2 - dir * Mathf.Min(dist * 0.2f, rootThickness * 0.5f);
            else { stations[1] = p1 + dir * taperStart; stations[2] = p2 - dir * taperEnd; }

            float[] widths = { t1 * 0.35f, t1, t2, t2 * 0.35f };

            for (int s = 0; s < 4; s++)
            {
                Vector3 center = stations[s];
                float w = widths[s];
                vertices.Add(center + perp * w);
                vertices.Add(center + perp * (w * 0.5f));
                vertices.Add(center - perp * (w * 0.2f));
                vertices.Add(center - perp * w);
                colors.Add(shadowColor); colors.Add(highlightColor); colors.Add(baseMetalColor); colors.Add(shadowColor);
            }

            for (int seg = 0; seg < 3; seg++)
            {
                int rowStart = vertIndex + seg * 4;
                for (int col = 0; col < 3; col++)
                {
                    int v00 = rowStart + col; int v01 = rowStart + col + 1;
                    int v10 = rowStart + 4 + col; int v11 = rowStart + 4 + col + 1;
                    triangles.Add(v00); triangles.Add(v01); triangles.Add(v10);
                    triangles.Add(v10); triangles.Add(v01); triangles.Add(v11);
                }
            }
            vertIndex += 16;

            hydraulicPipes[i].startWidth = t1 * 0.12f; hydraulicPipes[i].endWidth = t2 * 0.12f;
            hydraulicPipes[i].SetPosition(0, p1 + dir * (t1 * 0.4f) + perp * (t1 * 0.45f));
            hydraulicPipes[i].SetPosition(1, p2 - dir * (t2 * 0.4f) + perp * (t2 * 0.45f));
        }

        armMesh.Clear(); armMesh.SetVertices(vertices); armMesh.SetColors(colors); armMesh.SetTriangles(triangles, 0);

        // ==========================================
        // 3. 差异化齿轮与盖板同步
        // ==========================================
        for (int i = 0; i < nodeCount; i++)
        {
            if (i == nodeCount - 1)
            {
                jointOuter[i].enabled = jointMid[i].enabled = jointHole[i].enabled = false;
                continue;
            }

            Vector3 pos = oracleArm.joints[i];
            float t = Mathf.Lerp(rootThickness, tipThickness, (float)i / nodeCount);

            if (i == 1) // 【手肘关节：柔性盖板 + 半隐藏齿轮】
            {
                jointOuter[i].enabled = false; // 彻底隐藏外围的巨大黑圈
                jointMid[i].enabled = true;
                jointHole[i].enabled = true;

                // 内部齿轮保留，被外部装甲缝隙透出来
                jointMid[i].transform.localScale = Vector3.one * (t * 1.6f);
                jointHole[i].transform.localScale = Vector3.one * (t * 0.6f);

                // 同步外部弧形盖板，完美补齐两条直臂折叠时的锐角缝隙，形成圆弧！
                elbowShroudShadow.transform.position = pos;
                elbowShroudShadow.transform.localScale = Vector3.one * (t * 2.0f);
                elbowShroudMain.transform.position = pos;
                elbowShroudMain.transform.localScale = Vector3.one * (t * 1.9f);

                // 提取第一节手臂的方向，让盖板的高光和手臂高光完美对接
                Vector3 dir0 = (oracleArm.joints[1] - oracleArm.joints[0]).normalized;
                Vector3 perp0 = new Vector3(-dir0.y, dir0.x, 0);
                elbowShroudHighlight.transform.position = pos + perp0 * (t * 0.5f);
                elbowShroudHighlight.transform.localScale = Vector3.one * (t * 0.9f);
            }
            else // 【手腕/基座关节：刚性折叠 + 裸露齿轮】
            {
                jointOuter[i].enabled = jointMid[i].enabled = jointHole[i].enabled = true;
                jointOuter[i].transform.localScale = Vector3.one * (t * 2.4f);
                jointMid[i].transform.localScale = Vector3.one * (t * 1.5f);
                jointHole[i].transform.localScale = Vector3.one * (t * 0.6f);
            }

            jointOuter[i].transform.position = jointMid[i].transform.position = jointHole[i].transform.position = pos;
        }
    }
}