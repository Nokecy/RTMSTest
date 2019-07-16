using System;
using System.Collections.Generic;
using System.Text;

namespace Anub.Abp.ONVIF.Proxy
{
    public class SocketRtspResponse
    {
        public SocketRtspResponse()
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
}
