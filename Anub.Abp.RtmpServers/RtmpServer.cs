using Anub.Abp.RtmpServers.Packs;
using BeetleX;
using BeetleX.EventArgs;
using System;
using System.Collections.Generic;

namespace Anub.Abp.RtmpServers
{
    public class RtmpServer : ServerHandlerBase
    {
        internal readonly Dictionary<long, HandshakeStatus> SessionStatus = new Dictionary<long, HandshakeStatus>();
        private static IServer server;
        public int Port { get; }
        public RtmpServer() { }
        public RtmpServer(int port = 1935)
        {
            Port = port;
        }

        public bool Start()
        {
            var rtmpPacket = new RtmpPacket(this);
            server = SocketFactory.CreateTcpServer(this, rtmpPacket).Setting(setting =>
            {
                //setting.DefaultListen.Host = Options.Host;
                setting.DefaultListen.Port = Port;
            });
            return server.Open();
        }

        public override void Connected(IServer server, ConnectedEventArgs e)
        {
            SessionStatus.Add(e.Session.ID, HandshakeStatus.RTMP_HANDSHAKE_0);
            base.Connected(server, e);
        }

        public override void SessionPacketDecodeCompleted(IServer server, PacketDecodeCompletedEventArgs e)
        {
            if (e.Message is C0C1)
            {
                Console.WriteLine("Get C0C1");
                SessionStatus[e.Session.ID] = HandshakeStatus.RTMP_HANDSHAKE_1;
                var bytes = RtmpHandshake.GetS01Async();
                e.Session.Stream.ToPipeStream().Write(bytes, 0, bytes.Length);
                e.Session.Stream.Flush();
            }
            else if (e.Message is C2)
            {
                Console.WriteLine("Get C2 ");
                Console.WriteLine("Handshake Done");
                SessionStatus[e.Session.ID] = HandshakeStatus.RTMP_HANDSHAKE_2;
                var bytes = RtmpHandshake.GetS2Async();
                e.Session.Stream.ToPipeStream().Write(bytes, 0, bytes.Length);
                e.Session.Stream.Flush();
            }
            base.SessionPacketDecodeCompleted(server, e);
        }

        public override void SessionReceive(IServer server, SessionReceiveEventArgs e)
        {
            base.SessionReceive(server, e);
        }
    }
}
