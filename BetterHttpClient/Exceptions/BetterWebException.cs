using System;
using System.Net;
using System.IO;

namespace BetterHttpClient
{
    public class BetterWebException : Exception
    {
        public WebException WebException { get; }
        public string Content { get; }
        public int? RequestStatusCode { get; }

        public BetterWebException(WebException ex) : base(ex.Message)
        {
            WebException = ex ?? throw new ArgumentNullException();
            Content = GetContent();
            RequestStatusCode = GetStatusCode();
        }

        private string GetContent()
        {
            string content = "";
            WebResponse response = WebException.Response;
            HttpWebResponse httpResponse = response as HttpWebResponse;

            if (httpResponse == null)
                return null;

            using (var streamReader = new StreamReader(response.GetResponseStream()))
                content = streamReader.ReadToEnd();

            return content;
        }

        private int? GetStatusCode()
        {
            int? statusCode = null;

            if (WebException.Response is HttpWebResponse response)
            {
                statusCode = (int)response.StatusCode;
            }

            return statusCode;

        }

    }
}
