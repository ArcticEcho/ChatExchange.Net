﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;



namespace ChatExchangeDotNet
{
    public static class RequestManager
    {
		private static int responseTryCount;

	    public static readonly CookieContainer GlobalCookies = new CookieContainer();
		public static CookieContainer CookiesToPass = new CookieContainer();



		public static string GetResponseContent(HttpWebResponse response)
        {
            if (response == null) { throw new ArgumentNullException("response"); }

            Stream dataStream = null;
            StreamReader reader = null;
            string responseFromServer;

            try
            {
                dataStream = response.GetResponseStream();

                reader = new StreamReader(dataStream);

                responseFromServer = reader.ReadToEnd();
            }
            finally
            {                
                if (reader != null)
                {
	                reader.Close();
                }

                if (dataStream != null)
                {
                    dataStream.Close();
                }

                response.Close();
            }

            return responseFromServer;
        }

		public static HttpWebResponse SendPOSTRequest(string uri, string content, bool allowAutoRedirect = true, string referer = "", string origin = "")
        {
			return GetResponse(GenerateRequest(uri, content, "POST", allowAutoRedirect, referer, origin));
        }

		public static HttpWebResponse SendGETRequest(string uri, bool allowAutoRedirect = true)
        {
			return GetResponse(GenerateRequest(uri, null, "GET", allowAutoRedirect));
        }



		private static HttpWebRequest GenerateRequest(string uri, string content, string method, bool allowAutoRedirect = true, string referer = "", string origin = "")
        {
            if (uri == null) { throw new ArgumentNullException("uri"); }

			var req = (HttpWebRequest)WebRequest.Create(uri);

			req.Method = method;
	        req.AllowAutoRedirect = allowAutoRedirect;
			req.Credentials = CredentialCache.DefaultNetworkCredentials;
			req.CookieContainer = CookiesToPass;

			// Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test

			req.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/38.0.2125.111 Safari/537.36";
			req.Accept = "*/*";
			req.Headers.Add("X-Requested-With", "XMLHttpRequest");
			req.Headers.Add("DNT", "1");

			// Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test ~ Test

			if (!String.IsNullOrEmpty(referer))
			{
				req.Referer = referer;
			}

			if (!String.IsNullOrEmpty(origin))
			{
				req.Headers.Add("Origin", origin);
			}

            if (method == "POST")
            {
                var data = Encoding.UTF8.GetBytes(content);

                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = data.Length;

	            using (var dataStream = req.GetRequestStream())
	            {
		            dataStream.Write(data, 0, data.Length);
	            }				
			}

			return req;
		}

		private static HttpWebResponse GetResponse(HttpWebRequest request)
		{
			if (request == null) { throw new ArgumentNullException("request"); }

			HttpWebResponse res = null;

			try
			{
				res = (HttpWebResponse)request.GetResponse();

				GlobalCookies.Add(res.Cookies);
			}
			catch (WebException)
			{
				if (responseTryCount == 5) { return res; }

				responseTryCount++;

				Thread.Sleep(responseTryCount * 1000);

				GetResponse(request);
			}

			responseTryCount = 0;

			return res;
		}
	}
}