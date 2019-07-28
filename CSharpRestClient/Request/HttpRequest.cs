using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
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
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly HttpRequestMessage _httpRequestMessage;
        private readonly HttpMethod _httpMethod;
        private readonly ContentType _contentType;
        private readonly string _payload;
        private readonly TimeSpan? _timeout;

        private int? numberOfRetries;
        Func<CancellationToken, Task<T>> fallbackAction;

        private readonly List<IPayloadInterceptor> _payloadInterceptors;
        private readonly List<IResponseInterceptor> _responseInterceptors;

        private bool _acceptAnyStatusCode;
        private int[] _statusCodesToAccept;

        private string _tracingId = DateTime.Now.Ticks.ToString();
        private bool _logResponse;

        public string Url {
            get {
                return _httpRequestMessage.RequestUri.AbsoluteUri;
            }
        }

        private bool HasBodyToSend {
            get {
                return _payload != null && (_httpMethod == HttpMethod.Post || _httpMethod == HttpMethod.Put);
            }
        }

        private HttpRequest(HttpMethod httpMethod, string requestUri, ContentType contentType, string payload,
            Dictionary<string, string> headers, TimeSpan? timeout,
            List<IPayloadInterceptor> payloadInterceptors, List<IResponseInterceptor> responseInterceptors) {
            this._httpMethod = httpMethod;
            this._httpRequestMessage = CreateRequestMessage(httpMethod, requestUri, headers, payload);
            this._contentType = contentType;
            this._payload = payload;
            this._timeout = timeout;
            this._payloadInterceptors = payloadInterceptors;
            this._responseInterceptors = responseInterceptors;
        }

        public static HttpRequest<T> Create(HttpMethod httpMethod, string requestUri, ContentType contentType, string payload,
            Dictionary<string, string> headers, TimeSpan? timeout,
            List<IPayloadInterceptor> payloadInterceptors = null, List<IResponseInterceptor> responseInterceptors = null) {
            return new HttpRequest<T>(httpMethod, requestUri, contentType, payload, headers, timeout, payloadInterceptors, responseInterceptors);
        }

        private static HttpRequestMessage CreateRequestMessage(HttpMethod httpMethod, string requestUri,
                Dictionary<string, string> headers, string payload) {
            var httpRequestMessage = new HttpRequestMessage(httpMethod, requestUri);
            if (headers != null && headers.Count > 0) {
                foreach (var header in headers) {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }
            }
            return httpRequestMessage;
        }

        ///<summary>
        /// Set the tracing ID to help trace request and logs.
        ///</summary>
        public HttpRequest<T> TracingId(string tracingId) {
            this._tracingId = tracingId;
            return this;
        }

        ///<summary>
        /// Helper method to debug/log the request payload.
        ///</summary>
        public HttpRequest<T> LogPayload() {
            Console.WriteLine($"Ctx ID: {_tracingId} - URL: {Url} - Payload: {_payload}");
            return this;
        }

        ///<summary>
        /// Helper method to debug/log the response.
        ///</summary>
        public HttpRequest<T> LogResponse() {
            this._logResponse = true;
            return this;
        }

        private void LogResponse(HttpResponseMessage httpResponse, string response) {
            Console.WriteLine($"Ctx ID: {_tracingId} - Status: {httpResponse.StatusCode} - Response: {response}");
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

        private static async Task<string> ExtractResponse(HttpContent content) {
            var responseReader = new StreamReader(await content.ReadAsStreamAsync(), Encoding.UTF8);
            return await responseReader.ReadToEndAsync();
        }

        private bool HasAcceptableStatusCode(HttpResponseMessage httpWebResp) {
            return _statusCodesToAccept != null && _statusCodesToAccept.Contains((int) httpWebResp.StatusCode);
        }

        private string GetContentTypeHeaderValue() {
            return this._contentType == ContentType.Application_Json ? "application/json" : "application/x-www-form-urlencoded";
        }

        private StringContent GeneratePayloadContent() {
            if (this._payload != null)
                this._payloadInterceptors.ForEach(i => i.Intercept(_payload));
            return new StringContent(_payload, Encoding.UTF8, GetContentTypeHeaderValue());
        }
        
        private static async Task<string> ExtractResponseFromStream(Task<Stream> content) {
            var responseReader = new StreamReader(await content, Encoding.UTF8);
            return await responseReader.ReadToEndAsync();
        }

        private async Task<string> ExtractResponse(HttpResponseMessage httpResponse) {
            if (httpResponse == null || httpResponse.Content == null) return null;
            var response = await ExtractResponseFromStream(httpResponse.Content.ReadAsStreamAsync());
            if (this._logResponse) {
                LogResponse(httpResponse, response);
            }
            return response;
        }

        private async Task<string> ExecuteRequest() {
            if (HasBodyToSend) {
                _httpRequestMessage.Content = GeneratePayloadContent();
            }

            if (_timeout.HasValue)
                _httpClient.Timeout = _timeout.Value;
            
            try {
                using(var httpResponse = await _httpClient.SendAsync(_httpRequestMessage)) {
                    var response = await ExtractResponse(httpResponse);
                    ValidateExpectedHttpStatus(httpResponse);
                    return response;
                }
            } catch (RestClientException) {
                throw;
            } catch (TaskCanceledException ex) {
                throw new RestClientTimeoutException($"Timeout during the request to URL {Url}.", ex);
            } catch (Exception ex) {
                throw new RestClientException($"An error occurred during the request to URL {Url}.", ex);
            }
        }

        private void ValidateExpectedHttpStatus(HttpResponseMessage response) {
            if (response.IsSuccessStatusCode || _acceptAnyStatusCode || HasAcceptableStatusCode(response)) {
                return;
            }
            switch (response.StatusCode) {
                case HttpStatusCode.NotFound:
                    throw new RestClientException(response.StatusCode, $"URL {Url} not found.");
                case HttpStatusCode.BadRequest:
                    throw new RestClientException(response.StatusCode, "Invalid request.");
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.RequestTimeout:
                    throw new RestClientTimeoutException(response.StatusCode, $"Timeout during the request to URL {Url}");
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