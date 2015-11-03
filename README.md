# BetterHttpClient

BetterHttpClient will help you making http and https requests with full cookie support.

It supports three types of proxy servers:
  - HTTP
  - HTTPS
  - Socks5

# Http Client
- Http GET request
```cs
HttpClient client = new HttpClient
{
    UserAgent = "My custom user-agent",
    Referer = "My referer",
    Encoding = Encoding.UTF8,
    AcceptEncoding = "gzip, deflate" // setup transfer compression algorithm
};
string page = client.Get("http://www.google.com");
```

- Http POST request
```cs
HttpClient client = new HttpClient(); // if no headers are specified defaults will be used
string page = client.Post("https://httpbin.org/post", new NameValueCollection
{
    {"custname", "Some post data"},
});
```

- Https GET request using proxy

HttpClient will detect proxy type (http/s, socks5) if you don't provide such info.
```cs
string proxyAddress = "218.200.66.196:8080";
HttpClient client = new HttpClient(new Proxy(proxyAddress))
{
    UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0"
};

string page = client.Get("https://httpbin.org/get");
```

# Proxy Manager
This class will help you managing large amount of proxy servers.

You can load proxy list from file and then execute GET or POST requests.

If some proxy will be dead ProxyManager will mark them as offline and won't use in future requests.

To perform http/s request it will use first Not Busy and Online proxy (and check for an anonymity level if anonymousOnly is true).

Set anonymousOnly to true if you want to force ProxyManager to use only proxies that really hide your ip. For more info look at ProxyJudgeService class.
Methods
- GetPage
- PostPage
- DownloadBytes

are thread safe.

Example usage:
```cs
ProxyManager proxyManager = new ProxyManager("proxy_list.txt", anonymousOnly: true)
{
    UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:41.0) Gecko/20100101 Firefox/41.0"
    PreserveCookies = false, // if true it will use the same cookies between distinct requests
    Timeout = TimeSpan.FromSeconds(10),
    NumberOfAttempts = 2 // max number of attempts that can be made per one request
};       

// ProxyManager will choose first working proxy and use it to perform GET request
string page = proxyManager.GetPage("http://example.com");
```
