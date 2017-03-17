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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace ChatExchangeDotNet
{
    internal enum HttpMethod
    {
        GET,
        POST
    }

    internal class HttpRes
    {
        public string Endpoint { get; private set; }

        public string Data { get; private set; }

        public HttpStatusCode StatusCode { get; private set; }

        public HttpRes(string endpoint, string data, HttpStatusCode statusCode)
        {
            Endpoint = endpoint;
            Data = data;
            StatusCode = statusCode;
        }
    }

    internal class HttpReq
    {
        private string data;
        private bool isKVs;

        public HttpMethod Method { get; set; }

        public string Endpoint { get; set; }

        public string Referrer  { get; set; }

        public string Origin { get; set; }

        public string CookieKey { get; set; }

        public Func<Cookie, bool> CookieFilter { get; set; }

        public string Data
        {
            get
            {
                if (data?.EndsWith("&") ?? false && isKVs)
                {
                    return data.Substring(0, data.Length - 1);
                }

                return data;
            }

            set
            {
                data = value;
            }
        }

        public bool EscapeData { get; set; } = true;

        public void AddDataKVPair(string key, string value)
        {
            isKVs = true;
            data += key + "=";

            if (EscapeData)
            {
                data += Uri.EscapeDataString(value);
            }
            else
            {
                data += value;
            }

            data += "&";
        }
    }

    internal static class RequestManager
    {
        private static Dictionary<string, List<Cookie>> cookies = new Dictionary<string, List<Cookie>>();



        public static bool HasCookieKey(string key)
        {
            return key != null && cookies.ContainsKey(key);
        }

        public static void AddCookieKey(string key)
        {
            if (!HasCookieKey(key))
            {
                cookies[key] = new List<Cookie>();
            }
        }

        public static void RemoveCookieKey(string key)
        {
            if (HasCookieKey(key))
            {
                cookies.Remove(key);
            }
        }

        public static void RemoveCookies(string key, string name)
        {
            if (HasCookieKey(key))
            {
                cookies[key] = cookies[key].Where(x => x.Name != name).ToList();
            }
        }

        public static void AddCookie(string key, Cookie cookie)
        {
            if (HasCookieKey(key))
            {
                cookies[key].Add(cookie);
            }
        }

        public static string SimpleGet(string url, string cookieKey = null)
        {
            return SendRequest(new HttpReq
            {
                Endpoint = url,
                Method = HttpMethod.GET,
                CookieKey = cookieKey
            }).Data;
        }

        public static HttpRes SendRequest(HttpReq reqInfo)
        {
            var reqCookies = new CookieContainer();

            if (HasCookieKey(reqInfo.CookieKey))
            {
                lock (cookies)
                foreach (var cookie in cookies[reqInfo.CookieKey])
                {
                    if (reqInfo.CookieFilter?.Invoke(cookie) ?? true)
                    {
                        var domain = cookie.Domain.StartsWith(".") 
                            ? cookie.Domain.Remove(0, 1) 
                            : cookie.Domain;
                        reqCookies.Add(new Uri("https://" + domain), cookie);
                    }
                }
            }

            var req = WebRequest.CreateHttp(reqInfo.Endpoint);
            req.Method = reqInfo.Method.ToString();
            req.CookieContainer = reqCookies;

            req.Headers["Accept"] = "application/json, application/xml, text/json, text/x-json, text/javascript, text/xml";

            if (!string.IsNullOrEmpty(reqInfo.Referrer))
            {
                req.Headers["Referer"] = reqInfo.Referrer;
            }

            if (!string.IsNullOrEmpty(reqInfo.Origin))
            {
                req.Headers["Origin"] = reqInfo.Origin;
            }

            if (!string.IsNullOrEmpty(reqInfo.Data))
            {
                req.Headers["Content-Type"] = "application/x-www-form-urlencoded";
                using (var strm = req.GetRequestStreamAsync().Result)
                {
                    var dataBytes = Encoding.UTF8.GetBytes(reqInfo.Data);
                    strm.Write(dataBytes, 0, dataBytes.Length);
                }
            }

            HttpWebResponse res;
            string resData;

            try
            {
                res = (HttpWebResponse)req.GetResponseAsync().Result;
            }
            catch (AggregateException ex)
            when (ex.InnerException != null && ex.InnerException is WebException && ((WebException)ex.InnerException).Response != null)
            {
                res = (HttpWebResponse)((WebException)ex.InnerException).Response;
            }

            if (HasCookieKey(reqInfo.CookieKey))
            {
                lock (cookies)
                foreach (Cookie c in res.Cookies)
                {
                    cookies[reqInfo.CookieKey].Add(c);
                }
            }

            using (var resStrm = res.GetResponseStream())
            using (var reader = new StreamReader(resStrm))
            {
                resData =  reader.ReadToEnd();
            }

            return new HttpRes(res.ResponseUri.ToString(), resData, res.StatusCode);
        }

        //public static RestResponse SendRequest(RestRequest req)
        //{
        //    return SendRequest(null, req);
        //}

        //public static RestResponse SendRequest(string cookieKey, RestRequest req, Func<RestResponseCookie, bool> cookieFilter = null, Func<RestResponseCookie, bool> cookieFilterRedirects = null)
        //{
        //    var reqUri = new Uri(req.Resource);
        //    var baseReqUrl = reqUri.Scheme + "://" + reqUri.Host;

        //    // RestSharp doesn't currently honour cookie headers
        //    // upon redirect (which is crucial for authentication
        //    // in our case). So I've implemented my own (crude)
        //    // means of following redirects (302s) in the meantime.
        //    var c = new RestClient(baseReqUrl)
        //    {
        //        FollowRedirects = false
        //    };

        //    if (!string.IsNullOrWhiteSpace(cookieKey) && cookies.ContainsKey(cookieKey))
        //    {
        //        foreach (var cookie in cookies[cookieKey])
        //        {
        //            if (cookie.Expired) continue;
        //            if (!(cookieFilter?.Invoke(cookie) ?? true)) continue;

        //            req.AddCookie(cookie.Name, cookie.Value);
        //        }
        //    }

        //    req.Resource = req.Resource.Remove(0, baseReqUrl.Length);

        //    var are = new AutoResetEvent(false);
        //    RestResponse res = null;
        //    c.ExecuteAsync(req, response =>
        //    {
        //        res = (RestResponse)response;
        //        are.Set();
        //    });
        //    are.WaitOne(TimeSpan.FromSeconds(100));

        //    if (res == null) return null;

        //    if (!string.IsNullOrWhiteSpace(cookieKey) && res.Cookies != null && res.Cookies.Count > 0)
        //    {
        //        if (!cookies.ContainsKey(cookieKey))
        //        {
        //            cookies[cookieKey] = new List<RestResponseCookie>();
        //        }

        //        foreach (var cookie in res.Cookies)
        //        {
        //            cookies[cookieKey].Add(cookie);
        //        }
        //    }

        //    if (res.StatusCode == HttpStatusCode.Found || res.StatusCode == HttpStatusCode.Moved)
        //    {
        //        var url = Regex.Match(res.Content, "href=\"(\\S+)\">here<").Groups[1].Value;

        //        if (!url.StartsWith("http"))
        //        {
        //            url = baseReqUrl + url;
        //        }

        //        return SendRequest(cookieKey, GenerateRequest(Method.GET, url), cookieFilterRedirects);
        //    }

        //    return res;
        //}

        //public static RestRequest GenerateRequest(Method meth, string endpoint)
        //{
        //    return new RestRequest(endpoint, meth);
        //}

        //public static RestRequest GenerateRequest(Method meth, string endpoint, string referrer)
        //{
        //    var req = GenerateRequest(meth, endpoint);

        //    req.AddParameter("Referer", referrer, ParameterType.HttpHeader);

        //    return req;
        //}

        //public static RestRequest GenerateRequest(Method meth, string endpoint, string referrer, string origin)
        //{
        //    var req = GenerateRequest(meth, endpoint, referrer);

        //    req.AddParameter("Origin", origin, ParameterType.HttpHeader);

        //    return req;
        //}
    }
}