namespace CSharpRestClient.Interceptors {
    public interface IPayloadInterceptor {
        void Intercept(string payload);
    }
}