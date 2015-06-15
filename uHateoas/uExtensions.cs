using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace wg2k.umbraco
{
    public static class uExtensions
    {
        public static bool AsBoolean(this string s)
        {
            try
            {
                if (s == null) return false;
                if (s == string.Empty) return false;
                if (s.Trim() == string.Empty) return false;
                return s == "1" || s != "0" && Boolean.Parse(s);
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool IsNumeric(this string sNumber)
        {
            double myNum = 0;
            return Double.TryParse(sNumber, out myNum);
        }
        public static int AsInteger(this string s)
        {
            return s.AsInteger(0);
        }
        public static int AsInteger(this string s, int fallback)
        {
            try
            {
                if (s == null) return fallback;
                if (s == string.Empty) return fallback;
                if (s.Trim() == string.Empty) return fallback;
                return int.Parse(s);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
        public static string StripHtml(this string s)
        {
            s = s.Replace("<br />", ",").Replace("</p>", ",").Replace("</tr>", ",").Replace("</li>", ",").Replace("</div>", ",").Replace("</td>", " : ").Replace("</h1>", " : ").Replace("</h2>", " : ").Replace("</h3>", " : ").Replace("</h4>", " : ").Replace(".,", ".").Replace("&nbsp;", " ").Replace("&amp;", "&");
            return Regex.Replace(s, "<[^>]+?>", "");
        }
        public static string SmartReplace(this string str, object properties)
        {
            return str.SmartReplace('@', properties);
        }
        public static string SmartReplace(this string str, char prefix, object properties)
        {
            PropertyInfo[] props = properties.GetType().GetProperties();
            foreach (PropertyInfo prop in props)
            {
                string name = string.Format("{0}{1}", prefix, prop.Name);
                str = str.Replace(name, prop.GetValue(properties, null).ToString());
            }
            return str;
        }
    }
}