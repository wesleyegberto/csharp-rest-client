using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpRestClient.Builder {
    public static class UrlHelper {
        public static string FormatQueryParamsToUrl(Dictionary<string, string> parameters) {
            if (parameters == null || parameters.Count == 0)
                return String.Empty;
            var urlAppend = new StringBuilder();
            foreach (var entry in parameters) {
                urlAppend.Append(entry.Key)
                    .Append("=")
                    .Append(entry.Value)
                    .Append("&");
            }
            if (urlAppend.Length == 0)
                return String.Empty;
            // remove the last '&'
            return urlAppend.ToString(0, urlAppend.Length - 1);
        }

        public static string GetUrlWithQueryParams(string url, string queryParams) {
            if (queryParams == null || queryParams == String.Empty)
                return url;
            if (url.Contains("?"))
                return url + "&" + queryParams;
            return url + "?" + queryParams;
        }
    }
}