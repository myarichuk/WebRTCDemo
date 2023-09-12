using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.FFmpeg;
using System.Net;
using WebRTC.Server;
using WebSocketSharp.Server;

class Program
{
        private const int WEBSOCKET_PORT = 8081;

        static void Main()
        {
            Console.WriteLine("WebRTC Get Started");

            // Start web socket.
            Console.WriteLine("Starting web socket server...");

            //note: this REQUIRES ffmpeg 4.4 shared codecs in the output folder (only 4.4 version!)
            ffmpeg.RootPath = @"/usr/lib/x86_64-linux-gnu";
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_VERBOSE, Environment.CurrentDirectory);

            var webSocketServer = new WebSocketServer(IPAddress.Any, WEBSOCKET_PORT);
            webSocketServer.AddWebSocketService<WebRTCWebSocketPeer>("/", (peer) => peer.CreatePeerConnection = () => CreatePeerConnection());
            webSocketServer.Start();

            Console.WriteLine($"Waiting for web socket connections on {webSocketServer.Address}:{webSocketServer.Port}...");
            
            Console.WriteLine("Press any key exit.");
            Console.ReadKey();
        }

        private static Task<RTCPeerConnection> CreatePeerConnection()
        {
            var pc = new RTCPeerConnection(null);

            var audioSource = new FFmpegStreamSource(File.OpenRead("music.aac"),false, new AudioEncoder());
                //new FFmpegFileSource("music.aac", false, new AudioEncoder(), audioFrameSize: 480, useVideo: false);
            
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
