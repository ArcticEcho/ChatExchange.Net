using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CsQuery;
using Newtonsoft.Json.Linq;



namespace ChatExchangeDotNet
{
    public class Client : IDisposable
    {
        private readonly Regex hostParser = new Regex("https?://chat.|/rooms/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly Regex idParser = new Regex(".*/rooms/|/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        //private readonly string password;
        //private readonly string email;
        //private string openIDIdentifier;
        private bool disposed;

        public List<Room> Rooms { get; private set; }



        public Client(string email, string password)
        {
            if (String.IsNullOrEmpty(email)) { throw new ArgumentException("'email' must not be null or empty.", "email"); }
            if (String.IsNullOrEmpty(password)) { throw new ArgumentException("'password' must not be null or empty.", "password"); }

            Rooms = new List<Room>();

            SEOpenIDLogin(email, password);
        }

        ~Client()
        {
            if (disposed) { return; }

            for (var i = 0; i < Rooms.Count; i++)
            {
                Rooms[i].Dispose();
            }

            disposed = true;		
        }



        public Room JoinRoom(string roomURL)
        {
            var host = hostParser.Replace(roomURL, "");
            var id = int.Parse(idParser.Replace(roomURL, ""));

            if (Rooms.Any(room => room.Host == host && room.ID == id)) { throw new Exception("You're already in this room."); }

            if (Rooms.All(room => room.Host != host))
            {
                if (host.ToLowerInvariant() == "stackexchange.com")
                {
                    throw new NotImplementedException();
                    //SEChatLogin();
                }

                SiteLogin(host);
            }

            var r = new Room(host, id);

            Rooms.Add(r);

            return r;
        }

        public void Dispose()
        {
            if (disposed) { return; }

            for (var i = 0; i < Rooms.Count; i++)
            {
                Rooms[i].Dispose();
            }

            GC.SuppressFinalize(this);

            disposed = true;
        }



        private void SEOpenIDLogin(string email, string password)
        {
            var getRes = RequestManager.SendGETRequest("https://openid.stackexchange.com/account/login");

            if (getRes == null) { throw new Exception("Could not get OpenID fkey. Do you have an active internet connection?"); }

            var getResContent = RequestManager.GetResponseContent(getRes);

            var data = "email=" + Uri.EscapeDataString(email) + "&password=" + Uri.EscapeDataString(password) + "&fkey=" + CQ.Create(getResContent).GetFkey();

            RequestManager.CookiesToPass = RequestManager.GlobalCookies;

            var res = RequestManager.SendPOSTRequest("https://openid.stackexchange.com/account/login/submit", data);

            if (res == null || !String.IsNullOrEmpty(res.Headers["p3p"])) { throw new Exception("Could not login using the specified OpenID credentials. Have you entered the correct credentials and have an active internet connection?"); }

            // ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ Temp. For Debug purposes only. ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ 
            var resContent = RequestManager.GetResponseContent(res);
            // ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ Temp. For Debug purposes only. ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ 

            //var dom = CQ.Create(userPage);

            //var ident = dom["#delegate code"][0].InnerHTML;
            //ident = WebUtility.HtmlDecode(ident).Trim();

            //dom = CQ.Create(ident);
            //ident = dom[3].Attributes["href"];

            //openIDIdentifier = ident;
        }

        private void SiteLogin(string host)
        {
            var getRes = RequestManager.SendGETRequest("http://" + host + "/users/login?returnurl = %%2f");

            if (getRes == null) { throw new Exception("Could not get fkey from " + host + ". Do you have an active internet connection?"); }

            var getResContent = RequestManager.GetResponseContent(getRes);

            var data = "oauth_version=null&oauth_server=null&openid_identifier=" + Uri.EscapeDataString("https://openid.stackexchange.com/") + "&fkey=" + CQ.Create(getResContent).GetFkey();

            var postRes = RequestManager.SendPOSTRequest("http://" + host + "/users/authenticate", data);

            if (postRes == null) { throw new Exception("Could not login into site " + host + ". Have you entered the correct credentials and have an active internet connection?"); }
        }

        //private void SEChatLogin()
        //{
        //    var fkeyHtml = RequestManager.GetResponseContent(RequestManager.SendGETRequest("http://stackexchange.com/users/chat-login"));

        //    var fkey = CQ.Create(fkeyHtml).GetFkey();

        //    var data = "fkey=" + fkey + "&oauth_version=&oauth_server=&openid_identifier=" /*+ Uri.EscapeDataString(openIDIdentifier)*/;

        //    var res = RequestManager.SendPOSTRequest("http://stackexchange.com/users/authenticate", data, true, "https://openid.stackexchange.com/account/login");
        //    var resContent = RequestManager.GetResponseContent(res);

        //    var dom = CQ.Create(resContent);

        //    var session = dom["session"][0];//;//dom["input"].FirstOrDefault(e => e.Attributes["name"] != null && e.Attributes["name"] == "session").Attributes["value"];

        //    // Handle prompt.
        //    if (session != null)
        //    {
        //        data = "session=" + session.Attributes["value"] + "&fkey=" + dom.GetFkey();

        //        RequestManager.SendPOSTRequest("https://openid.stackexchange.com/account/prompt/submit", data);
        //    }

        //    //string token;
        //    //string nonce;

        //    //GetTokenAndNonce(out token, out nonce);
        //    //GetGlobalLogin(nonce);


        //    //var authNonceRes = RequestManager.SendPOSTRequest("http://chat.stackexchange.com/users/login/global/request", "", true, "http://chat.stackexchange.com", "http://chat.stackexchange.com");

        //    //var authNonceContent = RequestManager.GetResponseContent(authNonceRes);

        //    //var authNonce = JObject.Parse(authNonceContent);

        //    //var token = (string)authNonce["token"];
        //    //var nonce = (string)authNonce["nonce"];

        //    //var loginTokenRes = RequestManager.SendGETRequest("https://stackauth.com/auth/global/read?request=" + Uri.EscapeDataString(token) + "&nonce=" + Uri.EscapeDataString(nonce), true, "http://chat.stackexchange.com/");

        //    //var loginTokenContent = RequestManager.GetResponseContent(loginTokenRes);






        //    //var res = RequestManager.SendPOSTRequest("http://stackexchange.com/users/signin", "from=http%3A%2F%2Fstackexchange.com%2Fusers%2Flogin%3Freturnurl%3D%252fusers%252fchat-login%23log-in", true, "http://stackexchange.com/users/login?returnurl=%2fusers%2fchat-login", "http://stackexchange.com");

        //    //// Get form.
        //    //var formUrl = RequestManager.GetResponseContent(res);
        //    //res = RequestManager.SendGETRequest(formUrl);
        //    //var formHtml = RequestManager.GetResponseContent(res);

        //    //// Get form data.
        //    //var dom = CQ.Create(formHtml);
        //    //var fkey = dom.GetFkey();
        //    //var affId = dom.GetAffId();

        //    //var data = "email=" + Uri.EscapeDataString(email) + "&password=" + Uri.EscapeDataString(password) + "&affId=" + affId + "&fkey=" + fkey;

        //    //res = RequestManager.SendPOSTRequest("https://openid.stackexchange.com/affiliate/form/login/submit", data, true, formUrl, "https://openid.stackexchange.com");

        //    //var resContent = RequestManager.GetResponseContent(res);

        //    //dom = CQ.Create(resContent);

        //    //var redirLink0 = dom["a"];
        //    //var redirLink1 = dom["a"][0];
        //    //var redirLink2 = dom["a"][0].Attributes["href"];
        //    //var redirLink3 = dom["noscript a"];
        //    //var redirLink4 = dom["noscript a"][0];
        //    //var redirLink5 = dom["noscript a"][0].Attributes["href"];
        //    //// Parse response for redirect link.





        //    //var req = RequestManager.GetResponseContent(RequestManager.SendGETRequest("http://stackexchange.com/users/chat-login"));

        //    //var dom = CQ.Create(req);

        //    //var authToken = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "authToken").Attributes["value"];
        //    //var nonce = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "nonce").Attributes["value"];

        //    //var data = "authToken=" + authToken + "&nonce=" + nonce;

        //    //var res = RequestManager.GetResponseContent(RequestManager.SendPOSTRequest("http://chat.stackexchange.com/login/global-fallback", data, true, "http://stackexchange.com/users/chat-login"));
        //}

        //private void GetTokenAndNonce(out string token, out string nonce)
        //{
        //    var tokenNonceRes = RequestManager.SendPOSTRequest("http://chat.stackexchange.com/users/login/global/request", "", true, "http://chat.stackexchange.com", "http://chat.stackexchange.com");

        //    var tokenNonceContent = RequestManager.GetResponseContent(tokenNonceRes);

        //    var tokenNonce = JObject.Parse(tokenNonceContent);

        //    token = (string)tokenNonce["token"];
        //    nonce = (string)tokenNonce["nonce"];
        //}

        //private void GetGlobalLogin(string nonce)
        //{
        //    var gauth = "";

        //    foreach (var cookie in RequestManager.GlobalCookies.GetCookies())
        //    {
        //        if (cookie.Name.ToLowerInvariant().Contains("gauth"))
        //        {
        //            gauth = cookie.Value;

        //            break;
        //        }
        //    }

        //    var url = "https://stackauth.com/auth/global/write?authToken=" + gauth + "&nonce=" + nonce + "&referrer=http://stackexchange.com/users/chat-login";

        //    var res = RequestManager.SendGETRequest(url, true/*, "http://stackexchange.com/users/chat-login"*/);

        //    var resContent = RequestManager.GetResponseContent(res);

        //    var globalLogin = resContent.Remove(resContent.IndexOf("setItem('GlobalLogin',", StringComparison.Ordinal) + 24);
        //    globalLogin = globalLogin.Remove(globalLogin.IndexOf("');", StringComparison.Ordinal));

        //}

        //private CookieContainer GetUsrCookie()
        //{
        //    var allCookies = RequestManager.GlobalCookies.GetCookies();
        //    var siteCookies = new CookieCollection();

        //    foreach (var cookie in allCookies)
        //    {
        //        if (cookie.Name.ToLowerInvariant().Contains("usr"))
        //        {
        //            siteCookies.Add(cookie);
        //        }
        //    }

        //    var cookies = new CookieContainer();

        //    cookies.Add(siteCookies);

        //    return cookies;
        //}
    }
}
