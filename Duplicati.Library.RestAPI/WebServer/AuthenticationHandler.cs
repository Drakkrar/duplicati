// Copyright (C) 2024, The Duplicati Team
// https://duplicati.com, hello@duplicati.com
// 
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
using System;
using System.Collections.Concurrent;
using System.Linq;
using HttpServer;
using HttpServer.HttpModules;
using System.Collections.Generic;
using Duplicati.Library.RestAPI;

namespace Duplicati.Server.WebServer
{
    public class AuthenticationHandler : HttpModule
    {
        private const string AUTH_COOKIE_NAME = "session-auth";
        private const string NONCE_COOKIE_NAME = "session-nonce";

        private const string XSRF_COOKIE_NAME = "xsrf-token";
        private const string XSRF_HEADER_NAME = "X-XSRF-Token";

        private const string TRAYICONPASSWORDSOURCE_HEADER = "X-TrayIcon-PasswordSource";

        public const string LOGIN_SCRIPT_URI = "/login.cgi";
        public const string LOGOUT_SCRIPT_URI = "/logout.cgi";
        public const string CAPTCHA_IMAGE_URI = RESTHandler.API_URI_PATH + "/captcha/";

        private const int XSRF_TIMEOUT_MINUTES = 10;
        private const int AUTH_TIMEOUT_MINUTES = 10;

        private readonly ConcurrentDictionary<string, DateTime> m_activeTokens = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, Tuple<DateTime, string>> m_activeNonces = new ConcurrentDictionary<string, Tuple<DateTime, string>>();
        private readonly ConcurrentDictionary<string, DateTime> m_activexsrf = new ConcurrentDictionary<string, DateTime>();

        readonly System.Security.Cryptography.RandomNumberGenerator m_prng = System.Security.Cryptography.RNGCryptoServiceProvider.Create();

        private string FindXSRFToken(HttpServer.IHttpRequest request)
        {
            string xsrftoken = request.Headers[XSRF_HEADER_NAME] ?? "";

            if (string.IsNullOrWhiteSpace(xsrftoken))
            {
                var xsrfq = request.Form[XSRF_HEADER_NAME] ?? request.Form[Duplicati.Library.Utility.Uri.UrlEncode(XSRF_HEADER_NAME)];
                xsrftoken = (xsrfq == null || string.IsNullOrWhiteSpace(xsrfq.Value)) ? "" : xsrfq.Value;
            }

            if (string.IsNullOrWhiteSpace(xsrftoken))
            {
                var xsrfq = request.QueryString[XSRF_HEADER_NAME] ?? request.QueryString[Duplicati.Library.Utility.Uri.UrlEncode(XSRF_HEADER_NAME)];
                xsrftoken = (xsrfq == null || string.IsNullOrWhiteSpace(xsrfq.Value)) ? "" : xsrfq.Value;
            }

            return xsrftoken;
        }

        private bool AddXSRFTokenToRespone(HttpServer.IHttpResponse response)
        {
            if (m_activexsrf.Count > 500)
                return false;

            var buf = new byte[32];
            var expires = DateTime.UtcNow.AddMinutes(XSRF_TIMEOUT_MINUTES);
            m_prng.GetBytes(buf);
            var token = Convert.ToBase64String(buf);

            m_activexsrf.AddOrUpdate(token, key => expires, (key, existingExpires) =>
            {
                // Simulate the original behavior => if the random token, against all odds, is already used
                // we throw an ArgumentException
                throw new ArgumentException("An element with the same key already exists in the dictionary.");
            });

            response.Cookies.Add(new HttpServer.ResponseCookie(XSRF_COOKIE_NAME, token, expires));
            return true;
        }

        private string FindAuthCookie(HttpServer.IHttpRequest request)
        {
            var authcookie = request.Cookies[AUTH_COOKIE_NAME] ?? request.Cookies[Library.Utility.Uri.UrlEncode(AUTH_COOKIE_NAME)];
            var authform = request.Form["auth-token"] ?? request.Form[Library.Utility.Uri.UrlEncode("auth-token")];
            var authquery = request.QueryString["auth-token"] ?? request.QueryString[Library.Utility.Uri.UrlEncode("auth-token")];

            var auth_token = string.IsNullOrWhiteSpace(authcookie?.Value) ? null : authcookie.Value;
            if (!string.IsNullOrWhiteSpace(authquery?.Value))
                auth_token = authquery.Value;
            if (!string.IsNullOrWhiteSpace(authform?.Value))
                auth_token = authform.Value;

            return auth_token;
        }

