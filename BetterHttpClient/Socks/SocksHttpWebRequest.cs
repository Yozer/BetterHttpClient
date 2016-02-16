using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BetterHttpClient.Socks.Extensions;

namespace BetterHttpClient.Socks
{
    internal class SocksHttpWebRequest : WebRequest
    {
        private static readonly StringCollection validHttpVerbs = new StringCollection { "GET", "HEAD", "POST", "PUT", "DELETE", "TRACE", "OPTIONS" };
        private WebHeaderCollection _HttpRequestHeaders;
        private string _method;
        private byte[] _requestContentBuffer;
        private NeverEndingStream _requestContentStream;
        private SocksHttpWebResponse _response;

        private SocksHttpWebRequest(Uri requestUri)
        {
            RequestUri = requestUri;
        }

        public override int Timeout { get; set; }

        public string UserAgent
        {
            get { return _HttpRequestHeaders["User-Agent"]; }
            set { SetSpecialHeaders("User-Agent", value ?? string.Empty); }
        }

        public string Referer
        {
            get { return _HttpRequestHeaders["Referer"]; }
            set { SetSpecialHeaders("Referer", value ?? string.Empty); }
        }

        public string Accept
        {
            get { return _HttpRequestHeaders["Accept"]; }
            set { SetSpecialHeaders("Accept", value ?? string.Empty); }
        }

        public DecompressionMethods AutomaticDecompression
        {
            get
            {
                var result = DecompressionMethods.None;
                string encoding = _HttpRequestHeaders["Accept-Encoding"] ?? string.Empty;
                foreach (string value in encoding.Split(','))
                {
                    switch (value.Trim())
                    {
                        case "gzip":
                            result |= DecompressionMethods.GZip;
                            break;

                        case "deflate":
                            result |= DecompressionMethods.Deflate;
                            break;
                    }
                }

                return result;
            }
            set
            {
                string result = string.Empty;
                if ((value & DecompressionMethods.GZip) != 0)
                    result = "gzip";
                if ((value & DecompressionMethods.Deflate) != 0)
                {
                    if (!string.IsNullOrEmpty(result))
                        result += ", ";
                    result += "deflate";
                }

                SetSpecialHeaders("Accept-Encoding", result);
            }
        }

        public override Uri RequestUri { get; }

        public override IWebProxy Proxy { get; set; }

        public override WebHeaderCollection Headers
        {
            get { return _HttpRequestHeaders ?? (_HttpRequestHeaders = new WebHeaderCollection()); }
            set
            {
                if (RequestSubmitted)
                {
                    throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");
                }
                _HttpRequestHeaders = value;
            }
        }

        public bool RequestSubmitted { get; private set; }

        public override string Method
        {
            get { return _method ?? "GET"; }
            set
            {
                if (validHttpVerbs.Contains(value))
                {
                    _method = value;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"'{value}' is not a known HTTP verb.");
                }
            }
        }

        public override long ContentLength { get; set; }

        public override string ContentType { get; set; }
        public bool AllowAutoRedirect { get; set; } = true;

        public override WebResponse GetResponse()
        {
            if (Proxy == null)
            {
                throw new InvalidOperationException("Proxy property cannot be null.");
            }
            if (string.IsNullOrEmpty(Method))
            {
                throw new InvalidOperationException("Method has not been set.");
            }

            if (RequestSubmitted)
            {
                return _response;
            }
            _response = InternalGetResponse();
            RequestSubmitted = true;
            return _response;
        }

        public override Stream GetRequestStream()
        {
            if (RequestSubmitted)
            {
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");
            }

            if (_requestContentBuffer == null)
            {
                if (ContentLength == 0)
                {
                    _requestContentStream = new NeverEndingStream();
                    return _requestContentStream;
                }

                _requestContentBuffer = new byte[ContentLength];
            }
            else if (ContentLength == default(long))
            {
                _requestContentBuffer = new byte[int.MaxValue];
            }
            else if (_requestContentBuffer.Length != ContentLength)
            {
                Array.Resize(ref _requestContentBuffer, (int)ContentLength);
            }
            return new MemoryStream(_requestContentBuffer);
        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            if (Proxy == null)
            {
                throw new InvalidOperationException("Proxy property cannot be null.");
            }
            if (string.IsNullOrEmpty(Method))
            {
                throw new InvalidOperationException("Method has not been set.");
            }

            var task = Task.Run<WebResponse>(() =>
            {
                if (RequestSubmitted)
                {
                    return _response;
                }
                _response = InternalGetResponse();
                RequestSubmitted = true;
                return _response;
            });

            return task.AsApm(callback, state);
        }

        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            var task = asyncResult as Task<WebResponse>;

