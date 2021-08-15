using System;
using System.Collections.Generic;
using System.Text;

namespace CookieService
{
    public interface ICookieService
    {
        void SetCookie(string key, string value, int? expireTime, bool isSecure, bool isHttpOnly);
        void SetCookie(string key, string value, int? expireTime);
        void DeleteCookie(string key);
        void DeleteAllCookie(IEnumerable<string> cookiesToDelete);
    }
}
