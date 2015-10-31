using System;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterHttpClient.CheckService
{
    public class UrihProxyCheckService : CheckService.ProxyCheckService
    {
        public override bool IsProxyAnonymous(Proxy proxy)
        {
            HttpClient client = new HttpClient(proxy, Encoding.UTF8);
            string page = client.Get("http://request.urih.com/");
            return !page.Contains(MyIp);
        }

        protected override string GetMyIp()
        {
            HttpClient client = new HttpClient(Encoding.UTF8);
            string page = client.Get("http://request.urih.com/");

            Match match = Regex.Match(page, "Whois\\?</a></h5><div>IP: (.*?) <a title=\"My IP details");
            if (!match.Success)
                throw new GetMyIpException();
            else
                return match.Groups[1].Value;
        }
    }

    public class GetMyIpException : Exception
    {
    }
}