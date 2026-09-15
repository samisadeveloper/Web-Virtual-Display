using System.Runtime.InteropServices;
using System.Text;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input;

class RawWindowHandler
{
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private delegate void WinEventDelegate(
                IntPtr hWinEventHook,
                uint eventType,
                IntPtr hwnd, 
                int idObject,
                int idChild,
                uint dwEventThread,
                uint dwmsEventTime
        );

        public struct DragOffset {
                public int FromLeft;
                public int FromRight;
                public int FromTop;
        }

        public struct WindowCachedTrackData {
                public IntPtr hwnd;
                public DragOffset dragOffset;

                public int width;
                public int height;
        }

        private static WindowCachedTrackData? trackedWindow;

        public struct WindowTrackData {
                public WindowCachedTrackData cachedData;
                
                public int x;
                public int y;
        }

        public static EventHandler<WindowTrackData>? rawWindowMovement;
        public static EventHandler<WindowCachedTrackData>? rawWindowHeld;
        public static EventHandler<WindowCachedTrackData>? rawWindowReleased;

        private static WinEventDelegate? _hookDelegate;
        private static IntPtr _hookHandle = IntPtr.Zero;
        private static uint _hookThreadId;

        public static void initializeRawInput(CancellationToken stoppingToken) {
                _hookDelegate = new WinEventDelegate(WinEventProc);
                _hookThreadId = GetCurrentThreadId(); 

                // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
                _hookHandle = SetWinEventHook(
                        EVENT_SYSTEM_MOVESIZESTART, // listen for move/resize start
                        EVENT_OBJECT_LOCATIONCHANGE, // listen for continous movement
                        IntPtr.Zero, // not sure what this is
                        _hookDelegate, // our hook
                        0, // process ID (zero means all processes)
                        0, // thread ID (zero means all processes)
                        WINEVENT_OUTOFCONTEXT //| WINEVENT_SKIPOWNPROCESS // this would skip own process but is removed for debugging purposes
                );

                if (_hookHandle == IntPtr.Zero)
                {
                        Console.WriteLine("Failed to install hook!");
                        return;
                }

                stoppingToken.Register(() => {
                        // Post a quit message to the specific background thread running the loop

                        PostThreadMessage(_hookThreadId, 0x0012, IntPtr.Zero, IntPtr.Zero); // 0x0012 is WM_QUIT
                });

                // Establish a native Windows Message Loop to receive events
                MSG msg;
                while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                }

                // Cleanup if the loop exits
                if (_hookHandle != IntPtr.Zero)
                {
                        UnhookWinEvent(_hookHandle);
                }
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) {
                if (eventType == EVENT_SYSTEM_MOVESIZESTART) { // fired when the window starts moving
                        RECT rect = new RECT();
                        GetWindowRect(hwnd, ref rect);
                        
                        // calculate the width and height of the window right now
                        // so we don't have to compute it during the move loop
                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;

                        // compute offset
                        DragOffset offset = new DragOffset();

                        GetCursorPos(out Point cursor);

                        offset.FromLeft = cursor.X - rect.Left;
                        offset.FromRight = width - offset.FromLeft;
                        offset.FromTop = cursor.Y - rect.Top;

                        // start tracking a window
                        trackedWindow = new WindowCachedTrackData() {hwnd = hwnd, dragOffset = offset, width = width, height = height};

                        rawWindowHeld?.Invoke(null, (WindowCachedTrackData) trackedWindow);

                } else if (eventType == EVENT_SYSTEM_MOVESIZEEND) {
                        if (trackedWindow != null) {
                                rawWindowReleased?.Invoke(null, (WindowCachedTrackData) trackedWindow);
                        }

                        trackedWindow = null; // no longer tracking a window

                } else if (eventType == EVENT_OBJECT_LOCATIONCHANGE && hwnd == trackedWindow?.hwnd) { // we might not even need location change 
                        RECT rect = new RECT();
                        GetWindowRect(hwnd, ref rect);

                        int newWidth = rect.Right - rect.Left;
                        int newHeight = rect.Bottom - rect.Top;

                        // if the width and height change this is a resize, not a move.
                        if (trackedWindow?.width != newWidth) return;
                        if (trackedWindow?.height != newHeight) return;

                        int x = rect.Left;
                        int y = rect.Top;

                        WindowTrackData args = new WindowTrackData() {
                                x = x,
                                y = y,
                                cachedData = (WindowCachedTrackData) trackedWindow // this cast should be safe because hwnd exists but lets hope.
                        };

                        rawWindowMovement?.Invoke(null, args);
                }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
                public IntPtr hwnd;
                public uint message;
                public IntPtr wParam;
                public IntPtr lParam;
                public uint time;
                public System.Drawing.Point pt;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(
                uint eventMin,
                uint eventMax,
                IntPtr hmodWinEventProc, 
                WinEventDelegate lpfnWinEventProc,
                uint idProcess,
                uint idThread,
                uint dwFlags
        );

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
}
