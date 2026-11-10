using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public class RainWorldAtlasSlicer : MonoBehaviour
{
    [MenuItem("Tools/Slice Rain World Atlas")]
    public static void SliceAtlas()
    {
        // 1. 获取选中的贴图和文本文件（需要你在 Project 窗口中同时选中它们）
        Object[] selectedObjects = Selection.objects;
        Texture2D atlasTexture = null;
        TextAsset jsonText = null;

        foreach (Object obj in selectedObjects)
        {
            if (obj is Texture2D) atlasTexture = (Texture2D)obj;
            if (obj is TextAsset) jsonText = (TextAsset)obj;
        }

        if (atlasTexture == null || jsonText == null)
        {
            Debug.LogError("请同时选中 rainWorld.png 和 rainWorld.txt！");
            return;
        }

        string path = AssetDatabase.GetAssetPath(atlasTexture);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;

        // 确保贴图类型设置为 Sprite，模式为 Multiple
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.isReadable = true;

        // 2. 使用正则表达式解析看似 JSON 的文本结构
        // 匹配格式: "名字.png": { "frame": {"x":数字,"y":数字,"w":数字,"h":数字}
        string pattern = @"\""(.*?)\.png\""\s*:\s*\{\s*\""frame\""\s*:\s*\{\s*\""x\""\s*:\s*(\d+)\s*,\s*\""y\""\s*:\s*(\d+)\s*,\s*\""w\""\s*:\s*(\d+)\s*,\s*\""h\""\s*:\s*(\d+)";
        MatchCollection matches = Regex.Matches(jsonText.text, pattern);

        List<SpriteMetaData> newData = new List<SpriteMetaData>();

        foreach (Match match in matches)
        {
            string spriteName = match.Groups[1].Value;
            float x = float.Parse(match.Groups[2].Value);
            float y = float.Parse(match.Groups[3].Value);
            float w = float.Parse(match.Groups[4].Value);
            float h = float.Parse(match.Groups[5].Value);

            SpriteMetaData smd = new SpriteMetaData();
            smd.name = spriteName;
            // 注意：Unity 的 Y 轴是从下往上算的，而 JSON 通常是从上往下算的，需要反转 Y 轴坐标
            smd.rect = new Rect(x, atlasTexture.height - y - h, w, h);
            smd.alignment = 9; // Custom Pivot
            smd.pivot = new Vector2(0.5f, 0.5f); // 默认将轴心设置在中心
            
            newData.Add(smd);
        }

        // 3. 将划分好的数据应用到贴图上
        ti.spritesheet = newData.ToArray();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        
        Debug.Log($"拆分完成！共切分出 {newData.Count} 个 Sprite。");
    }
}