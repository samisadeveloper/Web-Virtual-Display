using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using ScreenRecorderLib;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;
using WebVirtualDisplayClient.util;

namespace WebVirtualDisplayClient.recording;

public class RecordingManager {
        private sealed class RecordingSession {
                public Recorder Recorder = null!;
                public MediaStreamTrack Track = null!;
                public Vp8NetVideoEncoderEndPoint Encoder = null!;
                public EventHandler<FrameRecordedEventArgs>? FrameHandler;
                public EncodedSampleDelegate? SendHandler;
                public uint LastSsrc;
                public volatile bool Ended;
                public RTCPeerConnection? CurrentPc;
        };

        private static readonly ConcurrentDictionary<IntPtr, RecordingSession> Sessions = new();

        public static bool HasRecording(IntPtr hwnd) => Sessions.ContainsKey(hwnd);

        private static int RoundUpToMultipleOf16(int value) => (value + 15) & ~15;

        private static void encodeFrame(ref Byte[] frameBuffer, FrameBitmapData bitmapData, Vp8NetVideoEncoderEndPoint encoder) {
                try {
                        int stride = bitmapData.Stride;
                        int width = bitmapData.Width;
                        int height = bitmapData.Height;

                        int paddedWidth = RoundUpToMultipleOf16(width);
                        int paddedHeight = RoundUpToMultipleOf16(height);

                        int byteCount = Math.Abs(stride) * height;

                        if (frameBuffer == null || frameBuffer.Length != byteCount) frameBuffer = new byte[byteCount];

                        Marshal.Copy(bitmapData.Data, frameBuffer, 0, byteCount);

                        // convert at the REAL size, this is what's actually in frameBuffer
                        var i420 = ColorFormatConverter.BgraToI420(frameBuffer, width, height, stride);

                        // THEN pad up to the encoder's required size
                        var paddedi420 = ColorFormatConverter.PadI420(i420, width, height, paddedWidth, paddedHeight);

                        // and tell the encoder the size that matches paddedi420
                        encoder.ExternalVideoSourceRawSample(
                                33, paddedWidth, paddedHeight, paddedi420,
                                SIPSorceryMedia.Abstractions.VideoPixelFormatsEnum.I420
                        );
                } catch (Exception ex) {
                        Console.WriteLine($"Something went wrong while processing frame from recorder {ex.Message}");
                }
        }

        private static void AttachVideoTrack(IntPtr hwnd, RTCPeerConnection peerConnection, MediaStreamTrack track, Vp8NetVideoEncoderEndPoint endpoint, ref EncodedSampleDelegate? sendHandler) {
                // register the SSRC before addTrack, since that triggers the offer munging
                WebRTCClient.ssrcToHwnd[track.Ssrc] = hwnd.ToString();
                peerConnection.addTrack(track);

                var stream = peerConnection.VideoStreamList.Last();

                sendHandler = (duration, sample) => stream.SendVideo(duration, sample);
                endpoint.OnVideoSourceEncodedSample += sendHandler;

                peerConnection.OnVideoFormatsNegotiated += formats => {
                        endpoint.SetVideoSourceFormat(formats.First());
                };

                peerConnection.onconnectionstatechange += state => {
                        if (state == RTCPeerConnectionState.connected)
                                endpoint.ForceKeyFrame();
                };
        }

        public static void EndRecording(IntPtr hwnd) {
                Task.Run(async () => {
                        if (!Sessions.TryRemove(hwnd, out var session)) return;

                        session.Ended = true;

                        session.Recorder.OnFrameRecorded -= session.FrameHandler;
                        session.Recorder.Stop();
                        session.Recorder.Dispose();

                        if (session.SendHandler != null) session.Encoder.OnVideoSourceEncodedSample -= session.SendHandler;
                        WebRTCClient.ssrcToHwnd.TryRemove(session.LastSsrc, out _);

                        if (session.CurrentPc != null && session.Track != null) {
                                session.CurrentPc.removeTrack(session.Track);
                        }

                        await session.Encoder.CloseVideo();
                });
        }

        public static void RecordWindow(IntPtr hwnd) {
                Task.Run(async () => {
                        try {
                                var session = new RecordingSession { Encoder = new Vp8NetVideoEncoderEndPoint() };
                                await session.Encoder.StartVideo();
                                Sessions[hwnd] = session;

                                Task Attach(RTCPeerConnection pc) {
                                        if (session.Ended) return Task.CompletedTask;

                                        // clean up whatever the previous connection left behind
                                        if (session.SendHandler != null) session.Encoder.OnVideoSourceEncodedSample -= session.SendHandler;
                                        WebRTCClient.ssrcToHwnd.TryRemove(session.LastSsrc, out _);

                                        // fresh track per connection
                                        var track = new MediaStreamTrack(session.Encoder.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
                                        session.LastSsrc = track.Ssrc;
                                        session.CurrentPc = pc;
                                        session.Track = track;

                                        AttachVideoTrack(hwnd, pc, track, session.Encoder, ref session.SendHandler);

                                        return Task.CompletedTask;
                                }

                                // runs on the current connection and every future one, never concurrently with a reset
                                await WebRTCClient.OnConnection(Attach);

                                RecorderOptions options = new RecorderOptions {
                                        OutputOptions = new OutputOptions { IsVideoFramePreviewEnabled = true },

                                        SourceOptions = new SourceOptions {
                                                RecordingSources = new List<RecordingSourceBase>{
                                                        new WindowRecordingSource(hwnd) {
                                                                IsBorderRequired = false
                                                        }}
                                        }
                                };

                                session.Recorder = Recorder.CreateRecorder(options);

                                byte[]? frameBuffer = [];

                                session.FrameHandler = (_, args) => {
                                        if (!session.Ended) encodeFrame(ref frameBuffer, args.BitmapData, session.Encoder);
                                };

                                session.Recorder.OnFrameRecorded += session.FrameHandler;
                                session.Recorder.Record(Stream.Null);

                        } catch (Exception ex) {
                                Console.WriteLine($"recordWindow failed for {hwnd}: {ex}");
                        }
                });
        }
}
