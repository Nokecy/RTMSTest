using Media.Rtsp;
using Media.Rtsp.Server;
using Media.Rtsp.Server.MediaTypes;
using System;
using System.Net;
using System.Threading;

namespace Anub.Abp.RtspServers
{
    public class MyRtspServer
    {
        public static void Main()
        {
            //Create the server optionally specifying the port to listen on
            using (RtspServer server = new RtspServer(IPAddress.Any, 554))
            {
                server.Logger = new RtspServerConsoleLogger();

                //Create a stream which will be exposed under the name Uri rtsp://localhost/live/RtspSourceTest
                //From the RtspSource rtsp://1.2.3.4/mpeg4/media.amp
                RtspSource source = new RtspSource("RtspSourceTest", "rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov");

                //If the stream had a username and password
                //source.Client.Credential = new System.Net.NetworkCredential("user", "password");

                //If you wanted to password protect the stream
                //source.RtspCredential = new System.Net.NetworkCredential("username", "password");

                //Add the stream to the server
                server.TryAddMedia(source);

                //server.TryAddRequestHandler(RtspMethod.OPTIONS,)

                //Start the server and underlying streams
                server.Start();

                Console.WriteLine("Waiting for source...");

                while (source.Ready == false) Thread.Sleep(10);

                Console.WriteLine("Source Ready...");

                Console.ReadKey();
                server.Stop();
            }
        }
    }
}
