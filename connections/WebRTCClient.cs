using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;

namespace WebVirtualDisplayClient;

class WebRTCClient {
        private static RTCPeerConnection peerConnection = new RTCPeerConnection(new RTCConfiguration { iceServers = new List<RTCIceServer>() });
        private static RTCSessionDescriptionInit? offer;

        private static ConcurrentQueue<String> remoteIceCandidates = new ConcurrentQueue<string>();
        private static ConcurrentBag<String> localIceCandidates = new ConcurrentBag<string>();

        public static async Task<RTCDataChannel> createDataChannel() {
                return await peerConnection.createDataChannel("data-stream");
        }

        public async static Task initializeClient(CancellationToken stoppingToken) {
                // RTCDataChannel dataChannel = await peerConnection.createDataChannel("data-stream");
                //
                // dataChannel.onopen += () => {
                        // Console.WriteLine("\n\n\nData stream was opened");
                        // // TODO: move this and stream actual real data
                        // _ = Task.Run(async () => {
                        //         Console.WriteLine("Data channel is now open!");
                        //
                        //         int counter = 0;
                        //
                        //         while (dataChannel.readyState == RTCDataChannelState.open && !stoppingToken.IsCancellationRequested) {
                        //                 dataChannel.send($"Hello from C# background worker! Count: {counter++}");
                        //                 await Task.Delay(1000); // Send data every second
                        //         }
                        // });
                // };

                // dataChannel.onclose += () => Console.WriteLine("Browser disconnected.");

                peerConnection.onicecandidate += (candidate) => {
                        if (!string.IsNullOrEmpty(candidate.candidate)) {
                                localIceCandidates.Add(candidate.candidate);
                        }
                };

                offer = peerConnection.createOffer();

                await peerConnection.setLocalDescription(offer);
        }

        public static void RegisterSignalingRoutes(WebApplication app) {
                if (offer == null) throw new NullReferenceException("Offer not generated yet");

                app.MapGet("/api/webrtc/offer", () => Results.Text(offer.sdp.ToString()));

                app.MapPost("/api/webrtc/answer", async (HttpContext ctx, IOptions<JsonOptions> jsonOptions) => {
                        string body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

                        RTCSessionDescriptionInit? answerPayload;
                        try {
                                answerPayload = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(body, jsonOptions.Value.SerializerOptions);
                        } catch (Exception ex) {
                                Console.WriteLine($"ANSWER DESERIALIZE FAILED: {ex}");
                                return Results.BadRequest(ex.Message);
                        }

                        if (answerPayload == null) {
                                return Results.BadRequest("null payload");
                        }

                        answerPayload.type = RTCSdpType.answer;
                        peerConnection.setRemoteDescription(answerPayload);
                        return Results.Ok();
                });

                app.MapGet("/api/webrtc/ice", () => Results.Json(localIceCandidates));

                app.MapPost("/api/webrtc/ice", async (HttpContext ctx) => {
                        string body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

                        RTCIceCandidateInit? icePayload;

                        try {
                                icePayload = System.Text.Json.JsonSerializer.Deserialize<RTCIceCandidateInit>(body);
                        } catch (Exception ex) { // failed to deserialize the payload
                                Console.WriteLine($"ICE DESERIALIZE FAILED: {ex}");
                                return Results.BadRequest(ex.Message);
                        }

                        // passes ICE 
                        if (icePayload != null && !string.IsNullOrEmpty(icePayload.candidate)) {
                                peerConnection.addIceCandidate(icePayload);
                                return Results.Ok();
                        } else {
                                return Results.BadRequest();
                        }
                });
        }
}
