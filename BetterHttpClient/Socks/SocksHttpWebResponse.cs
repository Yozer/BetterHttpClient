using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

// Credit: https://github.com/Yozer/BetterHttpClient

namespace BetterHttpClient.Socks
{
    public class SocksHttpWebResponse : WebResponse
    {
        #region Member Variables

        private WebHeaderCollection _httpResponseHeaders;
        private Uri responseUri;

        #endregion

        #region Constructors

        public SocksHttpWebResponse(Uri responseUri, byte[] httpResponseMessage)
        {
            this.responseUri = responseUri;
            SetHeadersAndResponseContent(httpResponseMessage);
        }

        private SocksHttpWebResponse()
        {
        }

        public static SocksHttpWebResponse CreateErrorResponse(HttpStatusCode statusCode)
        {
            var response = new SocksHttpWebResponse
            {
                StatusCode = statusCode
            };

            return response;
        }

        #endregion

        #region Methods

        private void SetHeadersAndResponseContent(byte[] responseMessage)
        {
            if (responseMessage == null || responseMessage.Length < 4)
                return;

            var headers = string.Empty;
            // get headers part;
            var headerEnd = 0;
            for (; headerEnd < responseMessage.Length - 3; headerEnd++)
            {
                if (responseMessage[headerEnd] == 13 && responseMessage[headerEnd + 1] == 10 && responseMessage[headerEnd + 2] == 13 && responseMessage[headerEnd + 3] == 10)
                {
                    headers = Encoding.ASCII.GetString(responseMessage, 0, headerEnd);
                    break;
                }
            }
            if (string.IsNullOrEmpty(headers))
                return;

            var headerValues = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            // ignore the first line in the header since it is the HTTP response code
            for (var i = 1; i < headerValues.Length; i++)
            {
                var headerEntry = headerValues[i].Split(':');
                Headers.Add(headerEntry[0], headerEntry[1]);
            }

            var statusRegex = new System.Text.RegularExpressions.Regex(@"^HTTP/\d+\.\d+ (\d+)(.*)$");
            var matches = statusRegex.Match(headerValues[0]);
            if (matches.Groups.Count < 3)
                throw new InvalidDataException();
            StatusCode = (HttpStatusCode)int.Parse(matches.Groups[1].Value);
            StatusDescription = matches.Groups[2].Value;

            ResponseContent = new byte[responseMessage.Length - headerEnd - 4];
            Array.Copy(responseMessage, headerEnd + 4, ResponseContent, 0, ResponseContent.Length);

            string transferEncoding = Headers["Transfer-Encoding"];

            switch (transferEncoding)
            {
                case "chunked":
                    DechunkContent();
                    break;
            }

            string contentEncoding = Headers["Content-Encoding"];

            switch (contentEncoding)
            {
                case "gzip":
                    ResponseContent = Decompress(ResponseContent) ?? ResponseContent;
                    break;
            }
        }

        private void DechunkContent()
        {
            using (var output = new MemoryStream())
            using (var ms = new MemoryStream(ResponseContent))
            {
                while (true)
                {
                    var infoLine = new StringBuilder();
                    while (true)
                    {
                        int b = ms.ReadByte();

                        if (b == -1)
                            // End of stream
                            break;

                        if (b == 13)
                            // Ignore
                            continue;

                        if (b == 10)
                        {
                            if (infoLine.Length == 0)
                                // Trim
                                continue;
                            else
                                break;
                        }

                        infoLine.Append((char)b);
                    }
                    if (infoLine.Length == 0)
                        break;

                    int chunkLength = int.Parse(infoLine.ToString(), System.Globalization.NumberStyles.HexNumber);
                    if (chunkLength == 0)
                        break;

                    var buffer = new byte[chunkLength];
                    ms.Read(buffer, 0, chunkLength);
                    output.Write(buffer, 0, buffer.Length);
                }

                ResponseContent = output.ToArray();
            }
        }

        #endregion

        #region WebResponse Members

        public HttpStatusCode StatusCode { get; private set; }

        public string StatusDescription { get; private set; }

        public override Uri ResponseUri
        {
            get
            {
                return this.responseUri;
            }
        }

        private static readonly byte[] GZipHeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 4, 0 };
        private static readonly byte[] GZipLevel10HeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 2, 0 };

        public static bool IsPossiblyGZippedBytes(byte[] a)
        {
            var yes = a.Length > 10;

            if (!yes)
            {
                return false;
            }

            var fail = false;
            for (var i = 0; i < 10; i++)
            {
                if (a[i] != GZipHeaderBytes[i])
                {
                    fail = true;
                    break;
                }
            }

            if (!fail)
                return true;

            for (var i = 0; i < 10; i++)
            {
                if (a[i] != GZipLevel10HeaderBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        private byte[] Decompress(byte[] gzip)
        {
            try
            {
                using (var stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
                {
                    const int size = 4096;
                    var buffer = new byte[size];
                    using (var memory = new MemoryStream())
                    {
                        var count = 0;
                        do
                        {
                            count = stream.Read(buffer, 0, size);
                            if (count > 0)
                            {
                                memory.Write(buffer, 0, count);
                            }
                        } while (count > 0);
                        return memory.ToArray();
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override Stream GetResponseStream()
        {
            return ResponseContent.Length == 0 ? Stream.Null : new MemoryStream(ResponseContent);
        }

        public override void Close()
        {
            /* the base implementation throws an exception */
        }

        public override WebHeaderCollection Headers
        {
            get
            {
                if (_httpResponseHeaders == null)
                {
                    _httpResponseHeaders = new WebHeaderCollection();
                }
                return _httpResponseHeaders;
            }
        }

        public override long ContentLength
        {
            get { return ResponseContent.Length; }
            set { throw new NotSupportedException(); }
        }

        #endregion

        #region Properties

        private byte[] ResponseContent { get; set; }

        public Encoding CorrectEncoding { get; set; }

        #endregion
    }
}