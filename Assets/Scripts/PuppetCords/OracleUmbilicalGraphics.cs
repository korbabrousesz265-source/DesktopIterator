using UnityEngine;

[DefaultExecutionOrder(60)]
public class OracleUmbilicalGraphics : MonoBehaviour
{
    public OracleUmbilical umbilicalPhysics;
    public Color trunkBaseColor = new Color(0.14f, 0.13f, 0.13f);
    public Color trunkHighlightColor = new Color(0.39f, 0.36f, 0.30f);
    [Min(0f)] public float trunkWidth = 0.066f;
    public Color[] cordColors =
    {
        new Color(0.46f, 0.36f, 0.28f),
        new Color(0.30f, 0.38f, 0.40f),
        new Color(0.41f, 0.27f, 0.25f)
    };
    [Min(0f)] public float cordWidth = 0.015f;

    private Material cableMaterial;
    private LineRenderer trunk;
    private LineRenderer trunkLight;
    private LineRenderer[] cords;

    private void Start()
    {
        cableMaterial = new Material(Shader.Find("Sprites/Default"));
        trunk = CreateLine("Trunk jacket", trunkWidth, trunkBaseColor, -16);
        trunkLight = CreateLine("Trunk highlight", trunkWidth * 0.26f, trunkHighlightColor, -15);
    }

    private LineRenderer CreateLine(string label, float width, Color color, int order)
    {
        GameObject lineObject = new GameObject(label);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = cableMaterial;
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.widthMultiplier = width;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 3;
        line.numCornerVertices = 4;
        line.sortingOrder = order;
        return line;
    }

    private void LateUpdate()
    {
        if (umbilicalPhysics == null || umbilicalPhysics.trunkPos == null)
            return;

        SetPath(trunk, umbilicalPhysics.trunkPos);
        SetPath(trunkLight, umbilicalPhysics.trunkPos);
        Vector2[][] paths = umbilicalPhysics.smallCordsPos;
        if (cords == null)
        {
            cords = new LineRenderer[paths.Length];
            for (int i = 0; i < cords.Length; i++)
            {
                Color color = cordColors != null && cordColors.Length > 0
                    ? cordColors[i % cordColors.Length] : trunkHighlightColor;
                cords[i] = CreateLine("Data cord " + (i + 1), cordWidth, color, -14);
            }
        }
        for (int i = 0; i < cords.Length; i++)
            SetPath(cords[i], paths[i]);
    }

    private static void SetPath(LineRenderer line, Vector2[] points)
    {
        line.positionCount = points.Length;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, points[i]);
    }

    private void OnDestroy()
    {
        if (cableMaterial != null)
            Destroy(cableMaterial);
    }
}
