using System;
using System.Net;

namespace CSharpRestClient.Exceptions {
    public class RestClientException : Exception {
        public HttpStatusCode HttpStatusCode { get; }
        
        public RestClientException(string message, Exception innerException) : base(message, innerException) {
        }

        public RestClientException(HttpStatusCode httpStatusCode, string message) : base(message) {
            this.HttpStatusCode = httpStatusCode;
        }
    }
}