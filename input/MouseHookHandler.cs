using System.Runtime.InteropServices;
using WebVirtualDisplayClient.input;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.util.MouseUtil;

public class MouseHookHandler {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        private static Point extent = ScreenExtent.GetScreenExtent();

        public MouseHookHandler()
        {
                _proc = HookCallback;
        }

        public void Start()
        {
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        }

        public void Stop()
        {
                UnhookWindowsHookEx(_hookID);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
                if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                        MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                        // if the mouse is not out of bounds carry on
                        if (MouseHandler.GlobalMousePoint.X < extent.X) return CallNextHookEx(_hookID, nCode, wParam, lParam);

                        foreach (WindowManager.WindowData window in WindowManager.Windows) {
                                IntPtr hwnd = window.hwnd;

                                if (GetWindowRect(hwnd, out RECT rect)) {
                                        if (hookStruct.pt.x >= rect.Left && hookStruct.pt.x <= rect.Right && hookStruct.pt.y >= rect.Top && hookStruct.pt.y <= rect.Bottom) {
                                                MouseUtil.mouseClickEvent?.Invoke(null, MouseEventType.PRESSED);

                                                return (IntPtr) 1; // swallow click event
                                        }
                                }
                        }
                }

                // Pass the event along to the rest of the OS if it's not our target
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #region Win32 API Definitions

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
                public POINT pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
                public int Left; public int Top; public int Right; public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion
}
