using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ScreenRecorderLib;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;

// using Vpx.Net;
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
                        public IntPtr hwnd;
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

                private static Dictionary<IntPtr, WindowData> windowRegistry = new Dictionary<IntPtr, WindowData>();

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
                                id = (int) window.hwnd, // use the window's hwnd as the id but casted to a regular int
                                x = window.x,
                                y = window.y,
                                width = window.width,
                                height = window.height,
                        };

                        // movement will be updating if there's a matching hwnd but only the client has to determine that
                        windowMovement?.send(JsonSerializer.Serialize(data));
                }

                public static void updateWindow(WindowData window) {
                        sendWindowMovement(window);

                        if (!windowRegistry.ContainsKey(window.hwnd)) {
                                Task.Run(async () => {
                                                var pc = WebRTCClient.getPeerConnection();

                                                var encoderEndPoint = new Vp8NetVideoEncoderEndPoint();
                                                await encoderEndPoint.StartVideo();

                                                var track = new MediaStreamTrack(encoderEndPoint.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
                                                pc.addTrack(track);

                                                // currently diagnosing: why tf doesn't this EVER FIRE????!?!?!?!?!?!?!?
                                                encoderEndPoint.OnVideoSourceEncodedSample += (duration, sample) => {
                                                        Console.WriteLine($"\n\n\n[ENCODER OUT] SUCCESS! Packed {sample.Length} compressed bytes.");
                                                };

                                                encoderEndPoint.OnVideoSourceEncodedSample += pc.SendVideo;
                                                RecorderOptions options = new RecorderOptions {
                                                        OutputOptions = new OutputOptions { IsVideoFramePreviewEnabled = true },

                                                        SourceOptions = new SourceOptions {
                                                                RecordingSources = new List<RecordingSourceBase>{ new WindowRecordingSource(window.hwnd) }
                                                        }
                                                };

                                                Recorder recorder = Recorder.CreateRecorder(options);

                                                byte[]? frameBuffer = null;

                                                recorder.OnFrameRecorded += (sender, args) => {
                                                        // the data from the recorder is uncompressed 32-bit BGRA
                                                        // I thought it was compressed but that isn't true
                                                        // it's important to know

                                                        int stride = args.BitmapData.Stride;
                                                        int height = args.BitmapData.Height;
                                                        int width = args.BitmapData.Width;
                                                        int byteCount = Math.Abs(stride) * height;

                                                        if (frameBuffer == null || frameBuffer.Length != byteCount)
                                                                frameBuffer = new byte[byteCount];

                                                        Marshal.Copy(args.BitmapData.Data, frameBuffer, 0, byteCount);

                                                        var i420 = CodecsUtil.BgraToI420(frameBuffer, width, height, stride);
                                                        // ^^^ this should be valid I420 but I am not 100% certain idfk

                                                        encoderEndPoint.ExternalVideoSourceRawSample(
                                                                33, width, height, i420,
                                                                SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.I420
                                                        );
                                                };

                                                pc.OnVideoFormatsNegotiated += formats => {
                                                        encoderEndPoint.SetVideoSourceFormat(formats.First());

                                                        recorder.Record(Stream.Null);                                
                                                };
                                });
                        }

                        windowRegistry[window.hwnd] = window;
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
