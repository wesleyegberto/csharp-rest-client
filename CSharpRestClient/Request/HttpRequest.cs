using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpRestClient.Enums;
using CSharpRestClient.Exceptions;
using CSharpRestClient.Interceptors;
using Newtonsoft.Json;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using Polly.Wrap;

namespace CSharpRestClient.Request {
    public class HttpRequest<T> {
        private readonly HttpMethod _httpMethod;
        private readonly string _url;
        private readonly ContentType _contentType;
        private readonly string _payload;
        private readonly Dictionary<string, string> _headers;
        private readonly Dictionary<string, string> _queryParams;
        private readonly TimeSpan? _timeout;

        private int? numberOfRetries;
        Func<CancellationToken, Task<T>> fallbackAction;

        private readonly List<IPayloadInterceptor> _payloadInterceptors;
        private readonly List<IResponseInterceptor> _responseInterceptors;

        private bool _acceptAnyStatusCode;
        private int[] _statusCodesToAccept;

        private HttpRequest(HttpMethod httpMethod, string url, ContentType contentType, string payload,
            Dictionary<string, string> headers, Dictionary<string, string> queryParams, TimeSpan? timeout,
            List<IPayloadInterceptor> payloadInterceptors, List<IResponseInterceptor> responseInterceptors) {
            this._httpMethod = httpMethod;
            this._url = url;
            this._contentType = contentType;
            this._payload = payload;
            this._headers = headers;
            this._queryParams = queryParams;
            this._timeout = timeout;
            this._payloadInterceptors = payloadInterceptors;
            this._responseInterceptors = responseInterceptors;
        }

        public static HttpRequest<T> Create(HttpMethod httpMethod, string url, ContentType contentType, string payload,
            Dictionary<string, string> headers, Dictionary<string, string> queryParams, TimeSpan? timeout,
            List<IPayloadInterceptor> payloadInterceptors = null, List<IResponseInterceptor> responseInterceptors = null) {
            return new HttpRequest<T>(httpMethod, url, contentType, payload, headers, queryParams, timeout, payloadInterceptors, responseInterceptors);
        }

        public static string FormartParamsToUrl(Dictionary<string, string> parameters) {
            if (parameters == null) return null;
            return string.Join("&", parameters.AsEnumerable()
                .Select(entry => $"{entry.Key}={entry.Value}")
                .ToList());
        }

        ///<summary>
        /// Helper method to debug or log in the server.
        ///</summary>
        public HttpRequest<T> LogPayloadToConsole() {
            Console.WriteLine(_payload);
            return this;
        }

        public HttpRequest<T> AcceptAnyStatusCode() {
            this._acceptAnyStatusCode = true;
            return this;
        }

        public HttpRequest<T> AcceptStatusCodes(params int[] statusCodes) {
            this._statusCodesToAccept = statusCodes;
            return this;
        }

        public HttpRequest<T> Retry(int numberOfRetries) {
            this.numberOfRetries = numberOfRetries;
            return this;
        }

        public HttpRequest<T> Fallback(Func<CancellationToken, Task<T>> fallbackAction) {
            this.fallbackAction = fallbackAction;
            return this;
        }

        private string GetUrlWithQueryParams() {
            if (_queryParams == null || _queryParams.Count == 0) return _url;
            var queryParam = FormartParamsToUrl(_queryParams);
            if (_url.Contains("?"))
                return _url + "&" + queryParam;
            return _url + "?" + queryParam;
        }

        private static async Task<string> ExtractResponse(HttpContent content) {
            var responseReader = new StreamReader(await content.ReadAsStreamAsync(), Encoding.UTF8);
            return await responseReader.ReadToEndAsync();
        }

        private bool HasAcceptableStatusCode(HttpResponseMessage httpWebResp) {
            return _statusCodesToAccept != null && _statusCodesToAccept.Contains((int) httpWebResp.StatusCode);
        }

        private bool HasBodyToSend() {
            return _payload != null && (_httpMethod == HttpMethod.Post || _httpMethod == HttpMethod.Put);
        }

        private string GetContentTypeHeaderValue() {
            return this._contentType == ContentType.Application_Json ? "application/json" : "application/x-www-form-urlencoded";
        }

        private StringContent GeneratePayloadContent() {
            if (this._payload != null)
                this._payloadInterceptors.ForEach(i => i.Intercept(_payload));
            return new StringContent(_payload, Encoding.UTF8, GetContentTypeHeaderValue());
        }

