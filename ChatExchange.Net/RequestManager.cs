﻿/*
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
        public static Dictionary<string, CookieContainer> Cookies = new Dictionary<string, CookieContainer>();

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

        public static HttpWebResponse SendPOSTRequest(string cookieKey, string uri, string content, bool allowAutoRedirect = true, string referer = "", string origin = "")
        {
            return GetResponse(GenerateRequest(cookieKey, uri, content, "POST", allowAutoRedirect, referer, origin));
        }

        public static HttpWebResponse SendGETRequest(string cookieKey, string uri, bool allowAutoRedirect = true, string referer = "")
        {
            return GetResponse(GenerateRequest(cookieKey, uri, null, "GET", allowAutoRedirect, referer));
        }



        private static HttpWebRequest GenerateRequest(string cookieKey, string uri, string content, string method, bool allowAutoRedirect = true, string referer = "", string origin = "")
        {
            if (uri == null) { throw new ArgumentNullException("uri"); }

            var req = (HttpWebRequest)WebRequest.Create(uri);

            req.Method = method;
            req.AllowAutoRedirect = allowAutoRedirect;
            req.Credentials = CredentialCache.DefaultNetworkCredentials;
            req.CookieContainer = String.IsNullOrEmpty(cookieKey) ? null : Cookies[cookieKey];
            req.Timeout = 300000; // 5 mins.

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

        private static HttpWebResponse GetResponse(HttpWebRequest req, string cookieKey = null)
        {
            if (req == null) { throw new ArgumentNullException("req"); }

            HttpWebResponse res = null;

            try
            {
                res = (HttpWebResponse)req.GetResponse();

                if (!String.IsNullOrEmpty(cookieKey))
                {
                    Cookies[cookieKey].Add(res.Cookies);
                }
            }
            catch (WebException ex)
            {
                // Check if we've been throttled.
                if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Conflict)
                {
                    // Yep, we have.
                    res = (HttpWebResponse)ex.Response;
                }
            }

            return res;
        }
    }
}