        private bool HasXSRFCookie(HttpServer.IHttpRequest request)
        {
            // Clean up expired XSRF cookies
            foreach (var k in (from n in m_activexsrf where DateTime.UtcNow > n.Value select n.Key))
                m_activexsrf.TryRemove(k, out _);

            var xsrfcookie = request.Cookies[XSRF_COOKIE_NAME] ?? request.Cookies[Library.Utility.Uri.UrlEncode(XSRF_COOKIE_NAME)];
            var value = xsrfcookie == null ? null : xsrfcookie.Value;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (m_activexsrf.ContainsKey(value))
            {
                m_activexsrf[value] = DateTime.UtcNow.AddMinutes(XSRF_TIMEOUT_MINUTES);
                return true;
            }
            else if (m_activexsrf.ContainsKey(Library.Utility.Uri.UrlDecode(value)))
            {
                m_activexsrf[Library.Utility.Uri.UrlDecode(value)] = DateTime.UtcNow.AddMinutes(XSRF_TIMEOUT_MINUTES);
                return true;
            }

            return false;
        }

        public override bool Process(HttpServer.IHttpRequest request, HttpServer.IHttpResponse response, HttpServer.Sessions.IHttpSession session)
        {
            HttpServer.HttpInput input = String.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase) ? request.Form : request.QueryString;

            var auth_token = FindAuthCookie(request);
            var xsrf_token = FindXSRFToken(request);

            if (!HasXSRFCookie(request))
            {
                var cookieAdded = AddXSRFTokenToRespone(response);

                if (!cookieAdded)
                {
                    response.Status = System.Net.HttpStatusCode.ServiceUnavailable;
                    response.Reason = "Too Many Concurrent Request, try again later";
                    return true;
                }
            }

