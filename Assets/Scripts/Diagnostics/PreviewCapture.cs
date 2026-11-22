using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Build-time helper. The pet itself never uses it: when the player is launched
// with "-previewCapture <folder>" it writes one transparent frame and quits, so a
// change to the arm can be checked on the real transparent window instead of the
// editor game view. "-previewPuppet x,y" pins the puppet so the same run can
// capture an idle pose and a pose at the side of the screen.
public class PreviewCapture : MonoBehaviour
{
    public string outputDirectory;
    public string fileName = "capture.png";
    public float captureDelay = 1.4f;
    public float quitDelay = 0.6f;

    private Vector2? puppetPosition;
    private OracleArm arm;
    private float elapsed;
    private bool requested;

    private void Awake()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "-previewCapture")
                outputDirectory = arguments[i + 1];
            else if (arguments[i] == "-previewName")
                fileName = arguments[i + 1];
            else if (arguments[i] == "-previewPuppet")
                puppetPosition = ParseVector(arguments[i + 1]);
            else if (arguments[i] == "-previewDelay" &&
                     float.TryParse(arguments[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture,
                         out float delay))
                captureDelay = Mathf.Max(0.1f, delay);
        }

        if (string.IsNullOrEmpty(outputDirectory))
        {
            enabled = false;
            return;
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Preview capture folder unavailable: " + exception.Message);
            enabled = false;
        }
    }

    private static Vector2? ParseVector(string text)
    {
        string[] parts = text.Split(',');
        if (parts.Length != 2)
            return null;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            return null;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            return null;
        return new Vector2(x, y);
    }

    private void Start()
    {
        arm = FindFirstObjectByType<OracleArm>();
    }

    private void Update()
    {
        if (!enabled)
            return;

        // Keep the pose pinned while the arm settles.
        if (puppetPosition.HasValue && arm != null && arm.puppetBody != null)
        {
            Vector3 current = arm.puppetBody.position;
            arm.puppetBody.position = new Vector3(puppetPosition.Value.x, puppetPosition.Value.y, current.z);
        }

        elapsed += Time.unscaledDeltaTime;
        if (!requested && elapsed >= captureDelay)
        {
            requested = true;
            string path = Path.Combine(outputDirectory, fileName);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("Preview capture written to " + path);
        }
        else if (requested && elapsed >= captureDelay + quitDelay)
        {
            Application.Quit();
        }
    }
}

public static class PreviewCaptureBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] != "-previewCapture")
                continue;

            GameObject holder = new GameObject("Preview capture");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.AddComponent<PreviewCapture>();
            return;
        }
    }
}
