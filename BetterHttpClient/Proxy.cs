using System.Net;
using BetterHttpClient.CheckService;

namespace BetterHttpClient
{
    public class Proxy
    {
        private volatile bool _isBusy = false;
        private bool _isAnonymous;
        private bool _isChecked = false;

        /// <summary>
        /// Check if proxy is busy.
        /// </summary>
        public bool IsBusy
        {
            get { return _isBusy; }
            internal set { _isBusy = value; }
        }


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

        /// <summary>
        /// Returns true if proxy can hide your ip address
        /// </summary>
        public bool IsAnonymous(ProxyJudgeService service)
        {
            if (_isChecked)
                return _isAnonymous;

            _isAnonymous = service.IsProxyAnonymous(this);
            _isChecked = true;
            IsOnline = _isAnonymous;
            return _isAnonymous;
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