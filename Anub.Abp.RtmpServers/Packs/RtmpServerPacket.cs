using Anub.Abp.RtmpServers.RtmpPacks;
using BeetleX;
using BeetleX.Buffers;
using BeetleX.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Wenli.Live.RtmpLib.Amfs;

namespace Anub.Abp.RtmpServers.Packs
{
    public class RtmpServerPacket : IPacket
    {
        internal const int DefaultChunkSize = 128;
        internal int readChunkSize = DefaultChunkSize;

        internal readonly AmfReader reader;
        internal readonly Dictionary<int, RtmpHeader> rtmpHeaders;
        internal readonly Dictionary<int, RtmpPacket> rtmpPackets;

        private PacketDecodeCompletedEventArgs mCompletedArgs = new PacketDecodeCompletedEventArgs();
        private RtmpServer mServer;

        public EventHandler<PacketDecodeCompletedEventArgs> Completed { get; set; }

        public RtmpServerPacket(RtmpServer server)
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
                #region 握手
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
                #endregion
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

        protected bool ReadOnce()
        {
            var header = ReadHeader();
            if (header == null)
            {
                return false;
            }
            rtmpHeaders[header.StreamId] = header;

            RtmpPacket packet;
            if (!rtmpPackets.TryGetValue(header.StreamId, out packet) || packet == null)
            {
                packet = new RtmpPacket(header);
                rtmpPackets[header.StreamId] = packet;
            }

            var remainingMessageLength = packet.Length + (header.Timestamp >= 0xFFFFFF ? 4 : 0) - packet.CurrentLength;
            var bytesToRead = Math.Min(remainingMessageLength, readChunkSize);
            var bytes = reader.ReadBytes(bytesToRead);
            packet.AddBytes(bytes);

            if (packet.IsComplete)
            {
                rtmpPackets.Remove(header.StreamId);

                var @event = ParsePacket(packet);

                //if (@event != null)
                //OnEventReceived(new EventReceivedEventArgs(@event));

                // process some kinds of packets
                var chunkSizeMessage = @event as ChunkSize;
                if (chunkSizeMessage != null)
                    readChunkSize = chunkSizeMessage.Size;

                var abortMessage = @event as Abort;
                if (abortMessage != null)
                    rtmpPackets.Remove(abortMessage.StreamId);
            }
            return true;
        }

        protected RtmpMessage ParsePacket(RtmpPacket packet, Func<AmfReader, RtmpMessage> handler)
        {
            var memoryStream = new MemoryStream(packet.Buffer, false);
            var packetReader = new AmfReader(memoryStream, reader.SerializationContext);

            var header = packet.Header;
            var message = handler(packetReader);
            message.Header = header;
            message.Timestamp = header.Timestamp;
            return message;
        }
        protected RtmpMessage ParsePacket(RtmpPacket packet)
        {
            switch (packet.Header.MessageType)
            {
                case MessageType.SetChunkSize:
                    return ParsePacket(packet, r => new ChunkSize(r.ReadInt32()));
                case MessageType.AbortMessage:
                    return ParsePacket(packet, r => new Abort(r.ReadInt32()));
                case MessageType.Acknowledgement:
                    return ParsePacket(packet, r => new Acknowledgement(r.ReadInt32()));
                case MessageType.UserControlMessage:
                    return ParsePacket(packet, r =>
                    {
                        var eventType = r.ReadUInt16();
                        var values = new List<int>();
                        while (r.Length - r.Position >= 4)
                            values.Add(r.ReadInt32());
                        return new UserControlMessage((UserControlMessageType)eventType, values.ToArray());
                    });
                case MessageType.WindowAcknowledgementSize:
                    return ParsePacket(packet, r => new WindowAcknowledgementSize(r.ReadInt32()));
                case MessageType.SetPeerBandwith:
                    return ParsePacket(packet, r => new PeerBandwidth(r.ReadInt32(), r.ReadByte()));
                case MessageType.Audio:
                    return ParsePacket(packet, r => new AudioData(packet.Buffer));
                case MessageType.Video:
                    return ParsePacket(packet, r => new VideoData(packet.Buffer));


                case MessageType.DataAmf0:
                    return ParsePacket(packet, r => ReadCommandOrData(r, new NotifyAmf0(), packet.Header));
                case MessageType.SharedObjectAmf0:
                    break;
                case MessageType.CommandAmf0:
                    return ParsePacket(packet, r => ReadCommandOrData(r, new InvokeAmf0()));


                case MessageType.DataAmf3:
                    return ParsePacket(packet, r => ReadCommandOrData(r, new NotifyAmf3()));
                case MessageType.SharedObjectAmf3:
                    break;
                case MessageType.CommandAmf3:
                    return ParsePacket(packet, r =>
                    {
                        // encoding? always seems to be zero
                        var unk1 = r.ReadByte();
                        return ReadCommandOrData(r, new InvokeAmf3());
                    });


                // aggregated messages only seem to be used in audio and video streams, so we should be OK until we need multimedia.
                // case MessageType.Aggregate:

                default:
#if DEBUG && RTMP_SHARP_DEV
                    // find out how to handle this message type.
                    System.Diagnostics.Debugger.Break();
#endif
                    break;
            }

            // skip messages we don't understand
            return null;
        }

