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

        private static void ResetConnection() {
                try {
                        peerConnection.close();
                } catch {}

                peerConnection = new RTCPeerConnection(new RTCConfiguration { iceServers = new List<RTCIceServer>() });
                localIceCandidates.Clear();
                remoteIceCandidates.Clear();

                Task.Run(async () => {
                        offer = peerConnection.createOffer();
                        await peerConnection.setLocalDescription(offer);

                        // Re-bind the ice candidate listener to the new instance
                        peerConnection.onicecandidate += (candidate) => {
                                if (!string.IsNullOrEmpty(candidate.candidate)) {
                                        localIceCandidates.Add(candidate.candidate);
                                }
                        };
                });
        }

        public static async Task<RTCDataChannel> createDataChannel() {
                return await peerConnection.createDataChannel("data-stream");
        }

        public async static Task initializeClient(CancellationToken stoppingToken) {
                peerConnection.onicecandidate += (candidate) => {
                        if (!string.IsNullOrEmpty(candidate.candidate)) {
                                localIceCandidates.Add(candidate.candidate);
                        }
                };

                peerConnection.onconnectionstatechange += (state) => {
                        Console.WriteLine($"WebRTC Connection State Changed: {state}");

                        if (state == RTCPeerConnectionState.disconnected || 
                                        state == RTCPeerConnectionState.failed || 
                                        state == RTCPeerConnectionState.closed) 
                        {
                                Console.WriteLine("\n\n\nPeer disconnected! Resetting WebRTC Client...");

                                ResetConnection();
                        }
                };

                offer = peerConnection.createOffer();

                await peerConnection.setLocalDescription(offer);
        }

        public static void RegisterSignalingRoutes(WebApplication app) {
                if (offer == null) throw new NullReferenceException("Offer not generated yet");

                // fix race condition with offer
                app.MapGet("/api/webrtc/offer", async () => {
                        int attempts = 0;
                        // Wait up to 2 seconds if the server is actively generating a new offer
                        while ((offer == null || peerConnection.signalingState != RTCSignalingState.have_local_offer) && attempts < 20) {
                                await Task.Delay(100);
                                attempts++;
                        }

                        if (offer == null) return Results.NotFound("Offer not ready yet");

                        return Results.Text(offer.sdp.ToString());
                });

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
