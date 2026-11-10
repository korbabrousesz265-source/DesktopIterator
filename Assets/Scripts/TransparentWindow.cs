using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class TransparentWindow : MonoBehaviour
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    // 【新增】用于强制修改窗口位置和大小的 API
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private struct MARGINS
    {
        public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight;
    }

    const int GWL_STYLE = -16;
    const uint WS_POPUP = 0x80000000;
    const uint WS_VISIBLE = 0x10000000;

    const int GWL_EXSTYLE = -20;
    const uint WS_EX_LAYERED = 0x00080000;
    const uint WS_EX_TRANSPARENT = 0x00000020;

    // 【新增】表示将窗口置于最顶层的句柄
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private IntPtr hWnd;
    private Camera mainCam;
    private bool isClickThrough = false;

    void Start()
    {
        Application.runInBackground = true;
        mainCam = Camera.main;

        // 【新增】强制让 Unity 的内部渲染分辨率等于你物理显示器的分辨率
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        Screen.SetResolution(screenWidth, screenHeight, FullScreenMode.Windowed);

#if !UNITY_EDITOR
        hWnd = FindWindow(null, Application.productName);

        if (hWnd != IntPtr.Zero)
        {
            MARGINS margins = new MARGINS { cxLeftWidth = -1 };
            DwmExtendFrameIntoClientArea(hWnd, ref margins);

            SetWindowLong(hWnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);

            // 【新增】强制把窗口移动到屏幕左上角(0,0)，撑满宽高，并保持在所有窗口的最顶层
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, screenWidth, screenHeight, 0);
        }
#endif
    }

    void Update()
    {
#if !UNITY_EDITOR
        if (hWnd != IntPtr.Zero)
        {
            Vector2 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            if (hit != null)
            {
                if (isClickThrough)
                {
                    SetClickThrough(false);
                    isClickThrough = false;
                }
            }
            else
            {
                if (!isClickThrough)
                {
                    SetClickThrough(true);
                    isClickThrough = true;
                }
            }
        }
#endif
    }

    private void SetClickThrough(bool clickThrough)
    {
        uint exStyle = (uint)GetWindowLong(hWnd, GWL_EXSTYLE);
        if (clickThrough)
        {
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        }
    }
}