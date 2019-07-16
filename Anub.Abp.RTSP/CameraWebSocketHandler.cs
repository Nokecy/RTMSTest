using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anub.Abp.RTSP
{
    public class CameraWebSocketHandler : WebSocketHandler
    {
        ConcurrentDictionary<string, WSRtspContext> ws_rtsps;
        public CameraWebSocketHandler(WebSocketConnectionManager webSocketConnectionManager)
            : base(webSocketConnectionManager)
        {
            ws_rtsps = new ConcurrentDictionary<string, WSRtspContext>();
        }
        public override void OnConnected(WebSocket socket)
        {
            base.OnConnected(socket);
        }

        public override async Task OnDisconnected(WebSocket socket)
        {
            var socketId = WebSocketConnectionManager.GetId(socket);
            if (ws_rtsps.ContainsKey(socketId))
            {
                WSRtspContext wsrtsp;
                ws_rtsps.TryRemove(socketId, out wsrtsp);
                wsrtsp.StopReceive();
                if (wsrtsp.Rtsp != null)
                {
                    wsrtsp.Rtsp.Close();
                }
            }//关闭rtsp.
            await base.OnDisconnected(socket);
        }

        public override async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return;
            var socketId = WebSocketConnectionManager.GetId(socket);
            WSRtspContext wsrtsp = null;
            if (ws_rtsps.ContainsKey(socketId))
            {
                wsrtsp = ws_rtsps[socketId];
            }
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string package = Encoding.UTF8.GetString(buffer);
                string command = getWSPCommand(package);
                string seq = getByKey(package, "seq");
                if (command == "INIT")//建立新链接.
                {
                    string host = getByKey(package, "host");
                    string port = getByKey(package, "port");
                    if (port == null)
                        port = "554";
                    if (host != null)
                    {
                        try
                        {
                            RtspProxy rtsp = new RtspProxy();
                            bool connected = rtsp.Connect(host, Convert.ToInt32(port));
                            if (connected)
                            {
                                rtsp.Start();
                                wsrtsp = new WSRtspContext(socket, rtsp, false);
                                wsrtsp.ControlWebSocketId = socketId;
                                ws_rtsps.TryAdd(socketId, wsrtsp);
                                wsrtsp.Seq = seq;
                                //返回握手.
                                WSRtspResponse response = new WSRtspResponse();
                                response.Seq = seq;
                                response.Shakehand = true;
                                response.Channel = socketId;
                                await socket.SendAsync(response.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
                                //启动接受rtsp控制报文，发送给ws.
                                await wsrtsp.StartReceive();
                            }
                            else
                            {
                                await removeSocket(wsrtsp);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            //Logger.Error(String.Format("connect to rtsp {0} Error:{1}", package, ex.Message));
                            return;
                        }
                    }
                }
                else if (command == "JOIN")//建立数据通道.
                {
                    string channel = getByKey(package, "channel");
                    if (channel != null && ws_rtsps.ContainsKey(channel))
                    {
                        WSRtspContext controlwsrtsp = ws_rtsps[channel];
                        controlwsrtsp.DataWebSocketId = socketId;
                        WSRtspContext datawsrtsp = new WSRtspContext(socket, controlwsrtsp.Rtsp, true);
                        datawsrtsp.ControlWebSocketId = controlwsrtsp.ControlWebSocketId;
                        datawsrtsp.DataWebSocketId = socketId;
                        ws_rtsps.TryAdd(socketId, datawsrtsp);
                        //返回握手.
                        WSRtspResponse response = new WSRtspResponse();
                        response.Seq = seq;
                        await socket.SendAsync(response.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
                        //启动接受rtsp数据报文，发送给ws.
                        await datawsrtsp.StartReceive();
                    }
                    return;
                }//WSP/1.1 JOIN channel: 127.0.0.1 - 2 18467 seq: 3   
                else
                {
                    wsrtsp.Seq = seq;
                    try
                    {
                        await wsrtsp.Send(getRtspBuffer(package));
                    }
                    catch
                    {
                        await removeSocket(wsrtsp);
                    }
                }
            }
            else if (wsrtsp != null)
            {
                try
                {
                    await wsrtsp.Send(buffer);
                }
                catch
                {
                    await removeSocket(wsrtsp);
                }
            }//WebSocketMessageType.Data      
        }

        private async Task removeSocket(WSRtspContext wsrtsp)
        {
            if (!String.IsNullOrEmpty(wsrtsp.ControlWebSocketId))
                await this.WebSocketConnectionManager.RemoveSocket(wsrtsp.ControlWebSocketId);
            if (!String.IsNullOrEmpty(wsrtsp.DataWebSocketId))
                await this.WebSocketConnectionManager.RemoveSocket(wsrtsp.DataWebSocketId);
        }

        //获取 WSP/1.1 WRAP 中的 WRAP.
        private static string getWSPCommand(string source)
        {
            string proto = "WSP/1.1";
            int protostart = source.IndexOf(proto);
            int protoend = source.IndexOf("\r\n", protostart);
            return source.Substring(proto.Length, protoend - proto.Length).Trim();
        }

        private static string getByKey(string source, string key)
        {
            int keyIndex = source.IndexOf(key);
            if (keyIndex > -1)
            {
                int indexKeyEnd = source.IndexOf("\r\n", keyIndex);
                if (indexKeyEnd > keyIndex)
                {
                    return source.Substring(keyIndex + key.Length + 1, indexKeyEnd - keyIndex - key.Length - 1).Trim();
                }
            }
            return null;
        }

        private static byte[] getRtspBuffer(string source)
        {
            if (source == null)
                return null;
            int wsmsgend = source.IndexOf("\r\n\r\n");
            if (wsmsgend > -1)
            {
                int rtsplen = source.Length - wsmsgend - 4;
                if (rtsplen > 0)
                {
                    string rtspmsg = source.Substring(wsmsgend + 4, rtsplen);
                    return ASCIIEncoding.UTF8.GetBytes(rtspmsg);
                }
            }
            return null;
        }
    }

    public class WSRtspResponse
    {
        public WSRtspResponse()
        {
            Shakehand = false;
        }

        const string Proto = "WSP/1.1 200 OK";

        public string Channel { get; set; }

        public string Seq { get; set; }

        public bool Shakehand { get; set; }

        public byte[] RtspBuffer { get; set; }

        public byte[] ToArray()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Proto).Append("\r\n");
            sb.Append("seq: ").Append(Seq).Append("\r\n");
            if (Shakehand)
                sb.Append("channel: ").Append(Channel).Append("\r\n");
            sb.Append("\r\n");
            byte[] wsheader = ASCIIEncoding.UTF8.GetBytes(sb.ToString());
            if (!Shakehand && RtspBuffer != null)
            {
                int wsheaderlength = wsheader.Length;
                if (RtspBuffer != null && RtspBuffer.Length > 0)
                {
                    int rtsplength = RtspBuffer.Length;
                    byte[] result = new byte[wsheaderlength + rtsplength];
                    Array.Copy(wsheader, result, wsheaderlength);
                    Array.Copy(RtspBuffer, 0, result, wsheaderlength, rtsplength);
                    return result;
                }
                else
                    return wsheader;
            }
            else
                return wsheader;

        }
    }

    public class WSRtspContext
    {
        WebSocket _ws;
        RtspProxy _rtsp;
        bool _dataChannel;
        bool quitFlag = false;
        public WSRtspContext(WebSocket ws, RtspProxy rtsp, bool dataChannel)
        {
            _ws = ws;
            _rtsp = rtsp;
            _dataChannel = dataChannel;
        }

        public string ControlWebSocketId { get; set; }

        public string DataWebSocketId { get; set; }

        public RtspProxy Rtsp { get { return _rtsp; } }

        public string Seq
        {
            get; set;
        }

        public async Task Send(byte[] buffer)
        {
            await _rtsp.Send(buffer);
        }

        public async Task StartReceive()
        {
            quitFlag = false;
            while (!quitFlag)
            {
                List<byte[]> datas;
                bool succeed;
                if (_dataChannel)
                    succeed = _rtsp.TryDequeData(out datas, 10);
                else
                    succeed = _rtsp.TryDequeControl(out datas, 1);
                if (succeed)
                {
                    foreach (byte[] data in datas)
                    {
                        if (!_dataChannel)
                        {
                            WSRtspResponse response = new WSRtspResponse();
                            response.Seq = Seq;
                            response.RtspBuffer = data;
                            await _ws.SendAsync(response.ToArray(), _dataChannel ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        else
                        {
                            await _ws.SendAsync(data, _dataChannel ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
                else
                {
                    if (_dataChannel)
                        await Task.Delay(1);
                    else
                        await Task.Delay(10);
                }
            }
        }

        public void StopReceive()
        {
            quitFlag = true;
        }
    }
}
