using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using BetterHttpClient.CheckService;

namespace BetterHttpClient
{
    public class ProxyManager
    {
        private List<Proxy> _proxies = new List<Proxy>();
        private string _requiredString = string.Empty;
        private CookieContainer _cookies = new CookieContainer();
        private int _numberOfAttemptsPerRequest;
        private TimeSpan _timeout = TimeSpan.FromSeconds(10);
        private int _numberOfAttempts = 4;
        private ProxyJudgeService _proxyJudgeService;

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
        /// Set proxy check service.
        /// Must derive from ProxyCheckService class.
        /// </summary>
        public ProxyJudgeService ProxyJudgeService
        {
            get { return _proxyJudgeService; }
            set
            {
                if(value == null)
                    throw new ArgumentNullException();
                _proxyJudgeService = value;
            }
        }
        /// <summary>
        /// Downloaded page has to contain this tring.
        /// It helps to check if returned page is the page which we watned to receive.
        /// Proxy sometimes are returing some other pages.
        /// Default value string.Empty
        /// </summary>
        public string RequiredString
        {
            get { return _requiredString; }
            set { _requiredString = value ?? string.Empty; }
        }
        /// <summary>
        /// Set encoding for request.
        /// </summary>
        /// <value>Default value: UTF-8</value>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// Set this to true if you want to use only anonymous proxies.
        /// </summary>
        /// <value>Default value: false</value>
        public bool AnonymousProxyOnly { get; } = false;
        /// <summary>
        /// Timeout for request.
        /// </summary>
        /// <value>Has to be greater than 5 milliseconds.<para />
        /// Default value: 10 seconds.</value>
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
        /// Set how many attempts can be made to execute request on one proxy.
        /// Default value is default value is equal 4
        /// </summary>
        public int NumberOfAttempts
        {
            get { return _numberOfAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Value has to be greater than one.");

                _numberOfAttempts = value;
                _proxyJudgeService.NumberOfAttempts = _numberOfAttempts;
            }
        }
        /// <summary>
        /// If true cookies are preserved between diffrent requests.
        /// </summary>
        public bool PreserveCookies { get; set; } = false;

        public ProxyManager(IEnumerable<string> proxies, bool anonymousOnly, ProxyJudgeService proxyJudgeService)
        {
            if(proxies == null || proxyJudgeService == null)
                throw new ArgumentNullException();

            foreach (string proxy in proxies)
            {
                try
                {
                    _proxies.Add(new Proxy(proxy));
                }
                catch (UriFormatException)
                {
                    // parsing exception
                }
            }

            AnonymousProxyOnly = anonymousOnly;
            _proxyJudgeService = proxyJudgeService;
            _numberOfAttemptsPerRequest = _proxies.Count + 1;
            _proxyJudgeService.NumberOfAttempts = _numberOfAttempts;
        }
        public ProxyManager(string file) : this(File.ReadLines(file), false, new ProxyJudgeJudgeService()) { }
        public ProxyManager(string file, bool anonymousOnly) : this(File.ReadLines(file), anonymousOnly, new ProxyJudgeJudgeService()) { }
        public ProxyManager(string file, bool anonymousOnly, ProxyJudgeService service) : this(File.ReadLines(file), anonymousOnly, service) { }

        /// <summary>
        /// Downloads url using GET.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string GetPage(string url)
        {
            return PostPage(url, null);
        }
       /// <summary>
       /// Downloads url using POST.
       /// </summary>
       /// <param name="url"></param>
       /// <param name="data"></param>
       /// <returns></returns>
        public string PostPage(string url, NameValueCollection data)
        {
            string page = null;
            int limit = 0;

            do
            {
                Proxy proxy = GetAvalibleProxy();
                
                try
                {
                    if (AnonymousProxyOnly)
                    {
                        if (AnonymousProxyOnly && !proxy.IsAnonymous(ProxyJudgeService))
                        {
                            continue;
                        }
                    }

                    var bytes = DownloadBytes(url, data, proxy);

                    if (bytes != null)
                    {
                        page = Encoding.GetString(bytes);
                        if (!page.Contains(RequiredString))
                        {
                            proxy.IsOnline = false;
                        }
                        else
                        {
                            return page;
                        }
                    }
                }
                finally
                {
                    limit++;
                    proxy.IsBusy = false;
                }

                limit++;
            } while (limit < _numberOfAttemptsPerRequest);

            throw new AllProxiesBannedException();
        }
        /// <summary>
        /// Downloads url using POST.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] DownloadBytes(string url, NameValueCollection data)
        {
            int limit = 0;

            do
            {
                Proxy proxy = GetAvalibleProxy();

                try
                {
                    if (AnonymousProxyOnly && !proxy.IsAnonymous(ProxyJudgeService))
                    {
                        continue;
                    }

                    byte[] result = DownloadBytes(url, data, proxy);

                    if (result != null)
                    {
                        return result;
                    }
                }
                finally
                {
                    limit++;
                    proxy.IsBusy = false;
                }

                limit++;
            } while (limit < _numberOfAttemptsPerRequest);

            throw new AllProxiesBannedException();
        }

        /// <summary>
        /// Downloads url using GET.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public byte[] DownloadBytes(string url)
        {
            return DownloadBytes(url, null);
        }
        /// <summary>
        /// Returns first free (but busy) and working proxy.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AllProxiesBannedException">All proxies are banned. You can't make request.</exception>
        public Proxy GetAvalibleProxy()
        {
            lock (_proxies)
            {
                Proxy selectedProxy = null;

                do
                {
                    if (_proxies.Count(t => t.IsOnline) == 0)
                        throw new AllProxiesBannedException();

                    selectedProxy = _proxies.FirstOrDefault(t => t.IsOnline && !t.IsBusy);
                    if(selectedProxy == null)
                        Thread.Sleep(1);

                } while (selectedProxy == null);

                selectedProxy.IsBusy = true;
                return selectedProxy;
            }
        }
        /// <summary>
        /// Sets all proxies IsOnline property to true.
        /// </summary>
        public void SetAllProxyAsOnline()
        {
            lock (_proxies)
            {
                _proxies.ForEach(t => t.IsOnline = true);
            }
        }


        private byte[] DownloadBytes(string url, NameValueCollection data, Proxy proxy)
        {
            HttpClient client = CreateHttpClient();
            client.Proxy = proxy;

            try
            {
                return client.DownloadBytes(url, data);
            }
            catch (WebException)
            {
                proxy.IsOnline = false;
            }

            return null;
        }
        private HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient(Encoding)
            {
                Accept = Accept,
                AcceptEncoding = AcceptEncoding,
                AcceptLanguage = AcceptLanguage,
                NumberOfAttempts = NumberOfAttempts,
                UserAgent = UserAgent,
                Referer = Referer,
                Timeout = Timeout
            };

            if (PreserveCookies)
                client.Cookies = _cookies;
            return client;
        }
    }

    public class AllProxiesBannedException : Exception
    {
    }
}
