using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using BetterHttpClient.Socks;

namespace BetterHttpClient
{
    public class HttpClient : WebClient
    {
        private int _numberOfAttempts = 4;
        private TimeSpan _timeout = TimeSpan.FromSeconds(60);
        private Proxy _proxy;

        /// <summary>
        /// Cookie container.
        /// </summary>
        public CookieContainer Cookies { get; set; } = new CookieContainer();

        /// <summary>
        /// Proxy which should be used for request.
        /// Set null or proxy with type ProxyTypeEnum.None if you want to perform request without proxy.
        /// </summary>
        public new Proxy Proxy
        {
            get { return _proxy; }
            set { _proxy = value ?? new Proxy(); }
        }
        /// <summary>
        /// Timeout for request.
        /// </summary>
        /// <value>Has to be greater than 5 milliseconds.<para />
        /// Default value: 60 seconds.</value>
        public TimeSpan Timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                if (value.TotalMilliseconds < 5)
                    throw new ArgumentOutOfRangeException("Timeout has to be greater or equal than 5 milliseconds.");
                _timeout = value;
            }
        }
        /// <summary>
        /// Set number of attempts that can be made to execute request.
        /// </summary>
        /// <value>Default value: 4 attempts.<para />
        /// Should be greater than 1</value>
        public int NumberOfAttempts
        {
            get { return _numberOfAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Value has to be greater than one.");

                _numberOfAttempts = value;
            }
        }

        /// <summary>
        /// Set User-Agent header.
        /// </summary>
        /// <value>Default value: "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0"</value>
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0";
        /// <summary>
        /// Set Accept header.
        /// </summary>
        /// <value>Default value: "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"</value>
        public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        /// <summary>
        /// Set Referer header.
        /// </summary>
        /// <value>Default value: null</value>
        public string Referer { get; set; }
        /// <summary>
        /// Set Accept-Language header.
        /// </summary>
        /// <value>Default value: "en-US;q=0.7,en;q=0.3"</value>
        public string AcceptLanguage { get; set; } = "en-US;q=0.7,en;q=0.3";
        /// <summary>
        /// Set default Accept-Encoding header.
        /// </summary>
        /// <value>Default value: "gzip, deflate"</value>
        public string AcceptEncoding { get; set; } = "gzip, deflate";
        /// <summary>
        /// Set encoding for request.
        /// </summary>
        /// <value>Default value: UTF-8</value>
        public new Encoding Encoding
        {
            get { return base.Encoding; }
            set { base.Encoding = value; }
        }
        /// <summary>
        /// Enabled automatic redirect. Default: true
        /// </summary>
        public bool AllowAutoRedirect { get; set; } = true;


        /// <summary>
        /// DNS Resolved by Proxy when Socks 5 used.
        /// Mandatory for TOR proxy, recommanded for anonymity
        /// </summary>
        public bool DnsResolvedBySocksProxy { get; set; } = true;

        /// <summary>
        /// Validate HTTPS certificat on SocksProxy ?
        /// The SslStream used won't follow the  servicePointManager checkCertificateName in the .config, so can be set here.
        /// </summary>
        public bool ValidateServerCertificateSocksProxy { get; set; } = true;
        
        /// <summary>
        /// Stay on the same root domain ?
        /// Allow subdomain redirect (or protocol change), but disallow redirect to another base domain (XXXX.YYY)
        /// </summary>
        public bool RefuseOtherDomainRedirect { get; set; } = false;
        
        /// <summary>
        /// Headers collection that will be added to each request
        /// </summary>
        public NameValueCollection CustomHeaders { get; set; }
        public HttpClient(Proxy proxy) : this(proxy, Encoding.UTF8) { }

        public HttpClient() : this(new Proxy(), Encoding.UTF8) { }
        public HttpClient(Encoding encoding) : this(new Proxy(), encoding) { }
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


            request.Headers.Add("Cookie", Cookies.GetCookieHeader(address));
            request.Headers.Add("Accept-Language", AcceptLanguage);
            request.Headers.Add("Accept-Encoding", AcceptEncoding);
            if (CustomHeaders != null)
            {
                foreach (string key in CustomHeaders.AllKeys)
                {
                    request.Headers.Add(key, CustomHeaders[key]);
                }
            }


            if (Proxy.ProxyType != ProxyTypeEnum.Socks)
            {
                var httpRequest = (request as HttpWebRequest);
                httpRequest.UserAgent = UserAgent;
                httpRequest.Accept = Accept;
                httpRequest.Referer = Referer;
                httpRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                httpRequest.AllowAutoRedirect = AllowAutoRedirect;
                // TO IMPLEMANT IF REQUIRED httpRequest.RefuseOtherDomainRedirect = RefuseOtherDomainRedirect;
            }
            else if (Proxy.ProxyType == ProxyTypeEnum.Socks)
            {
                var socksRequest = (request as SocksHttpWebRequest);
                socksRequest.UserAgent = UserAgent;
                socksRequest.Accept = Accept;
                socksRequest.Referer = Referer;
                socksRequest.AllowAutoRedirect = AllowAutoRedirect;
                socksRequest.DnsResolvedBySocksProxy = DnsResolvedBySocksProxy;
                socksRequest.ValidateServerCertificateSocksProxy = ValidateServerCertificateSocksProxy;
                socksRequest.RefuseOtherDomainRedirect = RefuseOtherDomainRedirect;
            }

            request.Timeout = (int) Timeout.TotalMilliseconds;
            request.Proxy = Proxy.ProxyItem;

            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            try
            {
                string setCookies = response.Headers["Set-Cookie"];
                Cookies.SetCookies(request.RequestUri, setCookies);
            }
            catch (Exception)
            {

            }

            return response;
        }
        /// <summary>
        /// Execute GET request.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string Get(string url)
        {
            return Encoding.GetString(DownloadBytes(url, null));
        }
        /// <summary>
        /// Execute POST request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public string Post(string url, NameValueCollection data)
        {
            return Encoding.GetString(DownloadBytes(url, data));
        }

        /// <summary>
        /// Execute GET request.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public byte[] DownloadBytes(string url)
        {
            return DownloadBytes(url, null);
        }
        /// <summary>
        /// Excecute POST request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] DownloadBytes(string url, NameValueCollection data)
        {
            int counter = 0;
            WebException lastWebException = null;
            bool unkownProxy = Proxy.ProxyType == ProxyTypeEnum.Unknown;

            while (counter < NumberOfAttempts + (NumberOfAttempts < 2 && unkownProxy ? 1 : 0)) // min two try for unkonwn proxy type
            {
                try
                {
                    if (unkownProxy && counter % 2 == 0) // every odd try is as http proxy
                        Proxy.ProxyType = ProxyTypeEnum.Http;
                    else if (unkownProxy)
                        Proxy.ProxyType = ProxyTypeEnum.Socks;

                    byte[] result = data == null ? Encoding.GetBytes(DownloadString(url)) : UploadValues(url, data);
                    return result;
                }
                catch (WebException e)
                {
                    lastWebException = e;
                    counter++;
                }
            }

            if (unkownProxy)
                Proxy.ProxyType = ProxyTypeEnum.Unknown;
            // ReSharper disable once PossibleNullReferenceException
            throw lastWebException;
        }
    }
}
