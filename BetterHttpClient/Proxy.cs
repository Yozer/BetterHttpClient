using System.Net;

namespace BetterHttpClient
{
    public class Proxy
    {
        public bool Working { get; internal set; }
        public ProxyTypeEnum ProxyType { get; internal set; }
        internal WebProxy ProxyItem { get; }

        public Proxy()
        {
            ProxyItem = new WebProxy();
            ProxyType = ProxyTypeEnum.None;
        }

        public Proxy(string address)
        {
            ProxyItem = new WebProxy(address);
        }

        public Proxy(string ip, int port)
        {
            ProxyItem = new WebProxy(ip, port);
        }
    }

    public enum ProxyTypeEnum
    {
        Unknown,
        None,
        Http,
        Socks
    }
}