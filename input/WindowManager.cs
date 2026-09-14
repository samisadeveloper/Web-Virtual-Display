using System.Text.Json;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input
{
        class WindowManager
        {
                private static SIPSorcery.Net.RTCDataChannel? mouseUpdate;
                private static SIPSorcery.Net.RTCDataChannel? windowMovement;

                public struct WindowData {
                        public int hwnd;
                        public int x;
                        public int y;
                        public int width;
                        public int height;
                }

                public static async void initialize() {
                        mouseUpdate = await WebRTCClient.createDataChannel();
                        windowMovement = await WebRTCClient.createDataChannel();
                }

                // lets include the window movement as its own struct
                public static void sendWindowMovement(WindowData window) {
                        // include the width, height and position of the window AND make sure to include the HWND so it can be updated

                        var data = new {
                                channel = "window",
                                id = window.hwnd, // use the window's hwnd as the id
                                x = window.x,
                                y = window.y,
                                width = window.width,
                                height = window.height,
                        };

                        // movement will be updating if there's a matching hwnd but only the client has to determine that
                        windowMovement?.send(JsonSerializer.Serialize(data));
                }

                public static void updateWindowPixelData(IntPtr windowHWND) {
                        // send the pixel data of a specific window
                }

                public static void sendMouseMovement(Point point) {
                        var data = new {
                                channel = "mouse",
                                id = -1, // always assign an id of -1
                                x = point.X,
                                y = point.Y
                        };

                        mouseUpdate?.send(JsonSerializer.Serialize(data));
                }

        }
}
