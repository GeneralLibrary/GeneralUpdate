using GeneralUpdate.Core.ContentProvider;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.ClientCore.Hubs
{
    public sealed class VersionHub<TParameter> where TParameter : class
    {
        #region Private Members

        private const string ReceiveMessageflag = "ReceiveMessage";
        private const string SendMessageflag = "SendMessage";
        private const string Onlineflag = "Online";

        private HubConnection _connection = null;
        private static VersionHub<TParameter> _instance;
        private static readonly object _lock = new object();
        private Action<TParameter> _receiveMessageCallback;
        private Action<string> _onlineMessageCallback;
        private Action<string> _reconnectedCallback;
        private string _name;

        #endregion Private Members

        #region Constructors

        private VersionHub()
        { }

        #endregion Constructors

        #region Public Properties

        public static VersionHub<TParameter> Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new VersionHub<TParameter>();
                    }
                }
                return _instance;
            }
        }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Subscribe to the latest version.
        /// </summary>
        /// <param name="url">remote server address , E.g : https://127.0.0.1:8080/versionhub .</param>
        /// <param name="name">The name needs to be guaranteed to be unique.</param>
        /// <param name="receiveMessageCallback">Receive server push callback function, The caller needs to implement the update process.</param>
        /// <param name="onlineMessageCallback">Receive online and offline notification callback function.</param>
        /// <param name="reconnectedCallback">Reconnect notification callback function.</param>
        /// <exception cref="Exception">Subscribe exception.</exception>
        public void Subscribe(string url, string name, Action<TParameter> receiveMessageCallback, Action<string> onlineMessageCallback = null, Action<string> reconnectedCallback = null)
        {
            if (string.IsNullOrWhiteSpace(url) || receiveMessageCallback == null) throw new Exception("Subscription key parameter cannot be null !");

            try
            {
                _name = name;
                _receiveMessageCallback = receiveMessageCallback;
                _onlineMessageCallback = onlineMessageCallback;
                _reconnectedCallback = reconnectedCallback;
                if (_connection == null)
                {
                    _connection = new HubConnectionBuilder()
                            .WithUrl(url)
                            .WithAutomaticReconnect(new RandomRetryPolicy())
                            .Build();
                    _connection.On<string, string>(ReceiveMessageflag, OnReceiveMessage);
                    _connection.On<string>(Onlineflag, OnOnlineMessage);
                    _connection.Reconnected += OnReconnected;
                    _connection.Closed += OnClosed;
                }
                _connection.StartAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' Subscribe error :  {ex.Message}", ex.InnerException);
            }
        }

        /// <summary>
        /// Send message to server.[Not recommended for now]
        /// </summary>
        /// <param name="user"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public async Task Send(string msg)
        {
            try
            {
                if (_connection == null) return;
                await _connection.InvokeAsync(SendMessageflag, _name, msg);
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' Send error :  {ex.Message}", ex.InnerException);
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Receives the message.
        /// </summary>
        /// <param name="message"></param>
        private void OnReceiveMessage(string name, string message)
        {
            if (_receiveMessageCallback == null || string.IsNullOrWhiteSpace(message)) return;
            try
            {
                var clientParameter = FileProvider.Deserialize<TParameter>(message);
                if (clientParameter == null) throw new ArgumentNullException($"'VersionHub' Receiving server push version information deserialization failed , receive content :  {message} .");
                _receiveMessageCallback.Invoke(clientParameter);
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' Receive message error :  {ex.Message}", ex.InnerException);
            }
        }

        /// <summary>
        /// Online and offline notification.
        /// </summary>
        /// <param name="message"></param>
        private void OnOnlineMessage(string message)
        {
            try
            {
                if (_onlineMessageCallback != null) _onlineMessageCallback.Invoke(message);
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' Online message error :  {ex.Message}", ex.InnerException);
            }
        }

        /// <summary>
        /// Reconnection notice.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private Task OnReconnected(string arg)
        {
            try
            {
                if (_reconnectedCallback != null) _reconnectedCallback.Invoke(arg);
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' On reconnected error :  {ex.Message}", ex.InnerException);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Shut down.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task OnClosed(Exception arg)
        {
            try
            {
                if (arg != null) throw new Exception($"'VersionHub' On closed internal exception :  {arg.Message}", arg.InnerException);

                if (_connection == null) return;
                await Task.Delay(new Random().Next(0, 3) * 1000);
                await _connection.StartAsync();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new ArgumentOutOfRangeException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new Exception($"'VersionHub' On closed error :  {ex.Message}", ex.InnerException);
            }
        }

        #endregion Private Methods
    }
}