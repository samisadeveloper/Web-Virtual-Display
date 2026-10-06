using System.IO;
using System.Runtime.InteropServices;
using ScreenRecorderLib;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;
using WebVirtualDisplayClient.util;

namespace WebVirtualDisplayClient.recording;

public class RecordingManager {
        private static int RoundUpToMultipleOf16(int value) => (value + 15) & ~15;

        public static void EndRecording((Recorder recorder, Vp8NetVideoEncoderEndPoint endpoint) recording) {
                Task.Run(async () => {
                        await recording.endpoint.CloseVideo();

                        recording.recorder.Stop();

                        Task Cleanup(RTCPeerConnection pc) {
                        return Task.CompletedTask;
                        }

                        await WebRTCClient.OnConnection(Cleanup);
                });
        }

        public static void RecordWindow(IntPtr hwnd) {
                Task.Run(async () => {
                        try {
                                var encoderEndPoint = new Vp8NetVideoEncoderEndPoint();
                                await encoderEndPoint.StartVideo();

                                string hwndString = hwnd.ToString();
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
                                WebRTCClient.ssrcToHwnd[track.Ssrc] = hwndString;
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
                                                        new WindowRecordingSource(hwnd) {
                                                                // IsBorderRequired = false
                                                        }}
                                        }
                                };

                                Recorder recorder = Recorder.CreateRecorder(options);
                                // RecordedWindows[hwnd] = (recorder, encoderEndPoint);

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
                                Console.WriteLine($"recordWindow failed for {hwnd}: {ex}");
                        }
                });
        }
}
