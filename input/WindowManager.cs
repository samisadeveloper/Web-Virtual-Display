using System.Text.Json;
using static WebVirtualDisplayClient.input.MouseHandler;

namespace WebVirtualDisplayClient.input
{
        class WindowManager
        {
                private static SIPSorcery.Net.RTCDataChannel? mouseUpdate;
                private static SIPSorcery.Net.RTCDataChannel? windowUpdate;

                public static async void initialize() {
                        mouseUpdate = await WebRTCClient.createDataChannel();
                        windowUpdate = await WebRTCClient.createDataChannel();
                }

                public static void sendWindowMovement() {
                        var data = new {channel = "window"}; // include the width, height and position of the window AND make sure to include the HWND so it can be updated
                }

                public static void updateWindowPixelData(IntPtr windowHWND) {
                        // send the pixel data of a specific window
                }

                public static void sendMouseMovement(Point point) {
                        var data = new {channel = "mouse", x = point.X, y = point.Y};

                        mouseUpdate?.send(JsonSerializer.Serialize(data));
                }

        }
}
