using System.Net;

namespace CSharpRestClient.Interceptors {
    public interface IResponseInterceptor {
        void Intercept(HttpStatusCode statusCode, string response);
    }
}