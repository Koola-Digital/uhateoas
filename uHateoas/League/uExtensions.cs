using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace uHateoas.League
{
    public static class UExtensions
    {
        public const string AppSettingsPrefix = "League.Hypermedia";
        public const string CachePrefix = "UHATEOAS-";

        public static bool AsBoolean(this string s)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(s)) return false;
                return s == "1" || s != "0" && bool.Parse(s);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsNumeric(this string sNumber)
        {
            return double.TryParse(sNumber, out _);
        }

        public static int AsInteger(this string s)
        {
            return s.AsInteger(0);
        }

        public static int AsInteger(this string s, int fallback)
        {
            try
            {
                return string.IsNullOrWhiteSpace(s) ? fallback : int.Parse(s);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public static string StripHtml(this string s)
        {
            // https://stackoverflow.com/questions/787932/using-c-sharp-regular-expressions-to-remove-html-tags/787987
            var regex = new Regex(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", RegexOptions.Singleline);
            return regex.Replace(s, "");
        }

        public static string SmartReplace(this string str, object properties)
        {
            return str.SmartReplace('@', properties);
        }

        public static string SmartReplace(this string str, char prefix, object properties)
        {
            return properties.GetType().GetProperties().
                Aggregate(str, (current, prop) => current.Replace($"{prefix}{prop.Name}", prop.GetValue(properties, null).ToString()));
        }

        public static byte[] GetHash(string inputString)
        {
            HashAlgorithm algorithm = SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        public static string GetHashString(string inputString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        private static readonly Dictionary<string, string> BinaryReplacements = new Dictionary<string, string>
        {
            {" eq ", " = "},
            {" ge ", " >= "},
            {" gt ", " > "},
            {" le ", " <= "},
            {" lt ", " < "},
            {" ne ", " != "},
            {"'", "\""},
            {" and ", " && "},
            {" or ", " || "}
        };

        public static bool SkipDomainCheck()
        {
            if (ConfigurationManager.AppSettings.AllKeys.Contains($"{UExtensions.AppSettingsPrefix}.SkipCacheDomains"))
            {
                var configSkipDomains = ConfigurationManager
                    .AppSettings[$"{AppSettingsPrefix}.SkipCacheDomains"].ToLower();
                if (string.IsNullOrEmpty(configSkipDomains))
                {
                    return false;
                }
                var skipDomains = configSkipDomains.Split(',');
                var siteDomain = HttpContext.Current.Request.Url.Host.ToLower();
                return skipDomains.Any(skipDomain => siteDomain.Contains(skipDomain));
            }

            return false;
        }
        public static string GetDetails(this HttpRequest request)
        {
            string baseUrl = $"{request.RawUrl}";
            StringBuilder sbHeaders = new StringBuilder();
            foreach (var header in request.Headers.AllKeys)
                sbHeaders.Append($"{header}: {request.Headers[header]}\n");

            string body = "no-body";
            if (request.InputStream.CanSeek)
            {
                request.InputStream.Seek(0, SeekOrigin.Begin);
                using (StreamReader sr = new StreamReader(request.InputStream))
                    body = sr.ReadToEnd();
            }
            var protocol = request.IsSecureConnection ? "HTTPS" : "HTTP";
            return $"{protocol} {request.HttpMethod} {baseUrl}\n\n{sbHeaders}\n{body}";
        }

        public static string ChangeBinary(this string s)
        {
            return BinaryReplacements.Keys.Aggregate(s, (current, toReplace) => current.Replace(toReplace, BinaryReplacements[toReplace]));
            //return s.Replace(" eq ", " = ").Replace(" ge ", " >= ").Replace(" gt ", " > ").Replace(" le ", " <= ")
            //    .Replace(" lt ", " < ").Replace(" ne ", " != ").Replace("'", "\"").Replace(" and ", " && ")
            //    .Replace(" or ", " || ");
        }
    }
}