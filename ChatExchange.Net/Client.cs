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
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using AngleSharp.Parser.Html;
using RestSharp;

namespace ChatExchangeDotNet
{
	/// <summary>
	/// Provides access to chat rooms via logging in using OAuth.
	/// </summary>
	public class Client : IDisposable
	{
		private const string OPENID_SE                      = "https://openid.stackexchange.com";
		private const string OPENID_SE_USER                 = OPENID_SE + "/user";
		private const string OPENID_SE_ACCOUNT_LOGIN        = OPENID_SE + "/account/login";
		private const string OPENID_SE_ACCOUNT_LOGIN_SUBMIT = OPENID_SE_ACCOUNT_LOGIN + "/submit";

		private const string USERS_LOGIN   = "https://{0}/users/login";
		private const string USERS_AUTH    = "https://{0}/users/authenticate";
		private const string USERS_CURRENT = "https://{0}/users/current";

		private readonly Regex openIdDelegateRegex = new Regex("https://openid\\.stackexchange\\.com/user/(.*?)\"", Extensions.RegexOpts);
		private readonly Regex hostParserRegex     = new Regex("https?://(chat.)?|/.*", Extensions.RegexOpts);
		private readonly Regex idParserRegex       = new Regex(".*/rooms/|/.*", Extensions.RegexOpts);
		private readonly string accountEmail;
		private readonly string accountPassword;
		private readonly string cookieKey;
		private readonly string openIDIdentifier;
		private bool disposed;

		/// <summary>
		/// Returns a collection of rooms the user is currently in.
		/// </summary>
		public ReadOnlyCollection<Room> Rooms { get; private set; }


		/// <summary>
		/// Logs in using the provided credentials.
		/// </summary>
		/// <param name="email">The account's registered OAuth email.</param>
		/// <param name="password">The account's password.</param>
		public Client(string email, string password)
		{
			if (string.IsNullOrEmpty(email) || !email.Contains("@"))
			{
				throw new ArgumentException("Invalid email address.", nameof(email));
			}
			if (string.IsNullOrEmpty(password))
			{
				throw new ArgumentNullException(nameof(password));
			}

			accountEmail = email;
			accountPassword = password;
			cookieKey = email.Split('@')[0];

			if (RequestManager.Cookies.ContainsKey(cookieKey))
			{
				throw new Exception("Cannot create multiple instances of the same user.");
			}

			Rooms = new ReadOnlyCollection<Room>(new List<Room>());

			openIDIdentifier = GetSEOpenIdIdentifier();
		}

#pragma warning disable CS1591
		~Client()
		{
			Dispose();
		}
#pragma warning restore CS1591



		/// <summary>
		/// Joins a chat room specified by its URL.
		/// </summary>
		/// <param name="roomUrl">The URL of the chat room to join.</param>
		/// <returns>A Room object which provides access to chat events/actions.</returns>
		/// <param name="loadUsersAsync">
		/// Specifies whether the Room object should fetch the: room owners,
		/// current users and pingable users lists asynchronously (true) or not (false).
		/// </param>
		/// <exception cref="Exception">
		/// Thrown if you attempt to join a room you are currently in.
		/// </exception>
		public Room JoinRoom(string roomUrl, bool loadUsersAsync = false)
		{
			var host = hostParserRegex.Replace(roomUrl, "");
			var id = int.Parse(idParserRegex.Replace(roomUrl, ""));

			return JoinRoom(host, id, loadUsersAsync);
		}

		/// <summary>
		/// Joins a chat room specified by its host and ID.
		/// </summary>
		/// <param name="host">
		/// The host domain of the chat room.
		/// For example: meta.stackexchange.com</param>
		/// <param name="roomID">The unique identification number of the room.</param>
		/// <param name="loadUsersAsync">
		/// Specifies whether the Room object should fetch the: room owners,
		/// current users and pingable users lists asynchronously (true) or not (false).
		/// </param>
		/// <returns>A Room object which provides access to chat events/actions.</returns>
		/// <exception cref="Exception">
		/// Thrown if you attempt to join a room you are currently in.
		/// </exception>
		public Room JoinRoom(string host, int roomID, bool loadUsersAsync = false)
		{
			if (Rooms.Any(room => room.Meta.Host == host && room.Meta.ID == roomID && !room.dispose))
			{
				throw new Exception("Cannot join a room you are already in.");
			}

			if (Rooms.All(room => room.Meta.Host != host))
			{
				Login(host);
			}

			var r = new Room(cookieKey, host, roomID, loadUsersAsync);
			var rms = Rooms.ToList();
			rms.Add(r);

			Rooms = new ReadOnlyCollection<Room>(rms);

			return r;
		}

		public void Dispose()
		{
			if (disposed) return;
			disposed = true;

			if (Rooms?.Count > 0)
			{
				foreach (var room in Rooms)
				{
					room.Dispose();
				}
			}

			if (!string.IsNullOrEmpty(cookieKey))
			{
				RequestManager.Cookies.Remove(cookieKey);
			}

			GC.SuppressFinalize(this);
		}



		private string GetSEOpenIdIdentifier()
		{
			var fkeyHtml = RequestManager.SimpleGet(OPENID_SE_ACCOUNT_LOGIN, cookieKey);

			if (fkeyHtml == null)
			{
				throw new Exception($"Null response received while fetching page {OPENID_SE_ACCOUNT_LOGIN}.");
			}

			var fkey = new HtmlParser().Parse(fkeyHtml).GetFKey();
			var req = RequestManager.GenerateRequest(Method.POST, OPENID_SE_ACCOUNT_LOGIN_SUBMIT);

			req.AddObject(new
			{
				email = accountEmail,
				password = accountPassword,
				fkey = fkey
			});

			var response = RequestManager.SendRequest(req, cookieKey);

			if (response.ResponseUri.ToString() != OPENID_SE_USER)
			{
				throw new AuthenticationException("Invalid OpenID credentials.");
			}

			var userId = openIdDelegateRegex.Match(response.Content).Groups[1].Value;

			return $"{OPENID_SE_USER}/{userId}";
		}

		private void Login(string host)
		{
			var fkeyPageUrl = String.Format(USERS_LOGIN, host);
			var fkeyHtml = RequestManager.SimpleGet(fkeyPageUrl, cookieKey);

			if (fkeyHtml == null)
			{
				throw new Exception($"Null response received while fetching page {fkeyPageUrl}.");
			}

			var fkey = new HtmlParser().Parse(fkeyHtml).GetFKey();
			var authUrl = String.Format(USERS_AUTH, host);
			var req = RequestManager.GenerateRequest(Method.POST, authUrl);

			req.AddObject(new
			{
				openid_identifier = openIDIdentifier,
				fkey = fkey
			});

			var res = RequestManager.SendRequest(req, cookieKey);

			TryFetchUserId(host);
		}

		private void TryFetchUserId(string host)
		{
			var url = String.Format(USERS_CURRENT, host);
			var req = RequestManager.GenerateRequest(Method.GET, url);
			var res = RequestManager.SendRequest(req, cookieKey);
			var split = res.ResponseUri.ToString().Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

			if (split.Length < 4 || split[2] != "users" || !int.TryParse(split[3], out var id))
			{
				throw new AuthenticationException("Unable to retrieve user ID.");
			}
		}
	}
}
