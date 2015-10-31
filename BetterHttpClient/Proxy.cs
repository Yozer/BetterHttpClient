using System.Net;

namespace BetterHttpClient
{
    public class Proxy
    {
        private volatile bool _isBusy = false;

        public bool IsBusy
        {
            get { return _isBusy; }
            internal set { _isBusy = value; }
        }

        internal bool IsChecked { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsOnline { get; set; } = true;
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