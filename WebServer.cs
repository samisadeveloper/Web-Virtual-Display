// TODO: this will start up an http server listening on some port or something

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SIPSorcery.Net;

namespace WebVirtualDisplayClient;

class WebServer : BackgroundService
{
        private static string? _latestOffer;
        private static string? _latestAnswer;
        private static readonly List<string> _iceCandidates = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                var builder = WebApplication.CreateBuilder();

                builder.Services.AddCors(options => {
                                options.AddPolicy("AllowReact", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
                                });

                var app = builder.Build();
                app.UseCors("AllowReact");

                app.UseDefaultFiles(); // Serve the index.html file by default
                String currentDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                app.UseStaticFiles(new StaticFileOptions
                {
                        FileProvider = new PhysicalFileProvider(currentDir),
                        RequestPath = ""
                });
                // INFO: to be honest I don't really know webrtc boilerplate
                // I did not write any of the below code.

                // accept the offer
                app.MapPost("/api/webrtc/offer", (RTCSessionDescriptionInit payload) => {
                        _latestOffer = payload.sdp;

                        return Results.Ok();
                });

                app.MapGet("/api/webrtc/offer", () => Results.Ok(_latestOffer));

                // accept the answer
                app.MapPost("/api/webrtc/answer", (RTCSessionDescriptionInit answer) => {
                        _latestAnswer = answer.ToString();

                        return Results.Ok();
                });

                app.MapGet("/api/webrtc/answer", () => Results.Ok(_latestAnswer));

                // accept ice candidates
                app.MapPost("/api/webrtc/ice", (RTCIceCandidateInit candidate) => {
                        _iceCandidates.Add(candidate.candidate);

                        return Results.Ok();
                });

                app.MapGet("/api/webrtc/ice", () => Results.Ok(_iceCandidates));

                await app.RunAsync(stoppingToken); // http://localhost:5000 -- TODO: host over the LAN not just loopback
        }
}
