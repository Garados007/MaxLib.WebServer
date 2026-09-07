using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

#nullable enable

namespace MaxLib.WebServer.Test
{
    [TestClass]
    public class TestHttpCookie
    {
        [TestMethod]
        public void TestToStringOmitsSecurityAttributesByDefault()
        {
            var cookie = new HttpCookie.Cookie("name", "value");
            Assert.AreEqual("name=value;Path=", cookie.ToString());
        }

        [TestMethod]
        public void TestToStringEmitsHttpOnlySecureAndSameSiteWhenSet()
        {
            var cookie = new HttpCookie.Cookie(
                "name", "value", new DateTime(9999, 12, 31), -1, "/",
                httpOnly: true, secure: true, sameSite: HttpCookie.SameSiteMode.Strict
            );
            Assert.AreEqual("name=value;Path=/;HttpOnly;Secure;SameSite=Strict", cookie.ToString());
        }

        [TestMethod]
        public void TestToStringOmitsSameSiteWhenUnspecified()
        {
            var cookie = new HttpCookie.Cookie(
                "name", "value", new DateTime(9999, 12, 31), -1, "/",
                httpOnly: true, secure: false, sameSite: HttpCookie.SameSiteMode.Unspecified
            );
            Assert.AreEqual("name=value;Path=/;HttpOnly", cookie.ToString());
        }

        [TestMethod]
        [DataRow(HttpCookie.SameSiteMode.Lax, "Lax")]
        [DataRow(HttpCookie.SameSiteMode.Strict, "Strict")]
        [DataRow(HttpCookie.SameSiteMode.None, "None")]
        public void TestToStringRendersEachSameSiteValue(HttpCookie.SameSiteMode mode, string expected)
        {
            var cookie = new HttpCookie.Cookie(
                "name", "value", new DateTime(9999, 12, 31), -1, "",
                httpOnly: false, secure: false, sameSite: mode
            );
            StringAssert.Contains(cookie.ToString(), $";SameSite={expected}");
        }

        [TestMethod]
        public void TestToStringStripsCrLfFromPathButKeepsSlashesIntact()
        {
            // Path is not URL-encoded like Name/Value (it legitimately contains "/"), but a raw
            // CRLF must never reach the wire - see http-header-crlf-injection.md
            var cookie = new HttpCookie.Cookie(
                "name", "value", new DateTime(9999, 12, 31), -1, "/foo\r\nSet-Cookie: evil=1/bar"
            );
            Assert.AreEqual("name=value;Path=/fooSet-Cookie: evil=1/bar", cookie.ToString());
        }
    }
}
