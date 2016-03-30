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
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using RestSharp;

namespace ChatExchangeDotNet
{
    internal static class RequestManager
    {
        public static Dictionary<string, List<RestResponseCookie>> Cookies { get; set; } = new Dictionary<string, List<RestResponseCookie>>();



        public static RestResponse SendRequest(RestRequest req)
        {
            return SendRequest(null, req);
        }

        public static RestResponse SendRequest(string cookieKey, RestRequest req)
        {
            var reqUri = new Uri(req.Resource);
            var baseReqUrl = reqUri.Scheme + "://" + reqUri.Host;

            // RestSharp doesn't currently honour cookie headers
            // upon redirect (which is crucial for authentication
            // in our case). So I've implemented my own (crude)
            // means of following redirects (302s) in the meantime.
            var c = new RestClient(baseReqUrl) { FollowRedirects = false };

            if (!string.IsNullOrWhiteSpace(cookieKey) && Cookies.ContainsKey(cookieKey))
            {
                foreach (var cookie in Cookies[cookieKey])
                {
                    if (cookie.Expired) continue;

                    req.AddCookie(cookie.Name, cookie.Value);
                }
            }

            req.Resource = req.Resource.Remove(0, baseReqUrl.Length);

            var res = (RestResponse)c.Execute(req);

            if (!string.IsNullOrWhiteSpace(cookieKey) && res.Cookies != null && res.Cookies.Count > 0)
            {
                if (!Cookies.ContainsKey(cookieKey))
                {
                    Cookies[cookieKey] = new List<RestResponseCookie>();
                }

                foreach (var cookie in res.Cookies)
                {
                    Cookies[cookieKey].Add(cookie);
                }
            }

            if (res.StatusCode == HttpStatusCode.Found || res.StatusCode == HttpStatusCode.Moved)
            {
                var url = Regex.Match(res.Content, "href=\"(\\S+)\">here<").Groups[1].Value;

                if (!url.StartsWith("http"))
                {
                    url = baseReqUrl + url;
                }

                return SendRequest(cookieKey, GenerateRequest(Method.GET, url));
            }

            return res;
        }

        public static RestRequest GenerateRequest(Method meth, string endpoint)
        {
            return new RestRequest(endpoint, meth);
        }

        public static RestRequest GenerateRequest(Method meth, string endpoint, string referrer)
        {
            var req = GenerateRequest(meth, endpoint);

            req.AddParameter("Referer", referrer, ParameterType.HttpHeader);

            return req;
        }

        public static RestRequest GenerateRequest(Method meth, string endpoint, string referrer, string origin)
        {
            var req = GenerateRequest(meth, endpoint, referrer);

            req.AddParameter("Origin", origin, ParameterType.HttpHeader);

            return req;
        }
    }
}