            try
            {
                return task.Result;
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is IOException || ex.InnerException is System.Net.Sockets.SocketException)
                    throw new WebException("Proxy error " + ex.InnerException.Message, ex.InnerException, WebExceptionStatus.ConnectFailure,
                        SocksHttpWebResponse.CreateErrorResponse(HttpStatusCode.GatewayTimeout));
                throw ex.InnerException;
            }
        }

        public new static WebRequest Create(string requestUri)
        {
            return new SocksHttpWebRequest(new Uri(requestUri));
        }

        public new static WebRequest Create(Uri requestUri)
        {
            return new SocksHttpWebRequest(requestUri);
        }

        private void SetSpecialHeaders(string headerName, string value)
        {
            _HttpRequestHeaders.Remove(headerName);
            if (value.Length != 0)
            {
                _HttpRequestHeaders.Add(headerName, value);
            }
        }

        private string BuildHttpRequestMessage(Uri requestUri)
        {
            if (RequestSubmitted)
            {
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");
            }

            // See if we have a stream instead of byte array
            if (_requestContentBuffer == null && _requestContentStream != null)
            {
                _requestContentBuffer = _requestContentStream.ToArray();
                _requestContentStream.ForceClose();
                _requestContentStream.Dispose();
                _requestContentStream = null;
                ContentLength = _requestContentBuffer.Length;
            }

            var message = new StringBuilder();
            message.AppendFormat("{0} {1} HTTP/1.1\r\nHost: {2}\r\n", Method, requestUri, requestUri.Host);

            Headers.Set(HttpRequestHeader.Connection, "close");

            // add the headers
            foreach (var key in Headers.Keys)
            {
                string value = Headers[key.ToString()];
                if (!string.IsNullOrEmpty(value))
                    message.AppendFormat("{0}: {1}\r\n", key, value);
            }

            if (!string.IsNullOrEmpty(ContentType))
            {
                message.AppendFormat("Content-Type: {0}\r\n", ContentType);
            }
            if (ContentLength > 0)
            {
                message.AppendFormat("Content-Length: {0}\r\n", ContentLength);
            }

            // add a blank line to indicate the end of the headers
            message.Append("\r\n");

            // add content
            if (_requestContentBuffer != null && _requestContentBuffer.Length > 0)
            {
                using (var stream = new MemoryStream(_requestContentBuffer, false))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        message.Append(reader.ReadToEnd());
                    }
                }
            }
            else if (_requestContentStream != null)
            {
                _requestContentStream.Position = 0;

                using (var reader = new StreamReader(_requestContentStream))
                {
                    message.Append(reader.ReadToEnd());
                }


                _requestContentStream.ForceClose();
                _requestContentStream.Dispose();
            }

            return message.ToString();
        }

        private SocksHttpWebResponse InternalGetResponse()
        {
            Uri requestUri = RequestUri;

            int redirects = 0;
            const int maxAutoredirectCount = 10;
            while (redirects++ < maxAutoredirectCount)
            {
                // Loop while redirecting

                var proxyUri = Proxy.GetProxy(requestUri);
                var ipAddress = GetProxyIpAddress(proxyUri);
                var response = new List<byte>();

                using (var client = new TcpClient(ipAddress.ToString(), proxyUri.Port))
                {
                    int timeout = Timeout;
                    if (timeout == 0)
                        timeout = 30 * 1000;
                    client.ReceiveTimeout = timeout;
                    client.SendTimeout = timeout;
                    var networkStream = client.GetStream();
                    // auth
                    var buf = new byte[300];
                    buf[0] = 0x05; // Version
                    buf[1] = 0x01; // NMETHODS
                    buf[2] = 0x00; // No auth-method
                    networkStream.Write(buf, 0, 3);

                    networkStream.Read(buf, 0, 2);
                    if (buf[0] != 0x05)
                    {
                        throw new IOException("Invalid Socks Version");
                    }
                    if (buf[1] == 0xff)
                    {
                        throw new IOException("Socks Server does not support no-auth");
                    }
                    if (buf[1] != 0x00)
                    {
                        throw new Exception("Socks Server did choose bogus auth");
                    }

                    // connect
                    var destIP = Dns.GetHostEntry(requestUri.DnsSafeHost).AddressList[0];
                    var index = 0;
                    buf[index++] = 0x05; // version 5 .
                    buf[index++] = 0x01; // command = connect.
                    buf[index++] = 0x00; // Reserve = must be 0x00

                    buf[index++] = 0x01; // Address is full-qualified domain name.
                    var rawBytes = destIP.GetAddressBytes();
                    rawBytes.CopyTo(buf, index);
                    index += (ushort)rawBytes.Length;

                    var portBytes = BitConverter.GetBytes(Uri.UriSchemeHttps == requestUri.Scheme ? 443 : 80);
                    for (var i = portBytes.Length - 3; i >= 0; i--)
                        buf[index++] = portBytes[i];


                    networkStream.Write(buf, 0, index);

                    networkStream.Read(buf, 0, 4);
                    if (buf[0] != 0x05)
                    {
                        throw new IOException("Invalid Socks Version");
                    }
                    if (buf[1] != 0x00)
                    {
                        throw new IOException($"Socks Error {buf[1]:X}");
                    }

                    var rdest = string.Empty;
                    switch (buf[3])
                    {
                        case 0x01: // IPv4
                            networkStream.Read(buf, 0, 4);
                            var v4 = BitConverter.ToUInt32(buf, 0);
                            rdest = new IPAddress(v4).ToString();
                            break;
                        case 0x03: // Domain name
                            networkStream.Read(buf, 0, 1);
                            if (buf[0] == 0xff)
                            {
                                throw new IOException("Invalid Domain Name");
                            }
                            networkStream.Read(buf, 1, buf[0]);
                            rdest = Encoding.ASCII.GetString(buf, 1, buf[0]);
                            break;
                        case 0x04: // IPv6
                            var octets = new byte[16];
                            networkStream.Read(octets, 0, 16);
                            rdest = new IPAddress(octets).ToString();
                            break;
                        default:
                            throw new IOException("Invalid Address type");
                    }
                    networkStream.Read(buf, 0, 2);
                    var rport = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(buf, 0));

                    Stream readStream = null;
                    if (Uri.UriSchemeHttps == requestUri.Scheme)
                    {
                        var ssl = new SslStream(networkStream);
                        ssl.AuthenticateAsClient(requestUri.DnsSafeHost);
                        readStream = ssl;
                    }
                    else
                    {
                        readStream = networkStream;
                    }

                    string requestString = BuildHttpRequestMessage(requestUri);

                    var request = Encoding.ASCII.GetBytes(requestString);
                    readStream.Write(request, 0, request.Length);
                    readStream.Flush();

                    var buffer = new byte[client.ReceiveBufferSize];

                    var readlen = 0;
                    do
                    {
                        readlen = readStream.Read(buffer, 0, buffer.Length);
                        response.AddRange(buffer.Take(readlen));
                    } while (readlen != 0);

                    readStream.Close();
                }

                var webResponse = new SocksHttpWebResponse(requestUri, response.ToArray());

                if (webResponse.StatusCode == HttpStatusCode.Moved || webResponse.StatusCode == HttpStatusCode.MovedPermanently)
                {
                    string redirectUrl = webResponse.Headers["Location"];
                    if (string.IsNullOrEmpty(redirectUrl))
                        throw new WebException("Missing location for redirect");

                    requestUri = new Uri(requestUri, redirectUrl);
                    if (AllowAutoRedirect)
                    {
                        continue;
                    }
                    return webResponse;
                }

                if ((int)webResponse.StatusCode < 200 || (int)webResponse.StatusCode > 299)
                    throw new WebException(webResponse.StatusDescription, null, WebExceptionStatus.UnknownError, webResponse);

                return webResponse;
            }

            throw new WebException("Too many redirects", null, WebExceptionStatus.ProtocolError, SocksHttpWebResponse.CreateErrorResponse(HttpStatusCode.BadRequest));
        }

        private static IPAddress GetProxyIpAddress(Uri proxyUri)
        {
            IPAddress ipAddress;
            if (!IPAddress.TryParse(proxyUri.Host, out ipAddress))
            {
                try
                {
                    return Dns.GetHostEntry(proxyUri.Host).AddressList[0];
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Unable to resolve proxy hostname '{proxyUri.Host}' to a valid IP address.", e);
                }
            }
            return ipAddress;
        }
    }
}