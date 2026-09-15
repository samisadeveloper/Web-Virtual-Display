using System.Runtime.InteropServices;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.util;

class ScreenExtent {
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        public static Point GetScreenExtent() {
                int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);

                return new Point(){X = virtualLeft + virtualWidth, Y = 1080}; // Assume the max height extent is 1080 (TODO: change this)
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
}
