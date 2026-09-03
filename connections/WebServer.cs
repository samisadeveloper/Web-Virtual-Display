using System.IO;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace WebVirtualDisplayClient;

class WebServer : BackgroundService
{
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                var builder = WebApplication.CreateBuilder();

                builder.Services.AddCors(options => {
                        options.AddPolicy("AllowReact", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
                });

                builder.Services.ConfigureHttpJsonOptions(options => {
                        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

                WebApplication app = builder.Build();
                app.UseCors("AllowReact");

                app.UseDefaultFiles(); // Serve the index.html file by default

                // serve files from ./wwwroot/
                String currentDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                app.UseStaticFiles(new StaticFileOptions
                {
                        FileProvider = new PhysicalFileProvider(currentDir),
                        RequestPath = ""
                });

                await WebRTCClient.initializeClient(stoppingToken);
                WebRTCClient.RegisterSignalingRoutes(app); // register the signaling

                await app.RunAsync("http://0.0.0.0:5000");
        }
}
