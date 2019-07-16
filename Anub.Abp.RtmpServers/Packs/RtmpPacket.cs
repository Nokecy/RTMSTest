using BeetleX;
using BeetleX.Buffers;
using BeetleX.EventArgs;
using System;
using System.IO;

namespace Anub.Abp.RtmpServers.Packs
{
    public class RtmpPacket : IPacket
    {
        private PacketDecodeCompletedEventArgs mCompletedArgs = new PacketDecodeCompletedEventArgs();
        private RtmpServer mServer;

        public EventHandler<PacketDecodeCompletedEventArgs> Completed { get; set; }

        public RtmpPacket(RtmpServer server)
        {
            mServer = server;
        }

        public void Decode(ISession session, Stream stream)
        {
            PipeStream pstream = stream.ToPipeStream();
        Start:
            object data;
            HandshakeStatus handshakeStatus = mServer.SessionStatus[session.ID];
            switch (handshakeStatus)
            {
                case HandshakeStatus.RTMP_HANDSHAKE_0:
                    if (pstream.Length < 1537)
                        return;
                    data = OnC0C1Reader(session, pstream);
                    Completed?.Invoke(this, mCompletedArgs.SetInfo(session, data));
                    break;
                case HandshakeStatus.RTMP_HANDSHAKE_1:
                    data = OnC2Reader(session, pstream);
                    Completed?.Invoke(this, mCompletedArgs.SetInfo(session, data));
                    break;
                case HandshakeStatus.RTMP_HANDSHAKE_2:
                default:
                    break;
            }
            goto Start;
        }

        protected object OnC0C1Reader(ISession session, PipeStream stream)
        {
            stream.ReadToEnd();
            return new C0C1();
        }

        protected object OnC2Reader(ISession session, PipeStream stream)
        {
            stream.ReadToEnd();
            return new C2();
        }

        public void Encode(object data, ISession session, Stream stream)
        {
            throw new NotImplementedException();
        }

        public byte[] Encode(object data, IServer server)
        {
            throw new NotImplementedException();
        }

        public ArraySegment<byte> Encode(object data, IServer server, byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public IPacket Clone()
        {
            RtmpPacket result = new RtmpPacket(mServer);
            return result;
        }

        public void Dispose()
        {

        }
    }
}