        private HttpClient CreateHttpClient() {
            var httpClient = new HttpClient();
            if (_timeout.HasValue)
                httpClient.Timeout = _timeout.Value;

            // Adiciona os Headers
            if (_headers != null && _headers.Count > 0) {
                foreach (var header in _headers) {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(GetContentTypeHeaderValue()));
            return httpClient;
        }

        private void ValidateHttpStatus(HttpResponseMessage response) {
            if (response.IsSuccessStatusCode || _acceptAnyStatusCode || HasAcceptableStatusCode(response)) {
                return;
            }
            switch (response.StatusCode) {
                case HttpStatusCode.NotFound:
                    throw new RestClientException(response.StatusCode, string.Format("URL {0} not found.", _url));
                case HttpStatusCode.BadRequest:
                    throw new RestClientException(response.StatusCode, "Invalid request.");
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.RequestTimeout:
                    throw new RestClientTimeoutException(response.StatusCode, string.Format("Timeout during the request to URL {0}", _url));
                case HttpStatusCode.InternalServerError:
                    throw new RestClientException(response.StatusCode, "An internal error occurred during the request.");
                case HttpStatusCode.ServiceUnavailable:
                    throw new RestClientException(response.StatusCode, "The requested server is unavailable.");
                case HttpStatusCode.BadGateway:
                    throw new RestClientException(response.StatusCode, "Bad gateway.");
                default:
                    throw new RestClientException(response.StatusCode, "An unexpected response was received.");
            }
        }

        private Task<HttpResponseMessage> CreateRequest() {
            var httpClient = CreateHttpClient();
            var requestUri = GetUrlWithQueryParams();

            if (_httpMethod == HttpMethod.Get) {
                return httpClient.GetAsync(requestUri);
            } else if (_httpMethod == HttpMethod.Post) {
                return httpClient.PostAsync(requestUri, GeneratePayloadContent());
            } else if (_httpMethod == HttpMethod.Put) {
                return httpClient.PutAsync(requestUri, GeneratePayloadContent());
            } else if (_httpMethod == HttpMethod.Delete) {
                return httpClient.DeleteAsync(requestUri);
            }
            throw new RestClientException("HTTP method not supported.");
        }

        public async Task<string> ExecuteRequest() {
            try {
                using(var httpResponse = await CreateRequest()) {
                    ValidateHttpStatus(httpResponse);
                    return await ExtractResponse(httpResponse.Content);
                }
            } catch (RestClientException) {
                throw;
            } catch (TaskCanceledException ex) {
                throw new RestClientTimeoutException($"Timeout during the request to URL {_url}.", ex);
            } catch (Exception ex) {
                throw new RestClientException($"An error occurred during the request to URL {_url}.", ex);
            }
        }

        private async Task<T> ExecuteRequestAndParseEntity() {
            var response = await ExecuteRequest();
            if (string.IsNullOrEmpty(response)) {
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(response);
        }

        private PolicyWrap<T> BuildPolicy() {
            Policy<T> fallback = null;
            if (fallbackAction != null) {
                fallback = Policy<T>
                    .Handle<RestClientException>()
                    .FallbackAsync<T>(fallbackAction);
            } else {
                fallback = Policy.NoOpAsync<T>();
            }

            Policy<T> retry = null;
            if (numberOfRetries.HasValue) {
                retry = Policy<T>
                    .Handle<RestClientException>()
                    .RetryAsync(numberOfRetries.Value);
            } else {
                retry = Policy.NoOpAsync<T>();
            }

            var breaker = Policy<T>
                .Handle<RestClientException>()
                .CircuitBreakerAsync(3, TimeSpan.FromSeconds(5));

            return Policy.WrapAsync(fallback, retry, breaker);
        }

        private async Task<T> AsyncRequest() {
            var policyWrap = BuildPolicy();
            var policyResult = await policyWrap
                .ExecuteAndCaptureAsync(ExecuteRequestAndParseEntity);

            if (policyResult.Outcome == OutcomeType.Successful)
                return policyResult.Result;

            throw policyResult.FinalException;
        }

        public async Task<string> GetResponse() {
            return await ExecuteRequest();
        }

        public async Task<T> GetEntity() {
            return await AsyncRequest();
        }
    }
}