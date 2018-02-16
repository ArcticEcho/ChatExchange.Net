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
using System.Linq;
using System.Net;
using RestSharp;

namespace ChatExchangeDotNet
{
	internal static class RequestManager
	{
		public static Dictionary<string, Dictionary<string, RestResponseCookie>> Cookies { get; private set; }



		static RequestManager()
		{
			Cookies = new Dictionary<string, Dictionary<string, RestResponseCookie>>();
		}



		public static string SimpleGet(string url, string cookieKey = null)
		{
			var req = GenerateRequest(Method.GET, url);
			return SendRequest(req, cookieKey).Content;
		}

		public static RestResponse SendRequest(RestRequest req, string cookieKey = null)
		{
			var reqUri = new Uri(req.Resource);
			var baseReqUrl = reqUri.Scheme + "://" + reqUri.Host;
			var enableCookies = !string.IsNullOrWhiteSpace(cookieKey);

			// RestSharp doesn't currently honour cookie headers
			// upon redirect (which is crucial for authentication
			// in our case). So I've implemented my own (crude)
			// means of following redirects (302s) in the meantime.
			var c = new RestClient(baseReqUrl)
			{
				FollowRedirects = false
			};

			if (enableCookies && Cookies.ContainsKey(cookieKey))
			{
				foreach (var cookie in Cookies[cookieKey].Values)
				{
					req.AddCookie(cookie.Name, cookie.Value);
				}
			}

			req.Resource = req.Resource.Remove(0, baseReqUrl.Length);

			var res = (RestResponse)c.Execute(req);

			if (enableCookies && res.Cookies?.Count > 0)
			{
				if (!Cookies.ContainsKey(cookieKey))
				{
					Cookies[cookieKey] = new Dictionary<string, RestResponseCookie>();
				}

				foreach (var cookie in res.Cookies)
				{
					if (Cookies[cookieKey].ContainsKey(cookie.Name))
					{
						if (cookie.Expired || cookie.Discard)
						{
							Cookies[cookieKey].Remove(cookie.Name);
						}
						else
						{
							Cookies[cookieKey][cookie.Name] = cookie;
						}
					}
					else
					{
						Cookies[cookieKey].Add(cookie.Name, cookie);
					}
				}
			}

			if (res.StatusCode == HttpStatusCode.Found)
			{
				var url = res.Headers.Single(x => x.Name == "Location").Value.ToString();

				if (!url.StartsWith("http"))
				{
					url = baseReqUrl + url;
				}

				return SendRequest(GenerateRequest(Method.GET, url), cookieKey);
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