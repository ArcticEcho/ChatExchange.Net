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
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;

namespace ChatExchangeDotNet
{
    internal static class RequestManager
    {
        private static readonly Dictionary<string, CookieContainer> cookies = new Dictionary<string, CookieContainer>();
        private static int timeout = 100000;

        public static Dictionary<string, CookieContainer> Cookies
        {
            get
            {
                return cookies;
            }
        }

        public static int TimeoutMilliseconds
        {
            get
            {
                return timeout;
            }
            set
            {
                timeout = value;
            }
        }



        public static string GetResponseContent(HttpWebResponse response)
        {
            if (response == null) { throw new ArgumentNullException("response"); }

            var data = "";

            try
            {
                using (var strm = response.GetResponseStream())
                using (var reader = new StreamReader(strm))
                {
                    data = reader.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return null;
            }

            return data;
        }

        public static HttpWebResponse SendPOSTRequestRaw(string cookieKey, string uri, string content, string referer = "", string origin = "")
        {
            var req = GenerateRequest(cookieKey, uri, content, "POST", referer, origin);
            return GetResponse(req);
        }

        public static HttpWebResponse SendGETRequestRaw(string cookieKey, string uri)
        {
            var req = GenerateRequest(cookieKey, uri, null, "GET");
            return GetResponse(req);
        }

        public static string SendPOSTRequest(string cookieKey, string uri, string content, string referer = "", string origin = "")
        {
            try
            {
                var req = GenerateRequest(cookieKey, uri, content, "POST", referer, origin);
                using (var res = GetResponse(req))
                {
                    return GetResponseContent(res);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string SendGETRequest(string cookieKey, string uri)
        {
            try
            {
                var req = GenerateRequest(cookieKey, uri, null, "GET");
                using (var res = GetResponse(req))
                {
                    return GetResponseContent(res);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }



        private static HttpWebRequest GenerateRequest(string cookieKey, string uri, string content, string method, string referer = "", string origin = "")
        {
            if (uri == null) { throw new ArgumentNullException("uri"); }

            var req = (HttpWebRequest)WebRequest.Create(uri);

            req.Method = method;
            req.CookieContainer = string.IsNullOrEmpty(cookieKey) ? null : Cookies[cookieKey];
            req.Timeout = TimeoutMilliseconds;
            req.Referer = referer;

            if (!string.IsNullOrEmpty(origin))
            {
                req.Headers.Add("Origin", origin);
            }

            if (method.ToUpperInvariant() == "POST")
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
            if (req == null) { throw new ArgumentNullException("req"); }

            HttpWebResponse res = null;

            try
            {
                res = (HttpWebResponse)req.GetResponse();

                if (!string.IsNullOrEmpty(cookieKey))
                {
                    Cookies[cookieKey].Add(res.Cookies);
                }
            }
            catch (WebException ex)
            {
                // Check if we've been throttled.
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Conflict)
                {
                    // Yep, we have.
                    res = (HttpWebResponse)ex.Response;
                }
                else
                {
                    throw ex;
                }
            }

            return res;
        }
    }
}