            if (LOGOUT_SCRIPT_URI.Equals(request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(auth_token))
                {
                    // Remove the active auth token
                    m_activeTokens.TryRemove(auth_token, out _);
                }

                response.Status = System.Net.HttpStatusCode.NoContent;
                response.Reason = "OK";

                return true;
            }
            else if (LOGIN_SCRIPT_URI.Equals(request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                // Remove expired nonces
                foreach(var k in (from n in m_activeNonces where DateTime.UtcNow > n.Value.Item1 select n.Key))
                    m_activeNonces.TryRemove(k, out _);

                if (input["get-nonce"] != null && !string.IsNullOrWhiteSpace(input["get-nonce"].Value))
                {
                    if (m_activeNonces.Count > 50)
                    {
                        response.Status = System.Net.HttpStatusCode.ServiceUnavailable;
                        response.Reason = "Too many active login attempts";
                        return true;
                    }

                    var password = FIXMEGlobal.DataConnection.ApplicationSettings.WebserverPassword;

                    if (request.Headers[TRAYICONPASSWORDSOURCE_HEADER] == "database")
                        password = FIXMEGlobal.DataConnection.ApplicationSettings.WebserverPasswordTrayIconHash;
                    
                    var buf = new byte[32];
                    var expires = DateTime.UtcNow.AddMinutes(AUTH_TIMEOUT_MINUTES);
                    m_prng.GetBytes(buf);
                    var nonce = Convert.ToBase64String(buf);

                    var sha256 = System.Security.Cryptography.SHA256.Create();
                    sha256.TransformBlock(buf, 0, buf.Length, buf, 0);
                    buf = Convert.FromBase64String(password);
                    sha256.TransformFinalBlock(buf, 0, buf.Length);
                    var pwd = Convert.ToBase64String(sha256.Hash);

                    m_activeNonces.AddOrUpdate(nonce, key => new Tuple<DateTime, string>(expires, pwd), (key, existingValue) =>
                    {
                        // Simulate the original behavior => if the nonce, against all odds, is already used
                        // we throw an ArgumentException
                        throw new ArgumentException("An element with the same key already exists in the dictionary.");
                    });

                    response.Cookies.Add(new HttpServer.ResponseCookie(NONCE_COOKIE_NAME, nonce, expires));
                    using(var bw = new BodyWriter(response, request))
                    {
                        bw.OutputOK(new {
                            Status = "OK",
                            Nonce = nonce,
                            Salt = FIXMEGlobal.DataConnection.ApplicationSettings.WebserverPasswordSalt
                        });
                    }
                    return true;
                }
                else
                {
                    if (input["password"] != null && !string.IsNullOrWhiteSpace(input["password"].Value))
                    {
                        var nonce_el = request.Cookies[NONCE_COOKIE_NAME] ?? request.Cookies[Library.Utility.Uri.UrlEncode(NONCE_COOKIE_NAME)];
                        var nonce = nonce_el == null || string.IsNullOrWhiteSpace(nonce_el.Value) ? "" : nonce_el.Value;
                        var urldecoded = nonce == null ? "" : Duplicati.Library.Utility.Uri.UrlDecode(nonce);
                        if (m_activeNonces.ContainsKey(urldecoded))
                            nonce = urldecoded;

                        if (!m_activeNonces.ContainsKey(nonce))
                        {
                            response.Status = System.Net.HttpStatusCode.Unauthorized;
                            response.Reason = "Unauthorized";
                            response.ContentType = "application/json";
                            return true;
                        }

                        var pwd = m_activeNonces[nonce].Item2;

                        // Remove the nonce
                        m_activeNonces.TryRemove(nonce, out _);

                        if (pwd != input["password"].Value)
                        {
                            response.Status = System.Net.HttpStatusCode.Unauthorized;
                            response.Reason = "Unauthorized";
                            response.ContentType = "application/json";
                            return true;
                        }

                        var buf = new byte[32];
                        var expires = DateTime.UtcNow.AddHours(1);
                        m_prng.GetBytes(buf);
                        var token = Duplicati.Library.Utility.Utility.Base64UrlEncode(buf);
                        while (token.Length > 0 && token.EndsWith("=", StringComparison.Ordinal))
                            token = token.Substring(0, token.Length - 1);

                        m_activeTokens.AddOrUpdate(token, key => expires, (key, existingValue) =>
                        {
                            // Simulate the original behavior => if the token, against all odds, is already used
                            // we throw an ArgumentException
                            throw new ArgumentException("An element with the same key already exists in the dictionary.");
                        });

                        response.Cookies.Add(new  HttpServer.ResponseCookie(AUTH_COOKIE_NAME, token, expires));

                        using(var bw = new BodyWriter(response, request))
                            bw.OutputOK();

                        return true;
                    }
                }
            }

            var limitedAccess =
                request.Uri.AbsolutePath.StartsWith(RESTHandler.API_URI_PATH, StringComparison.OrdinalIgnoreCase)
            ;

            // Override to allow the CAPTCHA call to go through
            if (request.Uri.AbsolutePath.StartsWith(CAPTCHA_IMAGE_URI, StringComparison.OrdinalIgnoreCase) && request.Method == "GET")
                limitedAccess = false;

            if (limitedAccess)
            {
                if (xsrf_token != null && m_activexsrf.ContainsKey(xsrf_token))
                {
                    var expires = DateTime.UtcNow.AddMinutes(XSRF_TIMEOUT_MINUTES);
                    m_activexsrf[xsrf_token] = expires;
                    response.Cookies.Add(new ResponseCookie(XSRF_COOKIE_NAME, xsrf_token, expires));
                }
                else
                {
                    response.Status = System.Net.HttpStatusCode.BadRequest;
                    response.Reason = "Missing XSRF Token. Please reload the page";

                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(FIXMEGlobal.DataConnection.ApplicationSettings.WebserverPassword))
                return false;

            foreach(var k in (from n in m_activeTokens where DateTime.UtcNow > n.Value select n.Key))
                m_activeTokens.TryRemove(k, out _);


            // If we have a valid token, proceed
            if (!string.IsNullOrWhiteSpace(auth_token))
            {
                DateTime expires;
                var found = m_activeTokens.TryGetValue(auth_token, out expires);
                if (!found)
                {
                    auth_token = Duplicati.Library.Utility.Uri.UrlDecode(auth_token);
                    found = m_activeTokens.TryGetValue(auth_token, out expires);
                }

                if (found && DateTime.UtcNow < expires)
                {
                    expires = DateTime.UtcNow.AddHours(1);

                    m_activeTokens[auth_token] = expires;
                    response.Cookies.Add(new ResponseCookie(AUTH_COOKIE_NAME, auth_token, expires));
                    return false;
                }
            }

            if ("/".Equals(request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase) || "/index.html".Equals(request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                response.Redirect("/login.html");
                return true;
            }
                
            if (limitedAccess)
            {
                response.Status = System.Net.HttpStatusCode.Unauthorized;
                response.Reason = "Not logged in";
                response.AddHeader("Location", "login.html");

                return true;
            }

            return false;
        }
    }
}

