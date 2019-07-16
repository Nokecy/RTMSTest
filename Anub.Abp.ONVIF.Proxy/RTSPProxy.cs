using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anub.Abp.RTSP
{
    /// <summary>
    /// RTSP连接代理，不解析RTSP协议和报文内容，只做透明转发.
    /// </summary>
    public class RtspProxy
    {
        private TcpClient tcpclient;
        private Thread _thread;
        private Stream _stream;
        private bool quitflag = false;
        readonly Queue<byte[]> dataQueue = new Queue<byte[]>();
        readonly Queue<byte[]> controlQueue = new Queue<byte[]>();

        public RtspProxy()
        {
        }
        public bool Connect(string host, int port)
        {
            try
            {
                tcpclient = new TcpClient(host, port);
            }
            catch
            {
                return false;
            }
            if (!tcpclient.Connected)
            {
                return false;
            }
            _stream = tcpclient.GetStream();
            return true;
        }
        public void Start()
        {
            quitflag = false;
            if (_thread == null)
            {
                _thread = new Thread(Dowork);
                _thread.Start();
            }
        }
        public void Close()
        {
            quitflag = true;
            if (_thread != null)
            {
                _thread.Join();
                _thread = null;
            }
            Thread.Sleep(100);
            try
            {
                if (_stream != null)
                    _stream.Close();
                tcpclient.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public bool TryDequeData(out List<byte[]> datas, int max)
        {
            datas = null;
            lock (dataQueue)
            {
                int count = dataQueue.Count;
                int toread = count > max ? max : count;
                if (toread > 0)
                {
                    datas = new List<byte[]>();
                    for (int i = 0; i < toread; i++)
                    {
                        datas.Add(dataQueue.Dequeue());
                    }
                    return true;
                }
                return false;
            }
        }
        public bool TryDequeControl(out List<byte[]> datas, int max)
        {
            datas = null;
            lock (controlQueue)
            {
                int count = controlQueue.Count;
                int toread = count > max ? max : count;
                if (toread > 0)
                {
                    datas = new List<byte[]>();
                    for (int i = 0; i < toread; i++)
                    {
                        datas.Add(controlQueue.Dequeue());
                    }
                    return true;
                }
                return false;
            }
        }
        private void Dowork()
        {
            try
            {
                int remainlen = 0;//当前读取到的memory位置.
                byte[] buffer = new byte[8192];
                while (!quitflag)
                {
                    int readlength = 0;
                    readlength = _stream.Read(buffer, remainlen, 4096);
                    if (readlength == 0)
                    {
                        break;
                    }//链接关闭.
                    int bufferlength = remainlen + readlength;
                    int pos = 0;
                    while (pos < bufferlength)
                    {
                        if (buffer[pos] == '$')
                        {
                            if (pos + 3 > bufferlength)
                            {
                                break;
                            }//need read more.
                            int l = (buffer[pos + 2] << 8) + buffer[pos + 3] + 4;
                            if (pos + l > bufferlength)
                            {
                                break;
                            }//need read more.
                            byte[] bs = new byte[l];
                            Array.Copy(buffer, pos, bs, 0, l);
                            lock (dataQueue)
                            {
                                if (dataQueue.Count < 1000)
                                {
                                    dataQueue.Enqueue(bs);
                                }//discard data when >= 1000
                            }
                            pos += l;
                        }//data
                        else
                        {
                            string strline;
                            byte lineend = (byte)'\n';
                            string contentlengthkey = "Content-Length";
                            int contentlength = 0;
                            int start = pos, i = pos;
                            bool getcontentlength = false;
                            while (i < bufferlength)
                            {
                                if (buffer[i] == lineend)
                                {
                                    int linelength = i - start - 1;//-1 for \r
                                    if (!getcontentlength && linelength > 0)
                                    {
                                        strline = ASCIIEncoding.UTF8.GetString(buffer, start, linelength);
                                        if (strline.Contains(contentlengthkey))
                                        {
                                            string[] parts = strline.Split(':');
                                            contentlength = Convert.ToInt32(parts[1].Trim());
                                            getcontentlength = true;
                                        }
                                    }
                                    start = i + 1;//next line +1 for \n
                                    if (linelength == 0)
                                    {
                                        break;
                                    }//header end with \r\n\r\n
                                }
                                i++;
                            }
                            int end = start + contentlength;
                            if (end == pos || end > bufferlength)//end==pos 等于一行也没读到，end > bufferlength等于有剩余的内容没读完.
                            {
                                break;
                            }//need readmore.
                            var bs = new byte[end - pos];
                            Array.Copy(buffer, pos, bs, 0, end - pos);
                            lock (controlQueue)
                            {
                                controlQueue.Enqueue(bs);
                            }
                            pos = end;
                        }//control
                    }
                    remainlen = bufferlength - pos;
                    for (int j = 0; j < remainlen; j++)
                    {
                        buffer[j] = buffer[j + pos];
                    }
                }
            }
            catch (IOException error)
            {
                //Logger.Error(error);
                throw error;
            }
            Close();
        }
        public async Task Send(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
        }
    }
}
