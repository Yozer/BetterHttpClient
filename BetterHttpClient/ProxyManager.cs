using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BetterHttpClient
{
    public class ProxyManager
    {
        private List<Proxy> _proxies = new List<Proxy>();

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
                    if (_proxies.Count(t => t.Working) == 0)
                        throw new AllProxiesBannedException();

                    selectedProxy = _proxies.SingleOrDefault(t => t.Working && !t.IsBusy);
                    if(selectedProxy == null)
                        Thread.Sleep(1);

                } while (selectedProxy == null);

                return selectedProxy;
            }
        }
    }

    public class AllProxiesBannedException : Exception
    {
    }
}
