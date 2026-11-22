using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Rain World keeps its sprites in one white atlas (rainWorld.png) that is tinted by
// the room palette at draw time. The oracle arm reads four elements out of it:
//
//   Circle20          OracleGraphics.ArmJointGraphics / ArmBase joint discs
//   deerEyeB          bolt caps on the discs and on the base supports
//   MirosLegSmallPart toothed strut that hangs from every elbow
//   pixel             1x1 bar used for cogs, rods and the base supports
//
// This exporter crops exactly those rects out of the shipped atlas so the desktop
// arm can draw the original pixels instead of approximating them with meshes.
public static class IteratorArmSpriteExporter
{
    private const string AtlasTexturePath = "Assets/rainWorld.png";
    private const string AtlasDataPath = "Assets/rainWorld.txt";
    private const string OutputFolder = "Assets/Resources/IteratorArm";

    // Frame name inside rainWorld.txt, output file name.
    private static readonly (string Frame, string File)[] Sprites =
    {
        ("Circle20", "Circle20"),
        ("deerEyeB", "deerEyeB"),
        ("MirosLegSmallPart", "MirosLegSmallPart"),
        ("pixel", "pixel")
    };

    [MenuItem("Tools/Iterator Arm/Export Joint And Base Sprites")]
    public static void ExportArmSprites()
    {
        TextureImporter atlasImporter = AssetImporter.GetAtPath(AtlasTexturePath) as TextureImporter;
        if (atlasImporter == null)
        {
            Debug.LogError("Atlas texture not found at " + AtlasTexturePath);
            return;
        }

        TextAsset atlasData = AssetDatabase.LoadAssetAtPath<TextAsset>(AtlasDataPath);
        if (atlasData == null)
        {
            Debug.LogError("Atlas data not found at " + AtlasDataPath);
            return;
        }

        Dictionary<string, Rect> frames = ParseFrames(atlasData.text);

        // The atlas is imported as a sliced, non readable sprite sheet. Toggle
        // readability for the crop and restore the previous setting afterwards.
        bool wasReadable = atlasImporter.isReadable;
        atlasImporter.isReadable = true;
        atlasImporter.SaveAndReimport();

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasTexturePath);
        if (atlas == null)
        {
            atlasImporter.isReadable = wasReadable;
            atlasImporter.SaveAndReimport();
            Debug.LogError("Could not load the atlas texture.");
            return;
        }

        EnsureFolder(OutputFolder);

        foreach ((string frame, string file) in Sprites)
        {
            if (!frames.TryGetValue(frame, out Rect rect))
            {
                Debug.LogError("Frame missing from the atlas data: " + frame);
                continue;
            }

            Color[] pixels;
            try
            {
                pixels = atlas.GetPixels(
                    Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
            }
            catch (UnityException exception)
            {
                Debug.LogError("Atlas read failed for " + frame + ": " + exception.Message);
                continue;
            }

            Texture2D slice = new Texture2D(
                Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height),
                TextureFormat.RGBA32, false);
            slice.SetPixels(pixels);
            slice.Apply();

            string path = OutputFolder + "/" + file + ".png";
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(slice));
            Object.DestroyImmediate(slice);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path);
            Debug.Log("Exported " + frame + " -> " + path + " (" + rect.width + "x" + rect.height + ")");
        }

        atlasImporter = AssetImporter.GetAtPath(AtlasTexturePath) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.isReadable = wasReadable;
            atlasImporter.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Iterator arm sprites exported to " + OutputFolder);
    }

    private static Dictionary<string, Rect> ParseFrames(string json)
    {
        // Same layout the existing slicer reads:
        //   "Circle20.png": { "frame": {"x":173,"y":132,"w":20,"h":20}
        const string pattern =
            "\"(.*?)\\.png\"\\s*:\\s*\\{\\s*\"frame\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*(\\d+)\\s*,\\s*\"y\"\\s*:\\s*(\\d+)\\s*,\\s*\"w\"\\s*:\\s*(\\d+)\\s*,\\s*\"h\"\\s*:\\s*(\\d+)";
        MatchCollection matches = Regex.Matches(json, pattern);

        int atlasHeight = 512; // rainWorld.png is 464x512; the frame data is top-left based.
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasTexturePath);
        if (atlas != null)
            atlasHeight = atlas.height;

        Dictionary<string, Rect> frames = new Dictionary<string, Rect>();
        foreach (Match match in matches)
        {
            string name = match.Groups[1].Value;
            float x = float.Parse(match.Groups[2].Value);
            float y = float.Parse(match.Groups[3].Value);
            float w = float.Parse(match.Groups[4].Value);
            float h = float.Parse(match.Groups[5].Value);
            // Unity reads pixels bottom-up, TexturePacker writes top-down.
            frames[name] = new Rect(x, atlasHeight - y - h, w, h);
        }
        return frames;
    }

    private static void ConfigureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
