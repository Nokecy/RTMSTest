using System;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Xml;

namespace Anub.Abp.ONVIF
{
    public class CustomMessageHeader : MessageHeader
    {
        private const string NAMESPACE_SECURITY_0 = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string NAMESPACE_SECURITY_1 = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public CustomMessageHeader()
        {
        }
        public override string Name
        {
            get { return "wsse:Security"; }
        }
        public override string Namespace
        {
            get { return ""; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            string username = "admin";
            string password = "mingming5288";
            string nonce = GetNonce();
            string created = GetCreated();
            string digest = GetPasswordDigest(nonce, created, password);

            writer.WriteAttributeString("xmlns", "wsse", null, NAMESPACE_SECURITY_0);
            writer.WriteAttributeString("xmlns", "wsu", null, NAMESPACE_SECURITY_1);

            writer.WriteStartElement("wsse:UsernameToken");

            writer.WriteStartElement("wsse:Username");
            writer.WriteValue(username);
            writer.WriteEndElement();

            writer.WriteStartElement("wsse:Password");
            //少了这个Type属性，可能会报错
            writer.WriteAttributeString("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest");
            writer.WriteValue(digest);
            writer.WriteEndElement();

            writer.WriteStartElement("wsse:Nonce");
            writer.WriteValue(nonce);
            writer.WriteEndElement();

            writer.WriteStartElement("wsu:Created");
            writer.WriteValue(created);
            writer.WriteEndElement();

            writer.WriteEndElement();
        }
        private static byte[] BuildBytes(string nonce, string createdString, string basedPassword)
        {
            byte[] nonceBytes = System.Convert.FromBase64String(nonce);
            byte[] time = Encoding.UTF8.GetBytes(createdString);
            byte[] pwd = Encoding.UTF8.GetBytes(basedPassword);

            byte[] operand = new byte[nonceBytes.Length + time.Length + pwd.Length];
            Array.Copy(nonceBytes, operand, nonceBytes.Length);
            Array.Copy(time, 0, operand, nonceBytes.Length, time.Length);
            Array.Copy(pwd, 0, operand, nonceBytes.Length + time.Length, pwd.Length);

            return operand;
        }
        public static byte[] SHAOneHash(byte[] data)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(data);
                return hash;
            }
        }
        public static string GetPasswordDigest(string nonce, string createdString, string password)
        {
            byte[] combined = BuildBytes(nonce, createdString, password);
            string output = System.Convert.ToBase64String(SHAOneHash(combined));
            return output;
        }
        public static string GetCreated()
        {
            return DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ");
        }
        public static string GetNonce()
        {
            byte[] nonce = new byte[16];
            new Random().NextBytes(nonce);
            return Convert.ToBase64String(nonce);
        }
    }

    public class ClientMessageInspector : IClientMessageInspector
    {
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            CustomMessageHeader header = new CustomMessageHeader();
            try
            {
                //会有一些无用的默认header信息，可以删掉
                request.Headers.RemoveAt(0);
                request.Headers.RemoveAt(0);
            }
            catch { }
            request.Headers.Add(header);
            return request;
        }
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }
    }

    public class CustomEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new ClientMessageInspector());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}
