using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;

namespace WebVirtualDisplayClient;

/*
 * Genuienly had to have Claude rewrite this class
 * No idea wth is going on in here I wish I could tell you
 * Probably has some issues in a few ways I am certain
 *
 * Still a mess of a class
*/

class WebRTCClient {
    public static RTCPeerConnection Current => peerConnection;
    public static readonly ConcurrentDictionary<uint, string> ssrcToHwnd = new();

    private static volatile RTCPeerConnection peerConnection = null!;
    private static volatile RTCSessionDescriptionInit? offer;

    private static readonly ConcurrentBag<string> localIceCandidates = new();

    // everything that wants to attach tracks/channels to each connection registers here
    private static readonly List<Func<RTCPeerConnection, Task>> connectionHandlers = new();
    private static readonly SemaphoreSlim connectionLock = new(1, 1); // serializes resets + registrations
    private static readonly SemaphoreSlim offerLock = new(1, 1);      // serializes offer building
    private static volatile bool suppressNegotiation;

    /// Runs the handler on the current connection (if any) and on every future connection.
    public static async Task OnConnection(Func<RTCPeerConnection, Task> handler) {
        await connectionLock.WaitAsync();
        try {
            connectionHandlers.Add(handler);
            if (peerConnection != null) await handler(peerConnection);
        } finally {
            connectionLock.Release();
        }
    }

    public static async Task ResetConnection() {
        await connectionLock.WaitAsync();
        try {
            var old = peerConnection;
            suppressNegotiation = true; // we build exactly one offer at the end

            offer = null;

            var pc = CreatePeerConnection();
            peerConnection = pc;          // swap FIRST so the old connection's events are seen as stale
            localIceCandidates.Clear();

            try { old?.close(); } catch { }

            foreach (var handler in connectionHandlers.ToArray()) {
                try { await handler(pc); }
                catch (Exception ex) { Console.WriteLine($"Connection handler failed: {ex}"); }
            }

            suppressNegotiation = false;
            await BuildOffer(pc);
        } finally {
            suppressNegotiation = false;
            connectionLock.Release();
        }
    }

    public static async Task InitializeClient(CancellationToken stoppingToken) {
        await ResetConnection();
    }

    // kept for any other callers; prefer OnConnection + pc.createDataChannel(label)
    public static async Task<RTCDataChannel> createDataChannel(string label = "data-stream") {
        return await peerConnection.createDataChannel(label);
    }

    // the ONLY place offers get created: createOffer + msid munging + setLocalDescription
    private static async Task BuildOffer(RTCPeerConnection pc) {
        await offerLock.WaitAsync();
        try {
            if (pc != peerConnection) return;

            var raw = pc.createOffer();
            var sections = raw.sdp.Split(new[] { "\r\nm=" }, StringSplitOptions.None);

            // section 0 is the session header, the rest are m-lines
            for (int i = 1; i < sections.Length; i++) {
                if (sections[i].Contains("a=msid:")) continue;

                var ssrcMatch = Regex.Match(sections[i], @"a=ssrc:(\d+)");
                if (!ssrcMatch.Success) continue;

                uint ssrc = uint.Parse(ssrcMatch.Groups[1].Value);
                if (!ssrcToHwnd.TryGetValue(ssrc, out var hwnd)) continue;

                // insert the msid line just before the first a=ssrc line
                sections[i] = sections[i].Insert(ssrcMatch.Index, $"a=msid:{hwnd} video-{hwnd}\r\n");
            }

            var munged = new RTCSessionDescriptionInit {
                type = RTCSdpType.offer,
                sdp = string.Join("\r\nm=", sections)
            };

            offer = munged;
            await pc.setLocalDescription(munged);
        } finally {
            offerLock.Release();
        }
    }

    private static RTCPeerConnection CreatePeerConnection() {
        var pc = new RTCPeerConnection(new RTCConfiguration { iceServers = new List<RTCIceServer>() });

        pc.onicecandidate += (c) => {
            if (pc != peerConnection) return; // stale connection
            if (!string.IsNullOrEmpty(c.candidate)) localIceCandidates.Add(c.candidate);
        };

        pc.onconnectionstatechange += (state) => {
            if (pc != peerConnection) return; // stale connection
            Console.WriteLine($"WebRTC Connection State Changed: {state}");
            if (state is RTCPeerConnectionState.failed or RTCPeerConnectionState.closed)
                _ = ResetConnection();
        };

        pc.onnegotiationneeded += async () => {
            if (pc != peerConnection || suppressNegotiation) return;
            try { await BuildOffer(pc); }
            catch (Exception ex) { Console.WriteLine($"Renegotiation failed: {ex}"); }
        };

        return pc;
    }

    public static void RegisterSignalingRoutes(WebApplication app) {
        if (offer == null) throw new NullReferenceException("Offer not generated yet");

        app.MapPost("/api/webrtc/reset", async () => {
            await ResetConnection();
            return Results.Ok();
        });

        app.MapGet("/api/webrtc/offer", async () => {
            // only wait while a fresh offer is being generated (e.g. right after a reset)
            int attempts = 0;
            while (offer == null && attempts < 20) {
                await Task.Delay(100);
                attempts++;
            }

            if (offer == null) return Results.StatusCode(503);

            // nothing pending: already negotiated, client just keeps polling
            if (peerConnection.signalingState != RTCSignalingState.have_local_offer)
                return Results.NoContent();

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

            if (answerPayload == null) return Results.BadRequest("null payload");

            if (peerConnection.signalingState != RTCSignalingState.have_local_offer)
                return Results.Conflict("No pending offer to answer");

            answerPayload.type = RTCSdpType.answer;
            var result = peerConnection.setRemoteDescription(answerPayload);
            if (result != SetDescriptionResultEnum.OK) {
                Console.WriteLine($"setRemoteDescription failed: {result}");
                return Results.Conflict(result.ToString());
            }

            return Results.Ok();
        });

        app.MapGet("/api/webrtc/ice", () => Results.Json(localIceCandidates));

        app.MapPost("/api/webrtc/ice", async (HttpContext ctx) => {
            string body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();

            RTCIceCandidateInit? icePayload;
            try {
                icePayload = System.Text.Json.JsonSerializer.Deserialize<RTCIceCandidateInit>(body);
            } catch (Exception ex) {
                Console.WriteLine($"ICE DESERIALIZE FAILED: {ex}");
                return Results.BadRequest(ex.Message);
            }

            if (icePayload != null && !string.IsNullOrEmpty(icePayload.candidate)) {
                peerConnection.addIceCandidate(icePayload);
                return Results.Ok();
            }

            return Results.BadRequest();
        });
    }
}
