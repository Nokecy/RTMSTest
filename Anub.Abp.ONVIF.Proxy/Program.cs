using FFmpeg.AutoGen;
using System;
using System.IO;

namespace Anub.Abp.ONVIF.Proxy
{
    class Program
    {
        public static class FFmpegBinariesHelper
        {
            internal static void RegisterFFmpegBinaries()
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("FFmpeg", "bin", Environment.Is64BitProcess ? "x64" : "x86");
                while (current != null)
                {
                    var ffmpegBinaryPath = Path.Combine(current, probe);
                    if (Directory.Exists(ffmpegBinaryPath))
                    {
                        Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                        ffmpeg.RootPath = ffmpegBinaryPath;
                        return;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }
        }

        static unsafe void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            string fileInput = "rtsp://admin:mingming5288@192.168.1.83:554/h264/ch40/sub/av_stream";
            string fileOutput = "rtsp://192.168.1.120:8554/live/mystream";

            FFmpegBinariesHelper.RegisterFFmpegBinaries();

            string inUrl = "rtsp://admin:mingming5288@192.168.1.83:554/h264/ch40/sub/av_stream";//可以是本地文件
            string outUrl = "rtsp://192.168.1.120:8554/live/mystream";

            //初始化所有封装器
            ffmpeg.av_register_all();

            //初始化网络库
            ffmpeg.avformat_network_init();

            int res = 0;
            //打开文件，解封装文件头
            //输入封装上下文
            AVFormatContext* ictx = null;
            //设置rtsp协议延时最大值
            AVDictionary* opts = null;
            ffmpeg.av_dict_set(&opts, "max_delay", "500", 0);
            if ((res = ffmpeg.avformat_open_input(&ictx, inUrl, null, &opts)) != 0)
                new Exception();

            //获取音视频流信息
            if ((res = ffmpeg.avformat_find_stream_info(ictx, null)) < 0)
                new Exception();
            ffmpeg.av_dump_format(ictx, 0, inUrl, 0);

            //创建输出上下文
            AVFormatContext* octx = null;
            if (ffmpeg.avformat_alloc_output_context2(&octx, null, "rtsp", outUrl) < 0)
                new Exception();

            //配置输出流
            //遍历输入的AVStream
            for (int i = 0; i < ictx->nb_streams; ++i)
            {
                //创建输出流
                AVStream* out1 = ffmpeg.avformat_new_stream(octx, ictx->streams[i]->codec->codec);
                if (out1 == null)
                {
                    //printf("new stream error.\n");
                    return;
                }
                //复制配置信息
                if ((res = ffmpeg.avcodec_copy_context(out1->codec, ictx->streams[i]->codec)) != 0)
                    new Exception();
                //out->codec->codec_tag = 0;//标记不需要重新编解码
            }
            ffmpeg.av_dump_format(octx, 0, outUrl, 1);

            //rtmp推流
            //打开io
            //@param s Used to return the pointer to the created AVIOContext.In case of failure the pointed to value is set to NULL.
            res = ffmpeg.avio_open(&octx->pb, outUrl, ffmpeg.AVIO_FLAG_READ_WRITE);
            if (octx->pb == null)
                new Exception();

            //写入头信息
            //avformat_write_header可能会改变流的timebase
            if ((res = ffmpeg.avformat_write_header(octx, null)) < 0)
                new Exception();

            var begintime = ffmpeg.av_gettime();
            var realdts = 0;
            var caldts = 0;
            AVPacket pkt;
            while (true)
            {
                if ((res = ffmpeg.av_read_frame(ictx, &pkt)) != 0)
                    break;
                if (pkt.size <= 0)//读取rtsp时pkt.size可能会等于0
                    continue;
                //转换pts、dts、duration
                //pkt.pts = (long)(pkt.pts * ffmpeg.av_q2d(ictx->streams[pkt.stream_index]->time_base) / ffmpeg.av_q2d(octx->streams[pkt.stream_index]->time_base));
                //pkt.dts = (long)(pkt.dts * ffmpeg.av_q2d(ictx->streams[pkt.stream_index]->time_base) / ffmpeg.av_q2d(octx->streams[pkt.stream_index]->time_base));
                //pkt.duration = (long)(pkt.duration * ffmpeg.av_q2d(ictx->streams[pkt.stream_index]->time_base) / ffmpeg.av_q2d(octx->streams[pkt.stream_index]->time_base));
                //pkt.pos = -1;//byte position in stream, -1 if unknown

                //文件推流计算延时
                //av_usleep(30 * 1000);
                /*realdts = av_gettime() - begintime;
                caldts = 1000 * 1000 * pkt.pts * av_q2d(octx->streams[pkt.stream_index]->time_base);
                if (caldts > realdts)
                    av_usleep(caldts - realdts);*/

                if ((res = ffmpeg.av_interleaved_write_frame(octx, &pkt)) < 0)//推流,推完之后pkt的pts，dts竟然都被重置了！而且前面几帧还因为dts没有增长而返回-22错误
                    new Exception();

                ffmpeg.av_packet_unref(&pkt);//回收pkt内部分配的内存
            }
            ffmpeg.av_write_trailer(octx);//写文件尾
        }
    }
}
