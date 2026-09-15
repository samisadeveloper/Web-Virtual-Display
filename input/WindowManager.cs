using System.Text.Json;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.input.RawWindowHandler;
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

                        public bool isInWindow(Point point) {
                                if (point.X >= x && point.X <= x + width) {
                                        if (point.Y >= y && point.Y <= y + height) {
                                                return true;
                                        }
                                }

                                return false;
                        }
                }

                private static Dictionary<int, WindowData> windowRegistry = new Dictionary<int, WindowData>();
                private static Point extent = ScreenExtent.GetScreenExtent();

                public static async void initialize() {
                        mouseUpdate = await WebRTCClient.createDataChannel();
                        windowMovement = await WebRTCClient.createDataChannel();

                        mouseClickEvent += onMouseClick;
                }

                private static void onMouseClick(Object? sender, MouseEventType clickType) {
                        Point globalPoint = MouseHandler.GlobalMousePoint;
                        Point point = new Point(){ X = globalPoint.X - extent.X, Y = globalPoint.Y };

                        // find the window which was clicked on
                        WindowData window = windowRegistry.Values.FirstOrDefault(window => window.isInWindow(point));

                        if (window.hwnd == 0) return;

                        // compute the drag offset (this might need changing)
                        DragOffset offset = new DragOffset();
                        offset.FromLeft = point.X - window.x;
                        offset.FromRight = window.width - offset.FromLeft;
                        offset.FromTop = point.Y - window.y;

                        WindowCachedTrackData windowData = new WindowCachedTrackData() {
                                dragOffset = offset,
                                hwnd = window.hwnd,
                                width = window.width,
                                height = window.height,
                        };

                        if (clickType.Equals(MouseEventType.PRESSED)) {
                                RawWindowHandler.rawWindowHeld?.Invoke(null, windowData);
                        } else if (clickType.Equals(MouseEventType.RELEASED)) {
                                RawWindowHandler.rawWindowReleased?.Invoke(null, windowData);
                        }
                }

                private static void sendWindowMovement(WindowData window) {
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

                private static void updateWindowPixelData(IntPtr windowHWND) {
                        // send the pixel data of a specific window
                }

                public static void updateWindow(WindowData window) {
                        windowRegistry[window.hwnd] = window;

                        sendWindowMovement(window);
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
