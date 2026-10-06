using System.Collections.Concurrent;
using System.Text.Json;
using SIPSorcery.Net;
using WebVirtualDisplayClient.recording;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input
{
        class WindowManager
        {
                private static SIPSorcery.Net.RTCDataChannel? mouseUpdate;
                private static SIPSorcery.Net.RTCDataChannel? windowMovement;

                public struct WindowData {
                        public IntPtr hwnd;
                        public int rawX;
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

                private static readonly ConcurrentDictionary<IntPtr, WindowData> WindowRegistry = new();
                public static List<WindowData> Windows => WindowRegistry.Values.ToList();

                private static Point extent = ScreenExtent.GetScreenExtent();

                public static void initialize() {
                        // runs for the current connection and for every one created after a browser refresh
                        _ = WebRTCClient.OnConnection(SetupChannels);
                }

                private static async Task SetupChannels(RTCPeerConnection pc) {
                        var mouseChannel = await pc.createDataChannel("mouse");
                        var windowChannel = await pc.createDataChannel("window");

                        mouseUpdate = mouseChannel;
                        windowMovement = windowChannel;

                        // a freshly loaded browser knows nothing about existing windows, so resend them all
                        windowChannel.onopen += () => {
                                foreach (var w in Windows) sendWindowMovement(w);
                        };
                }

                private static void SendOn(SIPSorcery.Net.RTCDataChannel? channel, object data) {
                        if (channel == null || channel.readyState != RTCDataChannelState.open) return;
                        channel.send(JsonSerializer.Serialize(data));
                }

                private static void sendWindowMovement(WindowData window) {
                        // include the width, height and position of the window AND make sure to include the HWND so it can be updated

                        if (window.hwnd == IntPtr.Zero) return;

                        var data = new {
                                channel = "window",
                                id = (int) window.hwnd, // use the window's hwnd as the id but casted to a regular int
                                x = window.x,
                                y = window.y,
                                width = window.width,
                                height = window.height,
                        };

                        // movement will be updating if there's a matching hwnd but only the client has to determine that
                        SendOn(windowMovement, data);
                }

                public static void updateWindow(WindowData window) {
                        sendWindowMovement(window);

                        if (window.hwnd == 0) return;

                        bool isRecording = RecordingManager.HasRecording(window.hwnd);

                        WindowRegistry[window.hwnd] = window;

                        if (window.rawX + window.width > extent.X) {
                                if (!isRecording) {
                                        RecordingManager.RecordWindow(window.hwnd);
                                }
                        } else {
                                if (isRecording) {
                                        RecordingManager.EndRecording(window.hwnd);
                                        WindowRegistry.TryRemove(window.hwnd, out _);
                                }
                        }
                }

                public static void sendMouseMovement(Point point) {
                        var data = new {
                                channel = "mouse",
                                id = -1, // always assign an id of -1
                                x = point.X,
                                y = point.Y
                        };

                        SendOn(mouseUpdate, data);
                }
        }
}
