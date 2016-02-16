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

        /// <param name="proxies">Proxy list</param>
        /// <param name="anonymousOnly">Set true if you want to filter proxy list and use only anonymous only</param>
        /// <param name="proxyJudgeService">Proxy judge service is used to determine proxy anonymity level</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ProxyManager(IEnumerable<string> proxies, bool anonymousOnly, ProxyJudgeService proxyJudgeService) :
            this(ParseProxies(proxies), anonymousOnly, proxyJudgeService)
        {
        }

        /// <param name="proxies">Proxy list</param>
        /// <param name="anonymousOnly">Set true if you want to filter proxy list and use only anonymous only</param>
        /// <param name="proxyJudgeService">Proxy judge service is used to determine proxy anonymity level</param>
        /// <exception cref="ArgumentNullException"></exception>
        public ProxyManager(IEnumerable<Proxy> proxies, bool anonymousOnly, ProxyJudgeService proxyJudgeService)
        {
            if (proxies == null || proxyJudgeService == null)
                throw new ArgumentNullException();
            foreach (Proxy proxy in proxies)
            {
                try
                {
                    _proxies.Add(proxy);
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
        public ProxyManager(string file) : this(File.ReadLines(file), false, new ProxyJudgeService()) { }
        public ProxyManager(string file, bool anonymousOnly) : this(File.ReadLines(file), anonymousOnly, new ProxyJudgeService()) { }
        public ProxyManager(string file, bool anonymousOnly, ProxyJudgeService service) : this(File.ReadLines(file), anonymousOnly, service) { }

        /// <summary>
        /// Downloads url using GET.
        /// </summary>
        /// <param name="url">Url of webpage</param>
        /// <param name="cookies">Cookies for request. Left null if you don't want to use cookies</param>
        /// <param name="customHeaders">Specify custom headers for this request</param>
        /// <returns></returns>
        /// <exception cref="WebPageNotFoundException">Page has returned 404 not found</exception>
        public string GetPage(string url, string requiredString = null, CookieContainer cookies = null, NameValueCollection customHeaders = null)
        {
            return PostPage(url, null, requiredString, cookies, customHeaders);
        }

        /// <summary>
        /// Downloads url using POST.
        /// </summary>
        /// <param name="url">Url of webpage</param>
        /// <param name="data">Post values</param>
        /// <param name="cookies">Cookies for request. Left null if you don't want to use cookies</param>
        /// <param name="customHeaders">Specify custom headers for this request</param>
        /// <exception cref="WebPageNotFoundException">Page has returned 404 not found</exception>
        /// <returns></returns>
        public string PostPage(string url, NameValueCollection data, string requiredString = null, CookieContainer cookies = null, NameValueCollection customHeaders = null)
        {
            if (requiredString == null)
                requiredString = RequiredString;

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

                    var bytes = DownloadBytes(url, data, proxy, cookies, customHeaders);

                    if (bytes != null)
                    {
                        page = Encoding.GetString(bytes);
                        if (!page.Contains(requiredString))
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
        /// <param name="url">Url of webpage</param>
        /// <param name="data">Post values</param>
        /// <param name="cookies">Cookies for request. Left null if you don't want to use cookies</param>
        /// <param name="customHeaders">Specify custom headers for this request</param>
        /// <exception cref="WebPageNotFoundException">Page has returned 404 not found</exception>
        /// <returns></returns>
        public byte[] DownloadBytes(string url, NameValueCollection data, CookieContainer cookies = null, NameValueCollection customHeaders = null)
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

                    byte[] result = DownloadBytes(url, data, proxy, cookies, customHeaders);

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
        /// <param name="cookies"></param>
        /// <param name="customHeaders">Specify custom headers for this request</param>
        /// <returns></returns>
        /// <exception cref="WebPageNotFoundException">Page has returned 404 not found</exception>
        public byte[] DownloadBytes(string url, CookieContainer cookies = null, NameValueCollection customHeaders = null)
        {
            return DownloadBytes(url, null, cookies, customHeaders);
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
                    if (selectedProxy == null)
                    {
                        Thread.Sleep(35);
                    }

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

        /// <summary>
        /// Return all proxies
        /// </summary>
        /// <returns></returns>
        public List<Proxy> GetAllProxies()
        {
            lock(_proxies)
                return CloneProxyList(_proxies);
        }


        private List<Proxy> CloneProxyList(List<Proxy> proxyInput)
        {
            List<Proxy> proxies = new List<Proxy>(proxyInput.Count);
            proxies.AddRange(proxyInput.Select(proxy => (Proxy) proxy.Clone()));
            return proxies;
        } 

        private byte[] DownloadBytes(string url, NameValueCollection data, Proxy proxy, CookieContainer cookies = null, NameValueCollection customHeaders = null)
        {
            HttpClient client = CreateHttpClient(customHeaders);
            client.Proxy = proxy;
            if (cookies != null) client.Cookies = cookies;

            try
            {
                return client.DownloadBytes(url, data);
            }
            catch (WebException e)
            {
                if (e.Response != null && (e.Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                    throw new WebPageNotFoundException();
                proxy.IsOnline = false;
            }

            return null;
        }
        private HttpClient CreateHttpClient(NameValueCollection customHeaders = null)
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

            if (customHeaders != null)
            {
                if (customHeaders.AllKeys.Contains("Accept"))
                {
                    client.Accept = customHeaders["Accept"];
                    customHeaders.Remove("Accept");
                }
                if (customHeaders.AllKeys.Contains("Accept-Encoding"))
                {
                    client.AcceptEncoding = customHeaders["Accept-Encoding"];
                    customHeaders.Remove("Accept-Encoding");
                }
                if (customHeaders.AllKeys.Contains("Accept-Language"))
                {
                    client.AcceptLanguage = customHeaders["Accept-Language"];
                    customHeaders.Remove("Accept-Language");
                }
                if (customHeaders.AllKeys.Contains("User-Agent"))
                {
                    client.UserAgent = customHeaders["User-Agente"];
                    customHeaders.Remove("User-Agent");
                }
                if (customHeaders.AllKeys.Contains("Referer"))
                {
                    client.Referer = customHeaders["Referer"];
                    customHeaders.Remove("Referer");
                }

                client.CustomHeaders = customHeaders;
            }

            return client;
        }
        private static IEnumerable<Proxy> ParseProxies(IEnumerable<string> proxies)
        {
            IEnumerable<Proxy> proxyParsed = proxies.Select(t =>
            {
                try
                {
                    Proxy proxy = new Proxy(t);
                    return proxy;
                }
                catch (UriFormatException)
                {
                    // parsing exception
                    return null;
                }
            }).Where(t => t != null);
            return proxyParsed;
        }
    }

    public class AllProxiesBannedException : Exception
    {
    }
    public class WebPageNotFoundException : Exception
    {
    }
}
