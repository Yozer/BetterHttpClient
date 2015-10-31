using System;

namespace BetterHttpClient.CheckService
{
    public abstract class ProxyJudgeServiceAbstract
    {
        private string _myIp = null;
        private readonly object _syncLock = new object();

        public string MyIp
        {
            get
            {
                lock (_syncLock)
                {
                    if(_myIp == null)
                        _myIp = GetMyIp();
                }
                
                return _myIp;
            }
        }
        /// <summary>
        /// Set number of attempts which can be made to verify proxy.
        /// </summary>
        public int NumberOfAttempts { get; set; }
        /// <summary>
        /// Abstract method. Should return true if proxy is hiding your real ip address.
        /// </summary>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public abstract bool IsProxyAnonymous(Proxy proxy);
        /// <summary>
        /// Abstract method. Should return real ip addres of user (so without any proxy).
        /// </summary>
        /// <returns></returns>
        protected abstract string GetMyIp();
    }
    public class GetMyIpException : Exception
    {
    }
}