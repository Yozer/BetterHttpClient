using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace BetterHttpClient
{
    public class ProxyManager
    {
        private List<Proxy> _proxies = new List<Proxy>();
        private string _requiredString = string.Empty;
        private CookieContainer _cookies = new CookieContainer();

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0";
        public string Accept { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        public string Referer { get; set; }
        public string AcceptLanguage { get; set; } = "en-US;q=0.7,en;q=0.3";
        public string AcceptEncoding { get; set; } = "gzip";
        /// <summary>
        /// String that returned page has to contain.
        /// It helps to check if returned page is the page which we watned to recieve.
        /// Proxy sometimes are returing some other pages.
        /// </summary>
        /// 
        public string RequiredString
        {
            get { return _requiredString; }
            set { _requiredString = value ?? string.Empty; }
        }
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        private int _numberOfAttemptsPerRequest;
        private int _timeout = 60000;
        private int _numberOfAttemptsPerProxy = 4;

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
        /// Set how many attempts can be made to execute request.
        /// Default value is equal to total proxy count.
        /// </summary>
        public int NumberOfAttemptsPerRequest
        {
            get { return _numberOfAttemptsPerRequest; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Value has to be greater than one.");

                _numberOfAttemptsPerRequest = value;
            }
        }
        /// <summary>
        /// Set how many attempts can be made to execute request on one proxy.
        /// Default value is default value of HttpClient class.
        /// </summary>
        public int NumberOfAttemptsPerProxy
        {
            get { return _numberOfAttemptsPerProxy; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("Value has to be greater than one.");

                _numberOfAttemptsPerProxy = value;
            }
        }
        /// <summary>
        /// If true cookies are preserved between requests.
        /// </summary>
        public bool PreserveCookies { get; set; } = false;

        public ProxyManager(IEnumerable<string> proxies)
        {
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
        }

        public ProxyManager(string file) : this(File.ReadLines(file)) { }

        public string GetPage(string url)
        {
            string page = null;
            while ((page = Encoding.GetString(DownloadBytes(url))).Contains(RequiredString)) ;
            return page;
        }

        public string PostPage(string url, NameValueCollection data)
        {
            string page = null;
            while((page = Encoding.GetString(DownloadBytes(url, data))).Contains(RequiredString));
            return page;
        }

        public byte[] DownloadBytes(string url, NameValueCollection data)
        {
            int limit = 0;

            do
            {
                HttpClient client = CreateHttpClient();
                Proxy proxy = GetAvalibleProxy();
                client.Proxy = proxy;

                try
                {
                    return client.DownloadBytes(url, data);
                }
                catch (WebException)
                {
                    limit++;
                }
                finally
                {
                    proxy.IsBusy = false;
                }

            } while (limit < NumberOfAttemptsPerRequest);

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
                    if (_proxies.Count(t => t.IsBusy) == 0)
                        throw new AllProxiesBannedException();

                    selectedProxy = _proxies.SingleOrDefault(t => t.IsBusy && !t.IsBusy);
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
                NumberOfAttempts = NumberOfAttemptsPerProxy,
                UserAgent = UserAgent,
                Referer = Referer
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
