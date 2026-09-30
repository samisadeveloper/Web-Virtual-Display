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

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
                public Point pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
        }

        private const uint MK_LBUTTON = 0x0001; // make left mouse button

        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONUP = 0x0202; // Left button up
        private const int WM_LBUTTONDOWN = 0x0201; // Left button down

        private const int WM_RBUTTONUP = 0x0205; // right button up
        private const int WM_RBUTTONDOWN = 0x0204; // right button down

        private const uint WM_MOUSEWHEEL = 0x020A; // mouse wheel
        private const uint WM_VSCROLL = 0x0115; // scroll

        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public enum MouseEventType {
                LEFT_PRESSED,
                LEFT_RELEASED,
                RIGHT_PRESSED,
                RIGHT_RELEASED,
                SCROLL,
        }

        public struct MouseEventArgs {
                public MouseEventType type;
                public short delta;
        }

        public static EventHandler<MouseEventArgs>? mouseClickEvent;

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
                        switch ((uint) wParam) {
                                case WM_LBUTTONUP:
                                        mouseClickEvent?.Invoke(null, new MouseEventArgs {type = MouseEventType.LEFT_RELEASED});

                                        break;

                                case WM_LBUTTONDOWN:
                                        mouseClickEvent?.Invoke(null, new MouseEventArgs {type = MouseEventType.LEFT_PRESSED});

                                        break;

                                case WM_RBUTTONUP:
                                        mouseClickEvent?.Invoke(null, new MouseEventArgs {type = MouseEventType.RIGHT_RELEASED});

                                        break;

                                case WM_RBUTTONDOWN:
                                        mouseClickEvent?.Invoke(null, new MouseEventArgs {type = MouseEventType.RIGHT_PRESSED});

                                        break;

                                case WM_MOUSEWHEEL:
                                        MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                                        short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);

                                        mouseClickEvent?.Invoke(null, new MouseEventArgs {type = MouseEventType.SCROLL, delta = delta});

                                        break;
                        }
                }

                
                return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static void ClickWindowAt(IntPtr hWnd, Point point, MouseEventArgs mouseArgs) {
                MouseEventType type = mouseArgs.type;
        
                // Pack coordinates into a single 32-bit integer (LPARAM)
                // X-coordinate goes in the low-order word, Y-coordinate in the high-order word
                IntPtr lParam = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));
                IntPtr wParam = (IntPtr)MK_LBUTTON;

                switch (type) {
                        case MouseEventType.LEFT_PRESSED:
                                PostMessage(hWnd, WM_LBUTTONDOWN, wParam, lParam);
                                break;

                        case MouseEventType.LEFT_RELEASED:
                                PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                                break;

                        case MouseEventType.RIGHT_PRESSED:
                                PostMessage(hWnd, WM_RBUTTONDOWN, wParam, lParam);
                                break;

                        case MouseEventType.RIGHT_RELEASED:
                                PostMessage(hWnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
                                break;

                        case MouseEventType.SCROLL:
                                short delta = mouseArgs.delta;

                                wParam = new IntPtr((delta << 16) & 0xFFFF0000);

                                PostMessage(hWnd, WM_MOUSEWHEEL, wParam, lParam);

                                break;
                }
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
