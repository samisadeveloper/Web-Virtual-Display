using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;
using SIPSorcery.Net;
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

                private static readonly Dictionary<IntPtr, WindowData> WindowRegistry = new Dictionary<IntPtr, WindowData>();
                public static List<WindowData> Windows => WindowRegistry.Values.ToList();

                private static Point extent = ScreenExtent.GetScreenExtent();

                public static async void initialize() {
                        mouseUpdate = await WebRTCClient.createDataChannel();
                        windowMovement = await WebRTCClient.createDataChannel();

                        mouseClickEvent += onMouseClick;
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
                                if (clickType.Equals(MouseEventType.PRESSED)) {
                                        RawWindowHandler.rawWindowHeld?.Invoke(null, windowData);
                                } else if (clickType.Equals(MouseEventType.RELEASED)) {
                                        RawWindowHandler.rawWindowReleased?.Invoke(null, windowData);
                                }
                        }

                        // TODO: there is a bug where you cannot pick up a window at all due to another window being ontop of it
                        // TODO: scrolling actually does work but it's quite finicky and it seems like it requires the scrollbox to be within window boundaries

                        if (mouseArgs.type.Equals(MouseEventType.SCROLL)) {
                                SetCursorPos(120, 120);
                                SetForegroundWindow(window.hwnd);

                                WindowHandler.SetWindowPos(window.hwnd, IntPtr.Zero, 0, 0, 0, 0, WindowHandler.SWP_NOSIZE | WindowHandler.SWP_NOACTIVATE);
                                
                                // ClickWindowAt(window.hwnd, new Point(){X = 120, Y = 120}, mouseArgs);
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
                        windowMovement?.send(JsonSerializer.Serialize(data));
                }

                private static void recordWindow(WindowData window) {
                        Task.Run(async () => {
                                var pc = WebRTCClient.getPeerConnection();
                                
                                var encoderEndPoint = new Vp8NetVideoEncoderEndPoint();
                                await encoderEndPoint.StartVideo();

                                var track = new MediaStreamTrack(encoderEndPoint.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);

                                WebRTCClient.ssrcToHwnd[track.Ssrc] = window.hwnd.ToString();

                                pc.addTrack(track);

                                var stream = pc.VideoStreamList.Last();
                                encoderEndPoint.OnVideoSourceEncodedSample += (duration, sample) => stream.SendVideo(duration, sample);

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

                                                // convert at the REAL size — this is what's actually in frameBuffer
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

                                pc.OnVideoFormatsNegotiated += formats => {
                                        encoderEndPoint.SetVideoSourceFormat(formats.First());
                                };
                        });
                }

                // refactor this later please
                private static int RoundUpToMultipleOf16(int value) => (value + 15) & ~15;

                public static void updateWindow(WindowData window) {
                        sendWindowMovement(window);

                        if (window.hwnd == 0) return;

                        bool isNewWindow = !WindowRegistry.ContainsKey(window.hwnd);

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

                        mouseUpdate?.send(JsonSerializer.Serialize(data));
                }

        }
}
