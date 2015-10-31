using System.Threading;

namespace BetterHttpClient.CheckService
{
    public abstract class ProxyCheckService
    {
        private string _myIp = null;
        private readonly object syncLock = new object();

        public string MyIp
        {
            get
            {
                lock (syncLock)
                {
                    if(_myIp == null)
                        _myIp = GetMyIp();
                }
                
                return _myIp;
            }
        }

        public int NumberOfAttempts { get; set; }
        public abstract bool IsProxyAnonymous(Proxy proxy);
        protected abstract string GetMyIp();
    }
}