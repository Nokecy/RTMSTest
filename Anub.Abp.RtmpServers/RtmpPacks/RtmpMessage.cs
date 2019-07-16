namespace Anub.Abp.RtmpServers.RtmpPacks
{
    public abstract class RtmpMessage
    {
        public RtmpHeader Header { get; set; }
        public int Timestamp { get; set; }
        public MessageType MessageType { get; set; }
        protected RtmpMessage(MessageType messageType)
        {
            MessageType = messageType;
        }
    }
}
