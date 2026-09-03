using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WebVirtualDisplayClient;

class RawInputHandler {
        private const ushort WM_INPUT = 0x00FF;
        private const ushort RIDEV_INPUTSINK = 0x00000100;
        private const ushort GENERIC_DESKTOP = 0x01;
        private const ushort MOUSE_IDENTIFIER = 0x02;

        private const int RIM_TYPEMOUSE = 0;
        private const int RID_INPUT = 0x10000003;

        public class RawMouseInputEventArgs : EventArgs {
                public RawMouseInputEventArgs(int deltaX, int deltaY)
                {
                        this.deltaX = deltaX;
                        this.deltaY = deltaY;
                }

                public int deltaX {get; set; }
                public int deltaY {get; set; }
        }


        // Non-nullable event 'rawMouseMovement' must contain a non-null value when exiting constructor.
        // Consider adding the 'required' modifier or declaring the event as nullable. [CS8618]

        public static event EventHandler<RawMouseInputEventArgs>? rawMouseMovement;

        public static void InitializeRawInput(HwndSource source)
        {
                source.AddHook(HwndHook);

                RAWINPUTDEVICE[] rawInputDevice = new RAWINPUTDEVICE[1];
                rawInputDevice[0].usUsagePage = GENERIC_DESKTOP;
                rawInputDevice[0].usUsage = MOUSE_IDENTIFIER;
                rawInputDevice[0].dwFlags = RIDEV_INPUTSINK;
                rawInputDevice[0].hwndTarget = source.Handle; 

                RegisterRawInputDevices(rawInputDevice, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
                if (msg == WM_INPUT) {
                        Task.Run(() => ProcessRawInput(lParam));
                }
                return IntPtr.Zero;
        }

        private static void ProcessRawInput(IntPtr lParam)
        {
                uint dwSize = 0;

                // Call once to get required buffer size
                GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                if (dwSize == 0) return;

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try {
                        if (GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint) Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize) {
                                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                                if (raw.header.dwType == RIM_TYPEMOUSE)
                                {
                                        int deltaX = raw.data.mouse.lLastX;
                                        int deltaY = raw.data.mouse.lLastY;

                                        rawMouseMovement?.Invoke(null, new RawMouseInputEventArgs(deltaX, deltaY));
                                }
                        }
                }
                finally
                {
                        Marshal.FreeHGlobal(buffer);
                }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
                public ushort usUsagePage;
                public ushort usUsage;
                public uint dwFlags;
                public IntPtr hwndTarget;
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
                public uint ulButtons;
                public uint ulRawButtons;
                public int lLastX;
                public int lLastY;
                public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTDATA
        {
                [FieldOffset(0)]
                public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
                public RAWINPUTHEADER header;
                public RAWINPUTDATA data;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
}
