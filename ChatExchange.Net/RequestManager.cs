/*
 * ChatExchange.Net. A .Net (4.0) API for interacting with Stack Exchange chat.
 * Copyright © 2015, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// The core class used by CE.Net to make essential network requests.
    /// </summary>
    public static class RequestManager
    {
        internal static Dictionary<string, CookieContainer> Cookies { get; private set; }

        /// <summary>
        /// The timeout to be used when making requests.
        /// </summary>
        public static TimeSpan Timeout { get; set; }



        static RequestManager()
        {
            Timeout = TimeSpan.FromSeconds(100);
            Cookies = new Dictionary<string, CookieContainer>();
        }



        internal static HttpWebResponse PostRaw(string cookieKey, string uri, string content, string referrer = null, string origin = null)
        {
            var req = GenerateRequest(cookieKey, uri, content, "POST", referrer, origin);

            return GetResponse(req);
        }

        internal static HttpWebResponse GetRaw(string cookieKey, string uri)
        {
            var req = GenerateRequest(cookieKey, uri, null, "GET");

            return GetResponse(req);
        }

        internal static string Post(string cookieKey, string uri, string content, string referrer = null, string origin = null)
        {
            var req = GenerateRequest(cookieKey, uri, content, "POST", referrer, origin);

            using (var res = GetResponse(req))
            {
                return res.GetContent();
            }
        }

        internal static string Get(string cookieKey, string uri)
        {
            var req = GenerateRequest(cookieKey, uri, null, "GET");

            using (var res = GetResponse(req))
            {
                return res.GetContent();
            }
        }



        private static HttpWebRequest GenerateRequest(string cookieKey, string uri, string content, string method, string referrer = null, string origin = null)
        {
            if (uri == null) throw new ArgumentNullException("uri");

            var wc = new WebClient();
            

            var req = (HttpWebRequest)WebRequest.Create(uri);
            var meth = method.Trim().ToUpperInvariant();

            req.Method = meth;
            req.CookieContainer = string.IsNullOrEmpty(cookieKey) ? null : Cookies[cookieKey];
            req.Timeout = (int)Timeout.TotalMilliseconds;
            req.Referer = referrer;

            if (!string.IsNullOrEmpty(origin))
            {
                req.Headers.Add("Origin", origin);
            }

            if (meth == "POST")
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

        private static HttpWebResponse GetResponse(HttpWebRequest req, string cookieKey = null)
        {
            if (req == null) throw new ArgumentNullException("req");

            HttpWebResponse res = null;

            try
            {
                res = (HttpWebResponse)req.GetResponse();

                if (!string.IsNullOrEmpty(cookieKey))
                {
                    Cookies[cookieKey].Add(res.Cookies);
                }
            }
            // Check if we've been throttled.
            catch (WebException ex) when (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Conflict)
            {
                // Yep, we have.
                res = (HttpWebResponse)ex.Response;
            }

            return res;
        }
    }
}