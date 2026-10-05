using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;
using WebVirtualDisplayClient.util;
using static WebVirtualDisplayClient.input.RawWindowHandler;
using static WebVirtualDisplayClient.util.MouseUtil;

namespace WebVirtualDisplayClient.input
{
    class WindowManager
    {
        private static SIPSorcery.Net.RTCDataChannel? mouseUpdate;
        private static SIPSorcery.Net.RTCDataChannel? windowMovement;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

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

            mouseClickEvent += onMouseClick;
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

        private static void onMouseClick(Object? sender, MouseUtil.MouseEventArgs mouseArgs) {
            MouseEventType clickType = mouseArgs.type;

            Point globalPoint = MouseHandler.GlobalMousePoint;
            Point point = new Point(){ X = globalPoint.X - extent.X, Y = globalPoint.Y };

            // find the window which was clicked on
            WindowData window = WindowRegistry.Values.FirstOrDefault(window => window.isInWindow(point));

            if (window.hwnd == 0) return;

            DragOffset offset = new DragOffset();
            offset.FromLeft = point.X - window.x;
            offset.FromRight = window.width - offset.FromLeft;
            offset.FromTop = point.Y - window.y;

            WindowCachedTrackData windowData = new WindowCachedTrackData() {
                dragOffset = offset,
                hwnd = window.hwnd,
                // window is fully beyond extent and should be virtual
                width = window.width,
                height = window.height,
            };

            // get mouse position relative to the window
            Point relativePoint = new Point() { X = point.X - window.x, Y = point.Y - window.y };

            // TODO: use a different number instead of a harcdoded one, it should scale depending on the monitor DPI or whatever
            if (relativePoint.Y < 50) {
                if (clickType.Equals(MouseEventType.LEFT_PRESSED)) {
                    RawWindowHandler.rawWindowHeld?.Invoke(null, windowData);
                } else if (clickType.Equals(MouseEventType.LEFT_RELEASED)) {
                    RawWindowHandler.rawWindowReleased?.Invoke(null, windowData);
                }
            }

            // TODO: there is a bug where you cannot pick up a window at all due to another window being ontop of it

            if (mouseArgs.type.Equals(MouseEventType.SCROLL)) {
                SetForegroundWindow(window.hwnd);

                // first capture the window position
                int x = window.rawX;
                int y = window.y;

                WindowHandler.MoveWindow(window.hwnd, new Point(){X = 0, Y = 0});

                ClickWindowAt(window.hwnd, relativePoint, mouseArgs);

                WindowHandler.MoveWindow(window.hwnd, new Point(){X = x, Y = y});
            } else {
                ClickWindowAt(window.hwnd, relativePoint, mouseArgs);
            }
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

        private static void recordWindow(WindowData window) {
            Task.Run(async () => {
                try {
                    var encoderEndPoint = new Vp8NetVideoEncoderEndPoint();
                    await encoderEndPoint.StartVideo();

                    string hwnd = window.hwnd.ToString();
                    uint lastSsrc = 0;
                    EncodedSampleDelegate? sendHandler = null;

                    Task Attach(RTCPeerConnection pc) {
                        // clean up whatever the previous connection left behind
                        if (sendHandler != null) encoderEndPoint.OnVideoSourceEncodedSample -= sendHandler;
                        WebRTCClient.ssrcToHwnd.TryRemove(lastSsrc, out _);

                        // fresh track per connection
                        var track = new MediaStreamTrack(encoderEndPoint.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
                        lastSsrc = track.Ssrc;

                        // register the SSRC before addTrack, since that triggers the offer munging
                        WebRTCClient.ssrcToHwnd[track.Ssrc] = hwnd;
                        pc.addTrack(track);

                        var stream = pc.VideoStreamList.Last();
                        sendHandler = (duration, sample) => stream.SendVideo(duration, sample);
                        encoderEndPoint.OnVideoSourceEncodedSample += sendHandler;

                        pc.OnVideoFormatsNegotiated += formats => {
                            encoderEndPoint.SetVideoSourceFormat(formats.First());
                        };

                        pc.onconnectionstatechange += state => {
                            if (state == RTCPeerConnectionState.connected)
                                encoderEndPoint.ForceKeyFrame();
                        };

                        return Task.CompletedTask;
                    }

                    // runs on the current connection and every future one, never concurrently with a reset
                    await WebRTCClient.OnConnection(Attach);

                    RecorderOptions options = new RecorderOptions {
                        OutputOptions = new OutputOptions { IsVideoFramePreviewEnabled = true },

                        SourceOptions = new SourceOptions {
                            RecordingSources = new List<RecordingSourceBase>{
                                new WindowRecordingSource(window.hwnd) {
                                    IsBorderRequired = false
                                }}
                        }
                    };

                    Recorder recorder = Recorder.CreateRecorder(options);

                    byte[]? frameBuffer = null;

                    recorder.OnFrameRecorded += (sender, args) => {
                        try {
                            int stride = args.BitmapData.Stride;
                            int width = args.BitmapData.Width;
                            int height = args.BitmapData.Height;

                            int paddedWidth = RoundUpToMultipleOf16(width);
                            int paddedHeight = RoundUpToMultipleOf16(height);

                            int byteCount = Math.Abs(stride) * height;

                            if (frameBuffer == null || frameBuffer.Length != byteCount)
                                frameBuffer = new byte[byteCount];

                            Marshal.Copy(args.BitmapData.Data, frameBuffer, 0, byteCount);

                            // convert at the REAL size, this is what's actually in frameBuffer
                            var i420 = ColorFormatConverter.BgraToI420(frameBuffer, width, height, stride);

                            // THEN pad up to the encoder's required size
                            var paddedi420 = ColorFormatConverter.PadI420(i420, width, height, paddedWidth, paddedHeight);

                            // and tell the encoder the size that matches paddedi420
                            encoderEndPoint.ExternalVideoSourceRawSample(
                                33, paddedWidth, paddedHeight, paddedi420,
                                SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.I420
                            );
                        } catch (Exception ex) {
                            Console.WriteLine($"Something went wrong while processing frame from recorder {ex.Message}");
                        }
                    };

                    recorder.Record(Stream.Null);
                } catch (Exception ex) {
                    Console.WriteLine($"recordWindow failed for {window.hwnd}: {ex}");
                }
            });
        }

        // refactor this later please
        private static int RoundUpToMultipleOf16(int value) => (value + 15) & ~15;

        public static void updateWindow(WindowData window) {
            sendWindowMovement(window);

            if (window.hwnd == 0) return;

            // TryAdd is atomic, so two threads can't both decide the window is new
            bool isNewWindow = WindowRegistry.TryAdd(window.hwnd, window);
            WindowRegistry[window.hwnd] = window;

            if (isNewWindow) {
                recordWindow(window);
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
