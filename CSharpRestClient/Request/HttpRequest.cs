using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
using CSharpRestClient.Enums;
using CSharpRestClient.Exceptions;
using CSharpRestClient.Interceptors;
using Newtonsoft.Json;

namespace CSharpRestClient.Request {
    public class HttpRequest {
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly HttpRequestMessage _httpRequestMessage;
        private readonly ContentType _contentType;
        private readonly string _payload;
        private readonly TimeSpan? _timeout;

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
                return _payload != null && (_httpRequestMessage.Method == HttpMethod.Post || _httpRequestMessage.Method == HttpMethod.Put);
            }
        }

        private HttpRequest(IHttpClientFactory httpClientFactory, HttpMethod httpMethod, string requestUri,
                ContentType contentType, string payload, Dictionary<string, string> headers, TimeSpan? timeout,
                List<IPayloadInterceptor> payloadInterceptors, List<IResponseInterceptor> responseInterceptors) {
            this._httpClientFactory = httpClientFactory;
            this._httpRequestMessage = CreateRequestMessage(httpMethod, requestUri, headers);
            this._contentType = contentType;
            this._payload = payload;
            this._timeout = timeout;
            this._payloadInterceptors = payloadInterceptors;
            this._responseInterceptors = responseInterceptors;
        }

        public static HttpRequest Create(IHttpClientFactory httpClientFactory, HttpMethod httpMethod, string requestUri,
                ContentType contentType, string payload, Dictionary<string, string> headers, TimeSpan? timeout,
                List<IPayloadInterceptor> payloadInterceptors = null, List<IResponseInterceptor> responseInterceptors = null) {
            return new HttpRequest(httpClientFactory, httpMethod, requestUri, contentType, payload, headers, timeout, payloadInterceptors, responseInterceptors);
        }

        private static HttpRequestMessage CreateRequestMessage(HttpMethod httpMethod, string requestUri,
                Dictionary<string, string> headers) {
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
        public HttpRequest TracingId(string tracingId) {
            this._tracingId = tracingId;
            return this;
        }

        ///<summary>
        /// Helper method to debug/log the request payload.
        ///</summary>
        public HttpRequest LogPayload() {
            Console.WriteLine($"Ctx ID: {_tracingId} - URL: {Url} - Payload: {_payload}");
            return this;
        }

        ///<summary>
        /// Helper method to debug/log the response.
        ///</summary>
        public HttpRequest LogResponse() {
            this._logResponse = true;
            return this;
        }

        private void LogResponse(HttpResponseMessage httpResponse, string response) {
            Console.WriteLine($"Ctx ID: {_tracingId} - Status: {httpResponse.StatusCode} - Response: {response}");
        }

        public HttpRequest AcceptAnyStatusCode() {
            this._acceptAnyStatusCode = true;
            return this;
        }

        public HttpRequest AcceptStatusCodes(params int[] statusCodes) {
            this._statusCodesToAccept = statusCodes;
            return this;
        }

        private static async Task<string> ExtractResponseFromStream(Task<Stream> content) {
            var responseReader = new StreamReader(await content, Encoding.UTF8);
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

        private async Task<string> ExtractResponse(HttpResponseMessage httpResponse) {
            if (httpResponse == null || httpResponse.Content == null) return null;
            var response = await ExtractResponseFromStream(httpResponse.Content.ReadAsStreamAsync());
            if (this._logResponse) {
                LogResponse(httpResponse, response);
            }
            return response;
        }

        private HttpClient CreateHttpClient() {
            if (_httpClientFactory == null) {
                return new HttpClient();
            }
            return _httpClientFactory.CreateClient();
        }

        private async Task<string> AsyncRequest() {
            if (HasBodyToSend) {
                _httpRequestMessage.Content = GeneratePayloadContent();
            }

            var httpClient = CreateHttpClient();

            if (_timeout.HasValue)
                httpClient.Timeout = _timeout.Value;
            
            try {
                using(var httpResponse = await httpClient.SendAsync(_httpRequestMessage)) {
                    var response = await ExtractResponse(httpResponse);
                    ValidateExpectedHttpStatus(httpResponse);
                    return response;
                }
            } catch (RestClientException) {
                throw;
            } catch (OperationCanceledException ex) {
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
                default:
                    throw new RestClientException(response.StatusCode, "An unexpected response was received.");
            }
        }

        public async Task<string> GetResponse() {
            return await AsyncRequest();
        }

        public async Task<T> GetEntity<T>() {
            var response = await GetResponse();
            if (string.IsNullOrEmpty(response)) {
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(response);
        }
    }
}