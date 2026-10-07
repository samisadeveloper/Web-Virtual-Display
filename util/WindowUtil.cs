using System.Text;
using System.Runtime.InteropServices;

namespace WebVirtualDisplayClient.util;

public class WindowUtil {
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int GetWindowTextLength(IntPtr hWnd);

    public static string GetWindowTitle(IntPtr hwnd) {
        int length = GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;

        StringBuilder sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
