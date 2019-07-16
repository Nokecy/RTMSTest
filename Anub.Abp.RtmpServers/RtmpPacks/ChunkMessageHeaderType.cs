namespace Anub.Abp.RtmpServers.RtmpPacks
{
    public enum ChunkMessageHeaderType : byte
    {
        New = 0,
        SameSource = 1,
        TimestampAdjustment = 2,
        Continuation = 3
    }
}
