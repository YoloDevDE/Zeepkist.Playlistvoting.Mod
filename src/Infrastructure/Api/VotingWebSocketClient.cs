using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using PlaylistVoting.Core.Models;
using Websocket.Client;

namespace PlaylistVoting.Infrastructure.Api;

public class VotingWebSocketClient : IDisposable
{
    private readonly string _baseUrl;
    private readonly string _hostId;
    private readonly ManualLogSource _logger;
    private readonly string _token;
    private WebsocketClient _client;
    private IDisposable _messageSubscription;
    private IDisposable _reconnectionSubscription;

    public VotingWebSocketClient(string baseUrl, string hostId, string token, ManualLogSource logger)
    {
        _baseUrl = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
        if (_baseUrl.EndsWith("/api/playlistvoting"))
        {
            _baseUrl = _baseUrl.Substring(0, _baseUrl.Length - "/api/playlistvoting".Length);
        }
        else if (_baseUrl.EndsWith("/api/playlistvoting/"))
        {
            _baseUrl = _baseUrl.Substring(0, _baseUrl.Length - "/api/playlistvoting/".Length);
        }

        if (!_baseUrl.EndsWith("/"))
        {
            _baseUrl += "/";
        }

        // Use /ws-dashboard as the primary endpoint. 
        // Spring STOMP usually upgrades this directly.
        _baseUrl += "ws-dashboard";

        if (!string.IsNullOrEmpty(token))
        {
            _baseUrl += "?token=" + Uri.EscapeDataString(token);
        }

        _hostId = hostId;
        _token = token;
        _logger = logger;
    }

    public void Dispose()
    {
        _messageSubscription?.Dispose();
        _reconnectionSubscription?.Dispose();
        _client?.Dispose();
    }

    public event Action<VotingResultResponse> OnResultReceived;
    public event Action OnDisconnected;

    public async Task ConnectAsync()
    {
        if (_client != null)
        {
            return;
        }

        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to set TLS 1.2: {ex.Message}");
        }

        Func<ClientWebSocket> factory = () =>
        {
            ClientWebSocket client = new ClientWebSocket();
            client.Options.AddSubProtocol("v10.stomp");
            client.Options.AddSubProtocol("v11.stomp");
            client.Options.AddSubProtocol("v12.stomp");
            return client;
        };

        _client = new WebsocketClient(new Uri(_baseUrl), factory);
        _client.Name = "VotingWebSocket";
        _client.ReconnectTimeout = TimeSpan.FromSeconds(30);
        _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);

        _reconnectionSubscription = _client.ReconnectionHappened.Subscribe(info =>
        {
            _logger.LogInfo($"WebSocket reconnection happened, type: {info.Type}");
            Task.Run(async () =>
            {
                try
                {
                    await SendStompConnectAsync();
                    await SubscribeAsync($"/topic/votes/{_hostId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error during STOMP setup after reconnect: {ex}");
                }
            });
        });

        _messageSubscription = _client.MessageReceived.Subscribe(msg =>
        {
            if (msg.Text != null)
            {
                string[] frames = msg.Text.Split('\0');
                foreach (string frame in frames)
                {
                    HandleStompMessage(frame);
                }
            }
        });

        _client.DisconnectionHappened.Subscribe(info =>
        {
            _logger.LogWarning($"WebSocket disconnected, type: {info.Type}");
            OnDisconnected?.Invoke();
        });

        try
        {
            _logger.LogInfo($"Connecting to WebSocket: {_baseUrl}");
            await _client.Start();
            _logger.LogInfo("WebSocket client started.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"WebSocket connection error: {ex}");
            throw;
        }
    }

    private async Task SendStompConnectAsync()
    {
        // STOMP CONNECT frame. We send the Authorization header here.
        // We also use heart-beat:0,0 to disable them if we don't have a timer to send pings.
        string connect = "CONNECT\r\n" +
                         "accept-version:1.1,1.2\r\n" +
                         "heart-beat:0,0\r\n" +
                         "Authorization:Bearer " + _token + "\r\n" +
                         "token:" + _token + "\r\n" +
                         "\r\n\0";
        await SendStringAsync(connect);
    }

    private async Task SubscribeAsync(string destination)
    {
        string subscribe = "SUBSCRIBE\r\n" +
                           "id:sub-0\r\n" +
                           $"destination:{destination}\r\n" +
                           "ack:auto\r\n" +
                           "\r\n\0";
        await SendStringAsync(subscribe);
    }

    public void SendTimer(string time)
    {
        if (_client == null || !_client.IsRunning)
        {
            return;
        }

        string frame = "SEND\r\n" +
                       "destination:/app/timer\r\n" +
                       "content-type:application/json\r\n" +
                       "\r\n" +
                       "\"" + time + "\"\0";
        _client.Send(frame);
    }

    private async Task SendStringAsync(string data)
    {
        _client.Send(data);
        await Task.CompletedTask;
    }


    private void HandleStompMessage(string message)
    {
        // Skip heart-beats
        message = message.TrimStart('\n', '\r', ' ');
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.StartsWith("MESSAGE"))
        {
            _logger.LogDebug($"STOMP Message received: {message}");
            int bodyStartIndex = message.IndexOf("\n\n", StringComparison.Ordinal);
            int bodyOffset = 2;

            if (bodyStartIndex == -1)
            {
                bodyStartIndex = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                bodyOffset = 4;
            }

            if (bodyStartIndex != -1)
            {
                string body = message.Substring(bodyStartIndex + bodyOffset).TrimEnd('\0').Trim();
                if (string.IsNullOrEmpty(body) || body == "\"\"")
                {
                    return;
                }

                try
                {
                    VotingResultResponse result = JsonConvert.DeserializeObject<VotingResultResponse>(body);
                    if (result != null)
                    {
                        OnResultReceived?.Invoke(result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error parsing WebSocket message body: {ex.Message}");
                    _logger.LogDebug($"Problematic body: {body}");
                }
            }
        }
        else if (message.StartsWith("ERROR"))
        {
            _logger.LogError($"STOMP Error received: {message}");
        }
        else if (message.StartsWith("CONNECTED"))
        {
            _logger.LogInfo("STOMP Connected.");
        }
    }
}