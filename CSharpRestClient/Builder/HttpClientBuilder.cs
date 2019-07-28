using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using CSharpRestClient.Enums;
using CSharpRestClient.Interceptors;
using CSharpRestClient.Options;
using CSharpRestClient.Request;
using Newtonsoft.Json;

namespace CSharpRestClient.Builder {
    public class HttpClientBuilder {
        private StringBuilder _urlBuilder;
        private ContentType _contentType = ContentType.Application_Json;
        private string _payload;
        private Dictionary<string, string> _headers;
        private StringBuilder _formattedQueryParams = new StringBuilder();
        private TimeSpan? _timeout = default(TimeSpan?);

        private List<IPayloadInterceptor> _payloadInterceptors;
        private List<IResponseInterceptor> _responseInterceptors;

        private HttpClientBuilder(string baseUrl) {
            this._urlBuilder = new StringBuilder(baseUrl);
        }

        public static HttpClientBuilder Create(string baseUrl) {
            return new HttpClientBuilder(baseUrl);
        }

        public HttpClientBuilder Path(string path) {
            this._urlBuilder.Append("/").Append(path);
            return this;
        }

        public HttpClientBuilder Path(int path) {
            this._urlBuilder.Append("/").Append(path);
            return this;
        }

        public HttpClientBuilder Path(Guid path) {
            return Path(path.ToString());
        }

        public HttpClientBuilder NoTimeout() {
            this._timeout = System.Threading.Timeout.InfiniteTimeSpan;
            return this;
        }

        public HttpClientBuilder Timeout(TimeSpan timeout) {
            this._timeout = timeout;
            return this;
        }

        public HttpClientBuilder Timeout(int timeoutMs) {
            if (timeoutMs <= 0) throw new ArgumentException("Timeout must be greater than zero.");
            this._timeout = TimeSpan.FromMilliseconds(timeoutMs);
            return this;
        }

        public string GetFormatQueryParams() {
            if (_formattedQueryParams.Length == 0)
                return String.Empty;
            // remove the last '&'
            return _formattedQueryParams.ToString(0, _formattedQueryParams.Length - 1);
        }

        public HttpClientBuilder Query(string key, string value) {
            this._formattedQueryParams.Append(key)
                .Append("=")
                .Append(value)
                .Append("&");
            return this;
        }

        public HttpClientBuilder Query(string key, object value) {
            return Query(key, value == null ? null : value.ToString());
        }

        public HttpClientBuilder Query(string key, Guid value) {
            return Query(key, value.ToString());
        }

        public HttpClientBuilder Header(string key, string value) {
            if (this._headers == null) {
                this._headers = new Dictionary<string, string>();
            }
            this._headers.Add(key, value);
            return this;
        }

        public HttpClientBuilder Header(string key, Guid value) {
            return Header(key, value.ToString());
        }

        public HttpClientBuilder AddBasicAuth(string username, string password) {
            return Header("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        }

        public HttpClientBuilder UseApplicationJson() {
            this._contentType = ContentType.Application_Json;
            return this;
        }

        public HttpClientBuilder UseFormUrlEncoded() {
            this._contentType = ContentType.Form_UrlEncoded;
            return this;
        }

        public HttpClientBuilder Payload(string payload) {
            this._payload = payload;
            return this;
        }

        public HttpClientBuilder AddFormParam(string name, string value) {
            if (this._payload == null)
                this._payload = $"{name}={value}";
            else
                this._payload = $"{_payload}&{name}={value}";
            return this;
        }

        public HttpClientBuilder Entity<T>(T entity) {
            this._payload = JsonConvert.SerializeObject(entity, SerializerOption.DOTNET_CAMELCASE);
            return this;
        }

        public HttpClientBuilder Entity<T>(T entity, JsonSerializerSettings jsonSettings) {
            this._payload = JsonConvert.SerializeObject(entity, jsonSettings);
            return this;
        }

        public HttpClientBuilder RegisterInterceptor(IPayloadInterceptor interceptor) {
            if (_payloadInterceptors == null) {
                _payloadInterceptors = new List<IPayloadInterceptor>();
            }
            _payloadInterceptors.Add(interceptor);
            return this;
        }

        public HttpClientBuilder RegisterInterceptor(IResponseInterceptor interceptor) {
            if (_responseInterceptors == null) {
                _responseInterceptors = new List<IResponseInterceptor>();
            }
            _responseInterceptors.Add(interceptor);
            return this;
        }

        private HttpRequest<T> CreateHttpRequest<T>(HttpMethod httpMethod) {
            var requestUri = UrlHelper.GetUrlWithQueryParams(_urlBuilder.ToString(), GetFormatQueryParams());
            return HttpRequest<T>.Create(httpMethod, requestUri, _contentType, _payload, _headers, _timeout,
                _payloadInterceptors, _responseInterceptors);
        }

        public HttpRequest<T> AsyncGet<T>() => CreateHttpRequest<T>(HttpMethod.Get);

        public HttpRequest<T> AsyncPost<T>() => CreateHttpRequest<T>(HttpMethod.Post);

        public HttpRequest<T> AsyncPut<T>() => CreateHttpRequest<T>(HttpMethod.Put);

        public HttpRequest<T> AsyncDelete<T>() => CreateHttpRequest<T>(HttpMethod.Delete);
    }
}