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
        private int _timeout = 60000;
        private int _numberOfAttempts = 4;
        private ProxyCheckService _proxyCheckService;

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0";
        public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        public string Referer { get; set; }
        public string AcceptLanguage { get; set; } = "en-US;q=0.7,en;q=0.3";
        public string AcceptEncoding { get; set; } = "gzip";

        /// <summary>
        /// Set proxy check service.
        /// Must implement IProxyCheckService interface.
        /// </summary>
        public ProxyCheckService ProxyCheckService
        {
            get { return _proxyCheckService; }
            set
            {
                if(value == null)
                    throw new ArgumentNullException();
                _proxyCheckService = value;
            }
        }
        /// <summary>
        /// String which returned page has to contain.
        /// It helps to check if returned page is the page which we watned to receive.
        /// Proxy sometimes are returing some other pages.
        /// </summary>
        public string RequiredString
        {
            get { return _requiredString; }
            set { _requiredString = value ?? string.Empty; }
        }
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// Set this to true if you want to use only anonymous proxies.
        /// Defalut false.
        /// </summary>
        public bool AnonymousProxyOnly { get; } = false;
        public int Timeout
        {
            get
            {
                return _timeout;
            }
            set
            {
                if (value < 5)
                    throw new ArgumentOutOfRangeException("Timeout has to be greater or equal than 5!");
                _timeout = value;
            }
        }
        /// <summary>
        /// Set how many attempts can be made to execute request on one proxy.
        /// Default value is default value of HttpClient class.
        /// </summary>
        public int NumberOfAttempts
        {
            get { return _numberOfAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Value has to be greater than one.");

                _numberOfAttempts = value;
                _proxyCheckService.NumberOfAttempts = _numberOfAttempts;
            }
        }
        /// <summary>
        /// If true cookies are preserved between requests.
        /// </summary>
        public bool PreserveCookies { get; set; } = false;

        public ProxyManager(IEnumerable<string> proxies, bool anonymousOnly, ProxyCheckService proxyCheckService)
        {
            if(proxies == null || proxyCheckService == null)
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
            _proxyCheckService = proxyCheckService;
            _numberOfAttemptsPerRequest = _proxies.Count + 1;
            _proxyCheckService.NumberOfAttempts = _numberOfAttempts;
        }

        public ProxyManager(string file) : this(File.ReadLines(file), false, new ProxyJudgeProxyCheckService()) { }
        public ProxyManager(string file, bool anonymousOnly) : this(File.ReadLines(file), anonymousOnly, new ProxyJudgeProxyCheckService()) { }
        public ProxyManager(string file, bool anonymousOnly, ProxyCheckService service) : this(File.ReadLines(file), anonymousOnly, service) { }

        public string GetPage(string url)
        {
            return PostPage(url, null);
        }

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
                        if (!proxy.IsChecked)
                        {
                            CheckProxy(proxy);
                            if(!proxy.IsOnline)
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

        public byte[] DownloadBytes(string url, NameValueCollection data)
        {
            byte[] result = null;
            int limit = 0;

            do
            {
                Proxy proxy = GetAvalibleProxy();

                try
                {
                    if (AnonymousProxyOnly)
                    {
                        if (!proxy.IsChecked)
                        {
                            CheckProxy(proxy);
                            if (!proxy.IsOnline)
                                continue;
                        }
                    }

                    result = DownloadBytes(url, data, proxy);

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

        public byte[] DownloadBytes(string url)
        {
            return DownloadBytes(url, null);
        }
        /// <summary>
        /// 
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
        private void CheckProxy(Proxy proxy)
        {
            bool isAnonymous = _proxyCheckService.IsProxyAnonymous(proxy);
            proxy.IsAnonymous = isAnonymous;
            proxy.IsChecked = true;
            proxy.IsOnline = isAnonymous;
        }
    }

    public class AllProxiesBannedException : Exception
    {
    }
}
