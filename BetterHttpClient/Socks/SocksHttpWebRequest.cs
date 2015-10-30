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

namespace BetterHttpClient.Socks
{
    class SocksHttpWebRequest : WebRequest
    {
        private WebHeaderCollection _HttpRequestHeaders;
        private string _method;
        private SocksHttpWebResponse _response;
        private string _requestMessage;
        private byte[] _requestContentBuffer;


        static readonly StringCollection validHttpVerbs = new StringCollection { "GET", "HEAD", "POST", "PUT", "DELETE", "TRACE", "OPTIONS" };

        public override int Timeout { get; set; }
        public string UserAgent
        {
            get
            {
                return _HttpRequestHeaders["User-Agent"];
            }
            set
            {
                SetSpecialHeaders("User-Agent", value);
            }
        }

        public string Referer
        {
            get
            {
                return _HttpRequestHeaders["Referer"];
            }
            set
            {
                SetSpecialHeaders("Referer", value);
            }
        }
        public string Accept
        {
            get
            {
                return _HttpRequestHeaders["Accept"];
            }
            set
            {
                SetSpecialHeaders("Accept", value);
            }
        }

        private SocksHttpWebRequest(Uri requestUri)
        {
            RequestUri = requestUri;
        }

        public override WebResponse GetResponse()
        {
            if (Proxy == null)
            {
                throw new InvalidOperationException("Proxy property cannot be null.");
            }
            if (String.IsNullOrEmpty(Method))
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
            get
            {
                return _method ?? "GET";
            }
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

        public override Stream GetRequestStream()
        {
            if (RequestSubmitted)
            {
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");
            }

            if (_requestContentBuffer == null)
            {
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


        public static new WebRequest Create(string requestUri)
        {
            return new SocksHttpWebRequest(new Uri(requestUri));
        }

        public static new WebRequest Create(Uri requestUri)
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
        private string BuildHttpRequestMessage()
        {
            if (RequestSubmitted)
            {
                throw new InvalidOperationException("This operation cannot be performed after the request has been submitted.");
            }

            var message = new StringBuilder();
            message.AppendFormat("{0} {1} HTTP/1.0\r\nHost: {2}\r\n", Method, RequestUri.PathAndQuery, RequestUri.Host);

            // add the headers
            foreach (var key in Headers.Keys)
            {
                message.AppendFormat("{0}: {1}\r\n", key, Headers[key.ToString()]);
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

            return message.ToString();
        }

        private SocksHttpWebResponse InternalGetResponse()
        {
            var proxyUri = Proxy.GetProxy(RequestUri);
            var ipAddress = GetProxyIpAddress(proxyUri);
            List<byte> response = new List<byte>();

            using (var client = new TcpClient(ipAddress.ToString(), proxyUri.Port))
            {
                client.ReceiveTimeout = Timeout;
                client.SendTimeout = Timeout;
                NetworkStream networkStream = client.GetStream();
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
                IPAddress destIP = Dns.GetHostEntry(RequestUri.DnsSafeHost).AddressList[0];
                int index = 0;
                buf[index++] = 0x05;   // version 5 .
                buf[index++] = 0x01;   // command = connect.
                buf[index++] = 0x00;    // Reserve = must be 0x00

                buf[index++] = 0x01;   // Address is full-qualified domain name.
                var rawBytes = destIP.GetAddressBytes();
                rawBytes.CopyTo(buf, index);
                index += (ushort)rawBytes.Length;

                byte[] portBytes = BitConverter.GetBytes(Uri.UriSchemeHttps == RequestUri.Scheme ? 443 : 80);
                for (int i = portBytes.Length - 3; i >= 0; i--)
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
                if (Uri.UriSchemeHttps == RequestUri.Scheme)
                {
                    var ssl = new SslStream(networkStream);
                    ssl.AuthenticateAsClient(RequestUri.DnsSafeHost);
                    readStream = ssl;
                }
                else
                {
                    readStream = networkStream;
                }

                var request = Encoding.ASCII.GetBytes(RequestMessage);
                readStream.Write(request, 0, request.Length);
                readStream.Flush();

                byte[] buffer = new byte[client.ReceiveBufferSize];

                int readlen = 0;
                do
                {
                    readlen = readStream.Read(buffer, 0, buffer.Length);
                    response.AddRange(buffer.Take(readlen));

                } while (readlen != 0);

                readStream.Close();
            }

            return new SocksHttpWebResponse(response.ToArray());
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

        public string RequestMessage
        {
            get
            {
                if (string.IsNullOrEmpty(_requestMessage))
                {
                    _requestMessage = BuildHttpRequestMessage();
                }
                return _requestMessage;
            }
        }
    }

}
