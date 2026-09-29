using System.Runtime.InteropServices;

namespace WebVirtualDisplayClient.util;

public static class LayeredWindow
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const uint LWA_ALPHA = 0x2;

    // Use the Ptr variants on 64-bit. SetWindowLong alone is 32-bit only.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    /// alpha: 0 = fully invisible, 255 = normal. Try 1 first.
    /// clickThrough: adds WS_EX_TRANSPARENT so local mouse clicks pass through the window.
    public static bool Apply(IntPtr hwnd, byte alpha = 1, bool clickThrough = false)
    {
        if (hwnd == IntPtr.Zero) return false;

        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex |= WS_EX_LAYERED;
        if (clickThrough) ex |= WS_EX_TRANSPARENT;

        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        return SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
    }

    /// Restores a normal, opaque window.
    public static bool Reset(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex &= ~(long)(WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)ex);
        return true;
    }
}
