using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CsQuery;



namespace ChatExchangeDotNet
{
    public class Client : IDisposable
    {
        private readonly Regex hostParser = new Regex("https?://chat.|/rooms/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly Regex idParser = new Regex(".*/rooms/|/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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



        public Room JoinRoom(string roomUrl)
        {
            var host = hostParser.Replace(roomUrl, "");
            var id = int.Parse(idParser.Replace(roomUrl, ""));

            if (Rooms.Any(room => room.Host == host && room.ID == id)) { throw new Exception("You're already in this room."); }

            if (Rooms.All(room => room.Host != host))
            {
                if (host.ToLowerInvariant() == "stackexchange.com")
                {
                    throw new NotImplementedException();
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
        }

        private void SiteLogin(string host)
        {
            var getRes = RequestManager.SendGETRequest("http://" + host + "/users/login?returnurl = %%2f");

            if (getRes == null) { throw new Exception("Could not get fkey from " + host + ". Do you have an active internet connection?"); }

            var getResContent = RequestManager.GetResponseContent(getRes);

            var data = "oauth_version=null&oauth_server=null&openid_identifier=" + Uri.EscapeDataString("https://openid.stackexchange.com/") + "&fkey=" + CQ.Create(getResContent).GetFkey();

            var postRes = RequestManager.SendPOSTRequest("http://" + host + "/users/authenticate", data);

            if (postRes == null) { throw new Exception("Could not login into site " + host + ". Have you entered the correct credentials and have an active internet connection?"); }

            // ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ Temp, for debug purposes only ~ ~ ~ ~ ~ ~ ~ ~ ~ ~
            var resContent = RequestManager.GetResponseContent(postRes);
            // ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ Temp, for debug purposes only ~ ~ ~ ~ ~ ~ ~ ~ ~ ~
        }
    }
}
