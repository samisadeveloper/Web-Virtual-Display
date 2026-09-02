// this will handle inputs and talk to the window manager and MAGIC

using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;

namespace WebVirtualDisplayClient;

class InputHandler : BackgroundService
{
        private static Point? lastPoint;
        private static bool allowRawInput = false;

        private static async void captureRawInputMouse(CancellationToken stoppingToken) {
                await Task.Run(() => {
                        // let the wizards listen to the mouser and talk to the method

                        RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                        rid[0].usUsagePage = 0x01;          // Generic Desktop Controls
                        rid[0].usUsage = 0x02;              // Mouse
                        rid[0].dwFlags = 0x00000100;        // RIDEV_INPUTSINK (Capture in background)
                        rid[0].hwndTarget = IntPtr.Zero;    // Bind to current thread context

                        if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                        {
                                throw new Exception("Could not register raw mouse device.");
                        }

                        MSG msg;
                        while (!stoppingToken.IsCancellationRequested)
                        {
                        // PeekMessage checks the queue for pending WM_INPUT events without blocking infinitely
                        if (PeekMessage(out msg, IntPtr.Zero, 0, 0, 1)) // 1 = PM_REMOVE
                        {
                        if (msg.message == 0x00FF) // WM_INPUT
                        {
                                ProcessRawInput(msg.lParam);
                        }

                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                        }

                        // Sleep for ~1ms to prevent maximum CPU pinning while waiting for input
                        Thread.Sleep(1);
                        }
                }, stoppingToken);
        }

        private static void ProcessRawInput(IntPtr lParam)
        {
                uint dwSize = 0;

                // Call once to get required buffer size
                GetRawInputData(lParam, 0x10000003, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                if (dwSize == 0) return;

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                        if (GetRawInputData(lParam, 0x10000003, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                        {
                                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                                if (raw.header.dwType == 0) // 0 = RIM_TYPEMOUSE
                                {
                                        // lLastX and lLastY contain the raw relative hardware movement deltas
                                        int deltaX = raw.data.mouse.lLastX;
                                        int deltaY = raw.data.mouse.lLastY;

                                        // Accumulate raw physical position tracking forever (even off-screen)
                                        // globalX += deltaX;
                                        // globalY += deltaY;
                                        
                                        Console.WriteLine($"raw deltas: {deltaX} {deltaY}");
                                        
                                        // Console.WriteLine($"Raw Delta: ({deltaX}, {deltaY}) | Tracked Multi-Screen Pos: ({globalX}, {globalY})");
                                }
                        }
                }
                finally
                {
                        Marshal.FreeHGlobal(buffer);
                }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                float maxWidth = GetMaxXCoordinate();

                captureRawInputMouse(stoppingToken);

                while (!stoppingToken.IsCancellationRequested) {
                        GetCursorPos(out Point point);

                        if ((point.X >= maxWidth - 1) || (point.X <= 0)) { // raw input can take over now
                        }

                        try {
                                await Task.Delay(TimeSpan.FromMilliseconds(33));
                        } catch (OperationCanceledException) {
                                break;
                        }
                }
        }

        // structs for raw input

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
                public ushort usUsagePage;
                public ushort usUsage;
                public uint dwFlags;
                public IntPtr hwndTarget;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
                public uint dwType;
                public uint dwSize;
                public IntPtr hDevice;
                public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
                public ushort usFlags;
                public uint ulButtons; // Includes combined button flags/data
                public uint ulRawButtons;
                public int lLastX;
                public int lLastY;
                public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTDATA
        {
                // The mouse data begins exactly at offset 0 of the data packet
                [FieldOffset(0)]
                public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
                public RAWINPUTHEADER header;
                public RAWINPUTDATA data; // This now cleanly exposes .data.mouse
        }

        // DLL inports for raw input
        
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        // type for regular mouse position

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
                public int X;
                public int Y;
                public float distance(Point point) { return Vector2.DistanceSquared(new Vector2(point.X, point.Y), new Vector2(this.X, this.Y)); }
        }

        // dll import for regular mouse position

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint); 

        // calculate maximum screen width

        private static int GetMaxXCoordinate() {
                int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);

                // This is the absolute maximum right-hand boundary edge of your combined screens
                return virtualLeft + virtualWidth;
        }

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

}
