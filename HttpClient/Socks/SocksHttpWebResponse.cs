using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace HttpClient.Socks
{
    internal class SocksHttpWebResponse : WebResponse
    {
        #region Member Variables

        private WebHeaderCollection _httpResponseHeaders;

        #endregion

        #region Constructors

        public SocksHttpWebResponse(byte[] httpResponseMessageMaybeGzip)
        {
            byte[] decompressed = null;

            if (IsPossiblyGZippedBytes(httpResponseMessageMaybeGzip))
            {
                decompressed = Decompress(httpResponseMessageMaybeGzip);
            }

            SetHeadersAndResponseContent(decompressed ?? httpResponseMessageMaybeGzip);
        }

        #endregion

        #region WebResponse Members

        private static byte[] GZipHeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 4, 0 };
        private static byte[] GZipLevel10HeaderBytes = { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 2, 0 };

        public static bool IsPossiblyGZippedBytes(byte[] a)
        {
            var yes = a.Length > 10;

            if (!yes)
            {
                return false;
            }

            bool fail = false;
            for (int i = 0; i < 10; i++)
            {
                if (a[i] != GZipHeaderBytes[i])
                {
                    fail = true;
                    break;
                }
            }

            if (!fail)
                return true;

            for (int i = 0; i < 10; i++)
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
                using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
                {
                    const int size = 4096;
                    byte[] buffer = new byte[size];
                    using (MemoryStream memory = new MemoryStream())
                    {
                        int count = 0;
                        do
                        {
                            count = stream.Read(buffer, 0, size);
                            if (count > 0)
                            {
                                memory.Write(buffer, 0, count);
                            }
                        }
                        while (count > 0);
                        return memory.ToArray();
                    }
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public override Stream GetResponseStream()
        {
            return ResponseContent.Length == 0 ? Stream.Null : new MemoryStream(ResponseContent);
        }

        public override void Close() { /* the base implementation throws an exception */ }

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
            get
            {
                return ResponseContent.Length;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        #endregion

        #region Methods

        private void SetHeadersAndResponseContent(byte[] responseMessage)
        {
            if (responseMessage == null || responseMessage.Length < 4)
                return;

            string headers = string.Empty;
            // get headers part;
            int headerEnd = 0;
            for (; headerEnd < responseMessage.Length - 3; headerEnd++)
            {
                if (responseMessage[headerEnd] == 13 && responseMessage[headerEnd + 1] == 10 && responseMessage[headerEnd + 2] == 13 && responseMessage[headerEnd + 3] == 10)
                {
                    headers = Encoding.ASCII.GetString(responseMessage, 0, headerEnd);
                    break;
                }
            }
            if(string.IsNullOrEmpty(headers))
                return;
            
            var headerValues = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            // ignore the first line in the header since it is the HTTP response code
            for (int i = 1; i < headerValues.Length; i++)
            {
                var headerEntry = headerValues[i].Split(':');
                Headers.Add(headerEntry[0], headerEntry[1]);
            }

            ResponseContent = new byte[responseMessage.Length - headerEnd - 4];
            Array.Copy(responseMessage, headerEnd + 4, ResponseContent, 0, ResponseContent.Length);
        }

        #endregion

        #region Properties

        private byte[] ResponseContent { get; set; }

        public Encoding CorrectEncoding
        {
            get; set;
        }

        #endregion

    }

}
