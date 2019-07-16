using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Anub.Abp.RTSP
{
    public class WebSocketManagerMiddleware
    {
        private readonly RequestDelegate _next;
        private WebSocketHandler _webSocketHandler { get; set; }

        private readonly ILogger<WebSocketManagerMiddleware> Logger;

        public WebSocketManagerMiddleware(RequestDelegate next, WebSocketHandler webSocketHandler, ILogger<WebSocketManagerMiddleware> logger)
        {
            _next = next;
            _webSocketHandler = webSocketHandler;

            Logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next.Invoke(context);
                return;
            }
            if (context.Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
            {
                context.Response.Headers.Add("Sec-WebSocket-Protocol", context.Request.Headers["Sec-WebSocket-Protocol"]);
            }
            var socket = await context.WebSockets.AcceptWebSocketAsync();
            _webSocketHandler.OnConnected(socket);

            await Receive(socket, async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        await _webSocketHandler.OnDisconnected(socket);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(String.Format("ws Close Error: {0}", ex.Message));
                    }
                    return;
                }
                else
                {
                    try
                    {
                        await _webSocketHandler.ReceiveAsync(socket, result, buffer);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(string.Format("ws Receive Error: {0}", ex.Message));
                    }
                    return;
                }

            });

            //TODO - investigate the Kestrel exception thrown when this is the last middleware
            //await _next.Invoke(context);
        }

        private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 4];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer: new ArraySegment<byte>(buffer),
                                                       cancellationToken: CancellationToken.None);
                if (result != null && result.Count > 0)
                {
                    var validbuffer = new byte[result.Count];
                    Array.Copy(buffer, validbuffer, result.Count);
                    handleMessage(result, validbuffer);
                }
                else
                    handleMessage(result, null);
            }
        }
    }
}
