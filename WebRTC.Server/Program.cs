using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.FFmpeg;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebSocketSharp.Server;

class Program
{
    private const int WEBSOCKET_PORT = 8081;
    private const int HTTP_PORT = 5000;

    static void Main()
    {
        // Start HTTP server for audio streaming
        Task.Run(() => StartWebServer());

        // Your existing WebRTC and WebSocket code
        Console.WriteLine("WebRTC Get Started");
        Console.WriteLine("Starting web socket server...");

        FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_VERBOSE, Environment.CurrentDirectory);

        var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT);
        webSocketServer.AddWebSocketService<WebRTCWebSocketPeer>("/", (peer) => peer.CreatePeerConnection = () => CreatePeerConnection());
        webSocketServer.Start();

        Console.WriteLine($"Waiting for web socket connections on {webSocketServer.Address}:{webSocketServer.Port}...");

        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }

    private static void StartWebServer()
    {
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls($"http://*:{HTTP_PORT}");
            })
            .Build()
            .Run();
    }

        private static Task<RTCPeerConnection> CreatePeerConnection()
        {
            var pc = new RTCPeerConnection(null);

            var audioSource = new FFmpegFileSource($"http://localhost:{HTTP_PORT}/audio", false, new AudioEncoder(), audioFrameSize: 480, useVideo: false);

            var audioTrack = new MediaStreamTrack(audioSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendOnly);
        
            pc.addTrack(audioTrack);

            audioSource.OnAudioSourceEncodedSample += pc.SendAudio; //wire sending as soon as data is received
            audioSource.OnAudioSourceError += msg => pc.Close($"Audio Source Error! (got message '{msg}')");

            pc.OnAudioFormatsNegotiated += (formats) => audioSource.SetAudioSourceFormat(formats.First());
            pc.onconnectionstatechange += async (state) =>
            {
                Console.WriteLine($"Peer connection state change to {state}.");

                switch(state)
                {
                    case RTCPeerConnectionState.connected:
                        await audioSource.StartAudio();
                        break;
                    case RTCPeerConnectionState.failed:
                        pc.Close("ice disconnection");
                        break;
                    case RTCPeerConnectionState.closed:
                        await audioSource.CloseAudio();
                        audioSource.Dispose();
                        break;
                }
            };

            return Task.FromResult(pc);
        }
}

public class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
    }
}

[ApiController]
public class StreamController : ControllerBase
{
    [HttpGet("audio")]
    public IActionResult GetAudioStream()
    {
        byte[] audioData = System.IO.File.ReadAllBytes("music.aac"); // Replace with your method to get audio data
        MemoryStream memStream = new MemoryStream(audioData);

        // Set the position to the beginning of the stream.
        memStream.Seek(0, SeekOrigin.Begin);

        return new FileStreamResult(memStream, "audio/aac");
    }
}
