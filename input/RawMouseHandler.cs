using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WebVirtualDisplayClient.input;

class RawMouseHandler {
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

        public static event EventHandler<RawMouseInputEventArgs>? rawMouseMovement;

        public static void InitializeRawInput(HwndSource source)
        {
                source.AddHook(HwndHook);

                // define a raw input device (this is a mouse in our case)
                RAWINPUTDEVICE[] rawInputDevice = new RAWINPUTDEVICE[1];
                rawInputDevice[0].usUsagePage = GENERIC_DESKTOP;
                rawInputDevice[0].usUsage = MOUSE_IDENTIFIER;
                rawInputDevice[0].dwFlags = RIDEV_INPUTSINK;
                rawInputDevice[0].hwndTarget = source.Handle; 

                // register the RID
                bool success = RegisterRawInputDevices(rawInputDevice, 1, (uint) Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                if (success) {
                        Console.WriteLine("Raw Mouse: successfully registered RID");
                } else {
                        Console.WriteLine("Raw Mouse: failed to register RID");
                }
        }

        private static IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
                if (msg == WM_INPUT) {
                        ProcessRawInput(lParam);
                        handled = true;
                }

                return IntPtr.Zero;
        }

        private static void ProcessRawInput(IntPtr lParam)
        {
                uint dwSize = 0;

                GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                if (dwSize == 0) {
                        Console.WriteLine("Raw Mouse: Failed to get input data!");
                        return;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try {
                        if (GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint) Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize) {
                                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                                if (raw.header.dwType == RIM_TYPEMOUSE)
                                {
                                        // get the delta X and Y of the mouse
                                        int deltaX = raw.data.mouse.lLastX;
                                        int deltaY = raw.data.mouse.lLastY;

                                        // invoke our raw mouse event
                                        rawMouseMovement?.Invoke(null, new RawMouseInputEventArgs(deltaX, deltaY));
                                }
                        }
                }
                finally
                {
                        Marshal.FreeHGlobal(buffer);
                }
        }

        public static void HideCursor() {
                uint[] cursorIds = {
                        32512, // OCR_NORMAL   (Default windows cursor)
                        32644, // OCR_SIZEWE   (Horizontal Resize)
                        32645, // OCR_SIZENS   (Vertical Resize)
                        32642, // OCR_SIZENWSE (Diagonal Resize)
                        32643, // OCR_SIZENESW (Diagonal Resize)
                        32646, // OCR_SIZEALL  (Move/Size All)
                        32513, // OCR_IBEAM    (Text cursor)
                        32649  // OCR_HAND     (Link hand)
                };

                foreach (uint id in cursorIds)
                {
                        // 1. Generate a brand new, unique blank cursor mask
                        IntPtr hAndMask = CreateBitmap(1, 1, 1, 1, Marshal.AllocHGlobal(1));
                        IntPtr hXorMask = CreateBitmap(1, 1, 1, 1, Marshal.AllocHGlobal(1));

                        ICONINFO iconInfo = new ICONINFO
                        {
                                fIcon = false,
                                xHotspot = 0,
                                yHotspot = 0,
                                hbmMask = hAndMask,
                                hbmColor = hXorMask
                        };

                        IntPtr blankCursor = CreateIconIndirect(ref iconInfo);

                        // 2. Overwrite this specific system cursor ID
                        if (blankCursor != IntPtr.Zero)
                        {
                                SetSystemCursor(blankCursor, id);
                        }
                }
        }

        public static void RestoreCursor() {
                // Forces Windows to reload the default cursors from the registry
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
                public bool fIcon;
                public int xHotspot;
                public int yHotspot;
                public IntPtr hbmMask;
                public IntPtr hbmColor;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateIconIndirect(ref ICONINFO piconinfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, IntPtr lpBits);

        // OCR_NORMAL is the standard Windows Arrow cursor ID
        private const uint OCR_NORMAL = 32512;
        private const uint SPI_SETCURSORS = 0x0057;

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
