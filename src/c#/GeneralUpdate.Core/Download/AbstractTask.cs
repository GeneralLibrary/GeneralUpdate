using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Exceptions.CustomArgs;
using GeneralUpdate.Core.Exceptions.CustomException;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace GeneralUpdate.Core.Download
{
    public abstract class AbstractTask<T> : WebClient, ITask<T>
    {
        #region Private Members

        private DownloadFileRangeState _fileRange;
        private long _beforBytes;
        private long _receivedBytes;
        private long _totalBytes;
        private int _timeOut;

        #endregion Private Members

        #region Public Properties

        public delegate void MultiDownloadProgressChangedEventHandler(object sender, MultiDownloadProgressChangedEventArgs e);

        public event MultiDownloadProgressChangedEventHandler MultiDownloadProgressChanged;

        public delegate void MultiAsyncCompletedEventHandler(object sender, AsyncCompletedEventArgs e);

        public event MultiAsyncCompletedEventHandler MultiDownloadFileCompleted;

        protected Timer SpeedTimer { get; set; }
        protected DateTime StartTime { get; set; }

        public long BeforBytes
        {
            get
            {
                return Interlocked.Read(ref _beforBytes);
            }

            set
            {
                Interlocked.Exchange(ref _beforBytes, value);
            }
        }

        public long ReceivedBytes
        {
            get
            {
                return Interlocked.Read(ref _receivedBytes);
            }

            set
            {
                Interlocked.Exchange(ref _receivedBytes, value);
            }
        }

        public long TotalBytes
        {
            get
            {
                return Interlocked.Read(ref _totalBytes);
            }
            set
            {
                Interlocked.Exchange(ref _totalBytes, value);
            }
        }

        #endregion Public Properties

        #region Public Methods

        public void InitTimeOut(int timeout)
        {
            if (timeout <= 0) timeout = 30;
            _timeOut = 1000 * timeout;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request;
            if (address.Scheme == "https")
            {
                ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => { return true; };
                request = (HttpWebRequest)base.GetWebRequest(address);
                request.ProtocolVersion = HttpVersion.Version10;
            }
            else
            {
                request = (HttpWebRequest)base.GetWebRequest(address);
            }

            request.Timeout = _timeOut;
            request.ReadWriteTimeout = _timeOut;
            request.AllowAutoRedirect = false;
            request.AllowWriteStreamBuffering = true;

            var cookieContainer = new CookieContainer();
            var collection = new NameValueCollection
            {
                { "Accept-Language", "zh-cn,zh;q=0.5" },
                { "Accept-Encoding", "gzip,deflate" },
                { "Accept-Charset", "GB2312,utf-8;q=0.7,*;q=0.7" },
                { "Keep-Alive", "115" }
            };
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            request.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 5.1; zh-CN; rv:1.9.2.13) Gecko/20101203 Firefox/3.6.13";
            request.Headers.Add(collection);
            request.CookieContainer = cookieContainer;
            request.ServicePoint.BindIPEndPointDelegate = (servicePoint, remoteEndPoint, retryCount) =>
            {
                if (remoteEndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    return new IPEndPoint(IPAddress.IPv6Any, 0);
                else
                    return new IPEndPoint(IPAddress.Any, 0);
            };
            return request;
        }

        public new void CancelAsync()
        {
            base.CancelAsync();
            if (_fileRange != null && _fileRange.IsRangeDownload) _fileRange.IsRangeDownload = false;
        }

        public void DownloadFileRange(string url, string path, object userState)
        {
            if (_fileRange != null && _fileRange.IsRangeDownload) return;
            _fileRange = new DownloadFileRangeState(path, userState, this);
            _fileRange.OnCompleted = () => MultiDownloadFileCompleted;
            _fileRange.IsRangeDownload = true;
            long startPos = CheckFile(_fileRange);
            if (startPos == -1) return;
            try
            {
                _fileRange.Request = (HttpWebRequest)GetWebRequest(new Uri(url));
                _fileRange.Request.ReadWriteTimeout = _timeOut;
                _fileRange.Request.Timeout = _timeOut;
                _fileRange.Respone = _fileRange.Request.GetResponse();
                _fileRange.Stream = _fileRange.Respone.GetResponseStream();
                if (_fileRange.Respone.ContentLength == startPos)
                {
                    _fileRange.Close();
                    File.Move(_fileRange.TempPath, _fileRange.Path);
                    _fileRange.Done(true);
                    return;
                }
                if (startPos > 0) _fileRange.Request.AddRange((int)startPos);
                long totalBytesReceived = _fileRange.Respone.ContentLength + startPos;
                long bytesReceived = startPos;
                if (totalBytesReceived != 0 && bytesReceived >= totalBytesReceived)
                {
                    _fileRange.Close();
                    try
                    {
                        if (File.Exists(_fileRange.Path)) File.Delete(_fileRange.Path);
                        File.Move(_fileRange.TempPath, _fileRange.Path);
                    }
                    catch (Exception e)
                    {
                        _fileRange.Exception = e;
                        _fileRange.Close();
                    }
                }
                else
                {
                    WriteFile(_fileRange, startPos);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new GeneralUpdateException<HttpExceptionArgs>(new HttpExceptionArgs(url, 400, "Download file failed."), ex.Message, ex.InnerException);
            }
            catch (Exception e)
            {
                _fileRange.Exception = e;
                throw new Exception($"'DownloadFileRange' This function has an internal exception : {e.Message} .", e.InnerException);
            }
            finally
            {
                if (_fileRange != null) _fileRange.Close();
            }
        }

        #endregion Public Methods

        #region Private Methods

        private long CheckFile(DownloadFileRangeState state)
        {
            long startPos = 0;
            if (File.Exists(state.TempPath))
            {
                state.FileStream = File.OpenWrite(state.TempPath);
                startPos = state.FileStream.Length;
                state.FileStream.Seek(startPos, SeekOrigin.Current);
            }
            else
            {
                try
                {
                    string direName = Path.GetDirectoryName(state.TempPath);
                    if (!Directory.Exists(direName)) Directory.CreateDirectory(direName);
                    state.FileStream = new FileStream(state.TempPath, FileMode.Create);
                }
                catch (Exception e)
                {
                    state.Exception = e;
                    startPos = -1;
                    state.Close();
                }
            }
            return startPos;
        }

        private void WriteFile(DownloadFileRangeState state, long startPos)
        {
            var bytesReceived = startPos;
            byte[] bytes = new byte[1024];
            bool isDownloadCompleted = false;
            var totalBytesReceived = state.Respone.ContentLength + startPos;
            int readSize = state.Stream.Read(bytes, 0, 1024);
            while (readSize > 0 && state.IsRangeDownload)
            {
                if (state == null || state.FileStream == null) break;
                lock (state.FileStream)
                {
                    if (MultiDownloadProgressChanged != null)
                        MultiDownloadProgressChanged(this, new MultiDownloadProgressChangedEventArgs(bytesReceived, totalBytesReceived, ((float)bytesReceived / totalBytesReceived), state.UserState));
                    state.FileStream.Write(bytes, 0, readSize);
                    bytesReceived += readSize;
                    if (totalBytesReceived != 0 && bytesReceived >= totalBytesReceived)
                    {
                        try
                        {
                            state.Close();
                            if (File.Exists(state.Path)) File.Delete(state.Path);
                            File.Move(state.TempPath, state.Path);
                            isDownloadCompleted = true;
                            state.Done(isDownloadCompleted);
                        }
                        catch (Exception e)
                        {
                            state.Exception = e;
                            state.Done(false);
                        }
                    }
                    else
                    {
                        readSize = state.Stream.Read(bytes, 0, 1024);
                    }
                }
            }
            if (!isDownloadCompleted) state.Exception = new Exception("Request for early closure");
        }

        #endregion Private Methods

        private class DownloadFileRangeState
        {
            #region Private Members

            private const string tmpSuffix = ".temp";
            private Func<MultiAsyncCompletedEventHandler> _onDownloadCompleted = null;
            private HttpWebRequest _request = null;
            private WebResponse _respone = null;
            private Stream _stream = null;
            private FileStream _fileStream = null;
            private Exception _exception = null;
            private bool _isRangeDownload;
            private string _tempPath;
            private string _path;
            private object _userState;
            private object _sender;

            #endregion Private Members

            #region Constructors

            public DownloadFileRangeState(string path, object userState, object sender)
            {
                _path = path;
                _userState = userState;
                _tempPath = _path + tmpSuffix;
                _sender = sender;
            }

            #endregion Constructors

            #region Public Properties

            public Func<MultiAsyncCompletedEventHandler> OnCompleted { get => _onDownloadCompleted; set => _onDownloadCompleted = value; }
            public HttpWebRequest Request { get => _request; set => _request = value; }
            public WebResponse Respone { get => _respone; set => _respone = value; }
            public Stream Stream { get => _stream; set => _stream = value; }
            public FileStream FileStream { get => _fileStream; set => _fileStream = value; }
            public Exception Exception { get => _exception; set => _exception = value; }
            public bool IsRangeDownload { get => _isRangeDownload; set => _isRangeDownload = value; }
            public string TempPath { get => _tempPath; }
            public string Path { get => _path; }
            public object UserState { get => _userState; }
            public object Sender { get => _sender; }

            #endregion Public Properties

            #region Public Methods

            public void Close()
            {
                if (_fileStream != null)
                {
                    _fileStream.Flush();
                    _fileStream.Close();
                    _fileStream = null;
                }
                if (_stream != null) _stream.Close();
                if (_respone != null) _respone.Close();
                if (_request != null) _request.Abort();
                if (_exception != null) throw new Exception(_exception.Message);
            }

            public void Done(bool isCompleted)
            {
                if (_exception != null) throw new Exception(_exception.Message);
                _onDownloadCompleted()(Sender, new AsyncCompletedEventArgs(_exception, isCompleted, _userState));
            }

            #endregion Public Methods
        }
    }
}