using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace WebVirtualDisplayClient.util;

class MouseUtil {
        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
                public int X;
                public int Y;

                public void Add(int x, int y) {
                        this.X += x;
                        this.Y += y;
                }

                // TODO: we should probably return which axis(s) the point is beyond extent in 
                // EX: the point may be beyont the X extent AND the Y extent or the point may be beyond the X axis in the negative direction
                public bool BeyondExtent(Point extent) {
                        return ((this.X >= extent.X - 1));
                }

                public float Distance(Point point) { return Vector2.DistanceSquared(new Vector2(point.X, point.Y), new Vector2(this.X, this.Y)); }
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONUP = 0x0202; // Left button up
        private const int WM_LBUTTONDOWN = 0x0201; // Left button down

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public enum MouseEventType {
                PRESSED,
                RELEASED
        }

        public static EventHandler<MouseEventType>? mouseClickEvent;

        public static void StartMouseHook()
        {
                _hookID = SetHook(_proc);
        }

        public static void StopMouseHook()
        {
                UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
                using (Process curProcess = Process.GetCurrentProcess())
                        using (ProcessModule curModule = curProcess.MainModule)
                        {
                                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                        }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
                // Check if the event is valid and matches Mouse1 Release
                if (nCode >= 0) {
                        if (wParam == (IntPtr) WM_LBUTTONUP) {
                                mouseClickEvent?.Invoke(null, MouseEventType.RELEASED);
                        } else if (wParam == (IntPtr) WM_LBUTTONDOWN) {
                                mouseClickEvent?.Invoke(null, MouseEventType.PRESSED);
                        }
                }

                
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint); 

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
}
