using Anub.Abp.RTSP;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Anub.Abp.ONVIF.Proxy
{
    public class RTSPSocketContext
    {
        public RtspProxy Rtsp { get; }
        public TcpClient TcpClient { get; }
        public bool DataChannel { get; }
        public bool QuitFlag = false;
        public string ControlWebSocketId { get; set; }
        public string DataWebSocketId { get; set; }
        public string Seq { get; set; }
        public RTSPSocketContext(TcpClient tcpClient, RtspProxy rtsp, bool dataChannel)
        {
            TcpClient = tcpClient;
            Rtsp = rtsp;
            DataChannel = dataChannel;
        }
        public async Task Send(byte[] buffer)
        {
            await Rtsp.Send(buffer);
        }
        public async Task StartReceive()
        {
            QuitFlag = false;
            while (!QuitFlag)
            {
                List<byte[]> datas;
                bool succeed;
                if (DataChannel)
                    succeed = Rtsp.TryDequeData(out datas, 10);
                else
                    succeed = Rtsp.TryDequeControl(out datas, 1);
                if (succeed)
                {
                    foreach (byte[] data in datas)
                    {
                        if (!DataChannel)
                        {
                            SocketRtspResponse response = new SocketRtspResponse
                            {
                                Seq = Seq,
                                RtspBuffer = data
                            };
                            await TcpClient.Client.SendAsync(response.ToArray(), SocketFlags.None, CancellationToken.None);
                        }
                        else
                        {
                            await TcpClient.Client.SendAsync(data, SocketFlags.None, CancellationToken.None);
                        }
                    }
                }
                else
                {
                    if (DataChannel)
                        await Task.Delay(1);
                    else
                        await Task.Delay(10);
                }
            }
        }
        public void StopReceive()
        {
            QuitFlag = true;
        }
    }
}
