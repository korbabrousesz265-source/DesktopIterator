using UnityEngine;

public class PuppetDrag : MonoBehaviour
{
    private Vector3 offset;
    private Camera mainCam;
    private OracleArm oracleArm;

    // 屏幕边界
    private float minX, maxX, minY, maxY;

    void Start()
    {
        mainCam = Camera.main;
        oracleArm = FindAnyObjectByType<OracleArm>();
        CalculateScreenBounds();
    }

    // 计算屏幕的物理边界
    void CalculateScreenBounds()
    {
        float camHeight = mainCam.orthographicSize;
        float camWidth = camHeight * mainCam.aspect;

        // 【修改这里】将 1.5f 改小，比如改成 0.5f 或者直接 0f
        // 如果设置为 0f，人偶的中心点（蓝点）可以一直拖到屏幕的最边缘
        float padding = 0.5f;

        minX = mainCam.transform.position.x - camWidth + padding;
        maxX = mainCam.transform.position.x + camWidth - padding;
        minY = mainCam.transform.position.y - camHeight + padding;
        maxY = mainCam.transform.position.y + camHeight - padding;
    }

    // 当鼠标在 Box Collider 2D 上按下时触发
    void OnMouseDown()
    {
        // 记录鼠标点击位置与人偶中心点的坐标差，防止点击瞬间人偶瞬移
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        offset = transform.position - new Vector3(mousePos.x, mousePos.y, transform.position.z);
    }

    // 当鼠标按住并拖拽时触发
    void OnMouseDrag()
    {
        CalculateScreenBounds();
        Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 newPos = new Vector3(mousePos.x, mousePos.y, transform.position.z) + offset;

        // 核心魔法：使用 Mathf.Clamp 将坐标死死限制在刚才计算的屏幕边界内
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        newPos.y = Mathf.Clamp(newPos.y, minY, maxY);

        if (oracleArm != null && oracleArm.isActiveAndEnabled && oracleArm.puppetBody == transform)
            oracleArm.RequestDragPosition(newPos);
        else
            transform.position = newPos;
    }
}
