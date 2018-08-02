using System;
using System.Net;

namespace CSharpRestClient.Exceptions {
    public class RestClientTimeoutException : RestClientException {
        public RestClientTimeoutException(string message, Exception innerException) : base(message, innerException) {
        }

        public RestClientTimeoutException(HttpStatusCode httpStatusCode, string message) : base(httpStatusCode, message) {
        }
    }
}