        protected RtmpMessage ReadCommandOrData(AmfReader r, Command command, RtmpHeader header = null)
        {
            var methodName = (string)r.ReadAmf0Item();
            object temp = r.ReadAmf0Item();
            if (header != null && methodName == "@setDataFrame")
            {
                command.ConnectionParameters = temp;
            }
            else
            {
                command.InvokeId = Convert.ToInt32(temp);
                command.ConnectionParameters = r.ReadAmf0Item();
            }


            var parameters = new List<object>();
            while (r.DataAvailable)
                parameters.Add(r.ReadAmf0Item());

            command.MethodCall = new Method(methodName, parameters.ToArray());
            return command;
        }

        protected RtmpHeader ReadHeader()
        {
            // first byte of the chunk basic header
            var chunkBasicHeaderByte = reader.ReadByte();
            var chunkStreamId = GetChunkStreamId(chunkBasicHeaderByte, reader);
            var chunkMessageHeaderType = (ChunkMessageHeaderType)(chunkBasicHeaderByte >> 6);

            var header = new RtmpHeader()
            {
                StreamId = chunkStreamId,
                IsTimerRelative = chunkMessageHeaderType != ChunkMessageHeaderType.New
            };

            RtmpHeader previousHeader;
            // don't need to clone if new header, as it contains all info
            if (!rtmpHeaders.TryGetValue(chunkStreamId, out previousHeader) && chunkMessageHeaderType != ChunkMessageHeaderType.New)
                previousHeader = header.Clone();

            switch (chunkMessageHeaderType)
            {
                // 11 bytes
                case ChunkMessageHeaderType.New:
                    header.Timestamp = reader.ReadUInt24();
                    header.PacketLength = reader.ReadUInt24();
                    header.MessageType = (MessageType)reader.ReadByte();
                    header.MessageStreamId = reader.ReadReverseInt();
                    break;

                // 7 bytes
                case ChunkMessageHeaderType.SameSource:
                    header.Timestamp = reader.ReadUInt24();
                    header.PacketLength = reader.ReadUInt24();
                    header.MessageType = (MessageType)reader.ReadByte();
                    header.MessageStreamId = previousHeader.MessageStreamId;
                    break;

                // 3 bytes
                case ChunkMessageHeaderType.TimestampAdjustment:
                    header.Timestamp = reader.ReadUInt24();
                    header.PacketLength = previousHeader.PacketLength;
                    header.MessageType = previousHeader.MessageType;
                    header.MessageStreamId = previousHeader.MessageStreamId;
                    break;

                // 0 bytes
                case ChunkMessageHeaderType.Continuation:
                    header.Timestamp = previousHeader.Timestamp;
                    header.PacketLength = previousHeader.PacketLength;
                    header.MessageType = previousHeader.MessageType;
                    header.MessageStreamId = previousHeader.MessageStreamId;
                    header.IsTimerRelative = previousHeader.IsTimerRelative;
                    break;
                default:
                    throw new SerializationException("Unexpected header type: " + (int)chunkMessageHeaderType);
            }

            // extended timestamp
            if (header.Timestamp == 0xFFFFFF)
                header.Timestamp = reader.ReadInt32();

            return header;
        }

        protected int GetChunkStreamId(byte chunkBasicHeaderByte, AmfReader reader)
        {
            var chunkStreamId = chunkBasicHeaderByte & 0x3F;
            switch (chunkStreamId)
            {
                // 2 bytes
                case 0:
                    return reader.ReadByte() + 64;

                // 3 bytes
                case 1:
                    return reader.ReadByte() + reader.ReadByte() * 256 + 64;

                // 1 byte
                default:
                    return chunkStreamId;
            }
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
            RtmpServerPacket result = new RtmpServerPacket(mServer);
            return result;
        }

        public void Dispose()
        {

        }
    }
}
