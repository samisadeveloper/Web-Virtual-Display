using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;
using WebVirtualDisplayClient.input;

namespace WebVirtualDisplayClient;

class WebRTCClient {
        private static RTCPeerConnection peerConnection = new RTCPeerConnection(new RTCConfiguration { iceServers = new List<RTCIceServer>() });
        private static RTCSessionDescriptionInit? offer;

        private static ConcurrentQueue<String> remoteIceCandidates = new ConcurrentQueue<string>();
        private static ConcurrentBag<String> localIceCandidates = new ConcurrentBag<string>();

        public static RTCPeerConnection getPeerConnection() {
                return peerConnection;
        }

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

                
                peerConnection.onnegotiationneeded += async () => {
                        offer = peerConnection.createOffer();

                        string rawSdp = offer.sdp;

                        // get the most recent window in the WM
                        WindowManager.WindowData window = WindowManager.Windows.Last();

                        // make sure the window actually exists so we can 
                        // embed the window handle in the SDP
                        if (window.hwnd != 0) {
                                string msidLine = $"a=mid:1\r\na=msid:{window.hwnd} video-track-0";
                                string modifiedSdp = rawSdp.Replace("a=mid:1", msidLine);

                                // create a new offer with new SDP
                                offer = new RTCSessionDescriptionInit {
                                        type = RTCSdpType.offer,
                                        sdp = modifiedSdp
                                };

                                // set this new offer
                                await peerConnection.setLocalDescription(offer);
                        } else {
                                await peerConnection.setLocalDescription(offer);
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
