using System;
using System.IO;
using System.Net;
using System.Text;



namespace ChatExchangeDotNet
{
    public class RequestManager
    {
	    readonly CookieContainer cookies = new CookieContainer();

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

        public HttpWebResponse SendPOSTRequest(string uri, string content, string login, string password, bool allowAutoRedirect)
        {
            HttpWebRequest request = GeneratePOSTRequest(uri, content, login, password, allowAutoRedirect);

            return GetResponse(request);
        }

        public HttpWebResponse SendGETRequest(string uri, string login, string password, bool allowAutoRedirect)
        {
            HttpWebRequest request = GenerateGETRequest(uri, login, password, allowAutoRedirect);

            return GetResponse(request);
        }

        public HttpWebResponse SendRequest(string uri, string content, string method, string login, string password, bool allowAutoRedirect)
        {
            HttpWebRequest request = GenerateRequest(uri, content, method, login, password, allowAutoRedirect);

            return GetResponse(request);
        }

        public HttpWebRequest GenerateGETRequest(string uri, string login, string password, bool allowAutoRedirect)
        {
			return GenerateRequest(uri, null, "GET", login, password, allowAutoRedirect);
        }

        public HttpWebRequest GeneratePOSTRequest(string uri, string content, string login, string password, bool allowAutoRedirect)
        {
            return GenerateRequest(uri, content, "POST", login, password, allowAutoRedirect);
        }

        internal HttpWebRequest GenerateRequest(string uri, string content, string method, string login, string password, bool allowAutoRedirect)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }
            // Create a request using a URL that can receive a post. 
            var request = (HttpWebRequest)WebRequest.Create(uri);
            // Set the Method property of the request to POST.
            request.Method = method;
            // Set cookie container to maintain cookies
            request.CookieContainer = cookies;
	        request.AllowAutoRedirect = allowAutoRedirect;
            // If login is empty use defaul credentials
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
			catch (Exception ex)
			{

			}

			return response;
		}
	}
}