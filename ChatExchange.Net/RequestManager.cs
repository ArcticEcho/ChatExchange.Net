using System;
using System.IO;
using System.Net;
using System.Text;



namespace ChatExchangeDotNet
{
    public class RequestManager
    {
	    private readonly CookieContainer cookies = new CookieContainer();
	    private int responseTryCount;

        public string LastResponse { protected set; get; }



        internal string GetCookieValue(Uri SiteUri, string name)
        {
            Cookie cookie = cookies.GetCookies(SiteUri)[name];

            return (cookie == null) ? null : cookie.Value;
        }

        public string GetResponseContent(HttpWebResponse response)
        {
            if (response == null) { throw new ArgumentNullException("response"); }

            Stream dataStream = null;
            StreamReader reader = null;
            string responseFromServer;

            try
            {
                // Get the stream containing content returned by the server.
                dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                reader = new StreamReader(dataStream);
                // Read the content.
                responseFromServer = reader.ReadToEnd();
                // Cleanup the streams and the response.
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

            LastResponse = responseFromServer;

            return responseFromServer;
        }

		public HttpWebResponse SendPOSTRequest(string uri, string content, bool allowAutoRedirect = true, string referer = "", string login = "", string password = "")
        {
			return GetResponse(GeneratePOSTRequest(uri, content, allowAutoRedirect, referer, login, password));
        }

		public HttpWebResponse SendGETRequest(string uri, bool allowAutoRedirect = true, string login = "", string password = "")
        {
            return GetResponse(GenerateGETRequest(uri, allowAutoRedirect, login, password));
        }

		public HttpWebResponse SendRequest(string uri, string content, string method, bool allowAutoRedirect = true, string login = "", string password = "")
        {
			return GetResponse(GenerateRequest(uri, content, method, allowAutoRedirect, login, password));
        }

		public HttpWebRequest GenerateGETRequest(string uri, bool allowAutoRedirect = true, string login = "", string password = "")
        {
			return GenerateRequest(uri, null, "GET", allowAutoRedirect, login, password);
        }

		public HttpWebRequest GeneratePOSTRequest(string uri, string content, bool allowAutoRedirect = true, string referer = "", string login = "", string password = "")
        {
            return GenerateRequest(uri, content, "POST", allowAutoRedirect, referer, login, password);
        }

		internal HttpWebRequest GenerateRequest(string uri, string content, string method, bool allowAutoRedirect = true, string referer = "", string login = "", string password = "")
        {
            if (uri == null) { throw new ArgumentNullException("uri"); }

			var request = (HttpWebRequest)WebRequest.Create(uri);

			request.Method = method;

			request.CookieContainer = cookies;
	        request.AllowAutoRedirect = allowAutoRedirect;

			if (!String.IsNullOrEmpty(referer))
			{
				request.Referer = referer;
			}

			request.Credentials = string.IsNullOrEmpty(login) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(login, password);

            if (method == "POST")
            {
                // Convert POST data to a byte array.
                var byteArray = Encoding.UTF8.GetBytes(content);
                // Set the ContentType property of the WebRequest.
                request.ContentType = "application/x-www-form-urlencoded";
                // Set the ContentLength property of the WebRequest.
                request.ContentLength = byteArray.Length;
				// Get the request stream.
				var dataStream = request.GetRequestStream();
				// Write the data to the request stream.
				dataStream.Write(byteArray, 0, byteArray.Length);
				// Close the Stream object.
				dataStream.Close();
			}

			return request;
		}

		internal HttpWebResponse GetResponse(HttpWebRequest request)
		{
			if (request == null) { throw new ArgumentNullException("request"); }

			HttpWebResponse response = null;

			try
			{
				response = (HttpWebResponse)request.GetResponse();
				cookies.Add(response.Cookies);
			}
			catch (WebException ex)
			{
				if (responseTryCount == 5) { return response; }

				responseTryCount++;

				GetResponse(request);
			}
			catch (Exception ex)
			{

			}

			responseTryCount = 0;

			return response;
		}
	}
}