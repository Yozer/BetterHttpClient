using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Threading;
using HttpClient.Socks;

namespace HttpClient
{
    public class HttpClient : WebClient
    {
        private CookieContainer _cookies = new CookieContainer();
      
        public Proxy Proxy { get; set; }
        public int Timeout { get; set; }
        public int NumberOfTry { get; set; }
        public bool SuppressWebException { get; set; } = true;

        public string UserAgent { get; set; }
        public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        public string Referer { get; set; }
        public string AcceptLanguage { get; set; } = "en-US;q=0.7,en;q=0.3";
        public string AcceptEncoding { get; set; } = "gzip";

        public HttpClient(Proxy proxy) : this(proxy, Encoding.UTF8) {  }

        public HttpClient() : this(new Proxy(), Encoding.UTF8) {  }
        public HttpClient(Proxy proxy, Encoding encoding)
        {
            Encoding = encoding;
            Proxy = proxy;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = null;

            if (Proxy.ProxyType != ProxyTypeEnum.Socks)
            {
                request = base.GetWebRequest(address);
            }
            else
            {
                request = SocksHttpWebRequest.Create(address);
                request.Method = base.GetWebRequest(address).Method;
                request.ContentLength = base.GetWebRequest(address).ContentLength;
                request.ContentType = base.GetWebRequest(address).ContentType;
            }

            request.Headers.Add("Cookie", _cookies.GetCookieHeader(address));
            request.Headers.Add("Accept-Language", AcceptLanguage);
            request.Headers.Add("Accept-Encoding", AcceptEncoding);


            if (Proxy.ProxyType != ProxyTypeEnum.Socks)
            {
                var httpRequest = (request as HttpWebRequest);
                httpRequest.UserAgent = UserAgent;
                httpRequest.Accept = Accept;
                httpRequest.Referer = Referer;
                httpRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            }
            else if(Proxy.ProxyType == ProxyTypeEnum.Socks)
            {
                var socksRequest = (request as SocksHttpWebRequest);
                socksRequest.UserAgent = UserAgent;
                socksRequest.Accept = Accept;
                socksRequest.Referer = Referer;
            }

            request.Timeout = Timeout;
            request.Proxy = Proxy.ProxyItem;
            
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            try
            {
                string setCookies = response.Headers["Set-Cookie"];
                _cookies.SetCookies(request.RequestUri, setCookies);
            }
            catch (Exception)
            {

            }

            return response;
        }

        public string Get(string url)
        {
            return Encoding.GetString(DownloadBytes(url, null));
        }

        public string Post(string url, NameValueCollection data)
        {
            return Encoding.GetString(DownloadBytes(url, data));
        }

        public byte[] DownloadBytes(string url, NameValueCollection data)
        {
            int counter = 0;
            WebException lastWebException = null;

            while (counter < NumberOfTry)
            {
                try
                {
                    byte[] result = data == null ? Encoding.GetBytes(DownloadString(url)) : UploadValues(url, data);
                    return result;
                }
                catch (WebException e)
                {
                    lastWebException = e;
                    counter++;
                }
            }

            if (!SuppressWebException && lastWebException != null)
                throw lastWebException;

            return null;
        }
    }


}
