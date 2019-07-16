using Anub.Abp.ONVIF.Devices;
using Anub.Abp.ONVIF.Medias;
using OnvifSharp.Discovery;
using System;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Anub.Abp.ONVIF.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("开始探索ONVIF相机!");
            var discovery = new WSDiscovery();
            var devices = await discovery.Discover(5);
            Console.WriteLine($"发现的设备: {devices.Count()}");
            int i = 1;
            foreach (var device in devices)
            {
                Console.Write($"( {i} ) 名称: {device.Name} Model: {device.Model} ");
                Console.Write($"XAddresses: ");
                foreach (var address in device.XAdresses)
                {
                    Console.Write($"{address}, ");

                    await GetDeviceInfoAsync(address);
                }
                i++;
                Console.WriteLine("");
            }
            Console.WriteLine("ONVIF Discovery finnished!");
        }

        public static async Task GetDeviceInfoAsync(string address)
        {
            var messageElement = new TextMessageEncodingBindingElement();
            messageElement.MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None);
            HttpTransportBindingElement httpBinding = new HttpTransportBindingElement();
            httpBinding.AuthenticationScheme = AuthenticationSchemes.Digest;
            CustomBinding bind = new CustomBinding(messageElement, httpBinding);

            //绑定服务地址
            EndpointAddress serviceAddress = new EndpointAddress(address);
            DeviceClient deviceClient = new DeviceClient(bind, serviceAddress);

            //查看系统时间
            var date = await deviceClient.GetSystemDateAndTimeAsync();
            Console.WriteLine(date.UTCDateTime.Date.Month.ToString());
            //查看设备能力
            GetCapabilitiesResponse cap = await deviceClient.GetCapabilitiesAsync(new CapabilityCategory[] { CapabilityCategory.All });
            Console.WriteLine(cap.Capabilities.Media.XAddr.ToString());
            await GetMediaInfoAsync(cap.Capabilities.Media.XAddr.ToString());
        }

        public static async Task GetMediaInfoAsync(string address)
        {
            var messageElement = new TextMessageEncodingBindingElement
            {
                MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None)
            };
            HttpTransportBindingElement httpBinding = new HttpTransportBindingElement();
            httpBinding.MaxReceivedMessageSize = 1024 * 1024 * 1024;
            //httpBinding.AuthenticationScheme = AuthenticationSchemes.Digest;
            CustomBinding bind = new CustomBinding(messageElement, httpBinding);

            //绑定服务地址
            EndpointAddress serviceAddress = new EndpointAddress(address);
            MediaClient mediaClient = new MediaClient(bind, serviceAddress);

            //给每个请求都添加认证信息
            mediaClient.Endpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());
            //不要少了下面这行，会报异常
            var channel = mediaClient.ChannelFactory.CreateChannel();
            //查看系统时间
            var profilesResponse = await mediaClient.GetProfilesAsync();
            foreach (var profile in profilesResponse.Profiles)
            {
                var step = new StreamSetup
                {
                    Transport = new Transport()
                    {
                        Protocol = TransportProtocol.RTSP
                    },
                    Stream = StreamType.RTPUnicast
                };

                var streamUri = await mediaClient.GetStreamUriAsync(step, profile.token);
                Console.WriteLine(streamUri.Uri);
            }
        }
    }
}
