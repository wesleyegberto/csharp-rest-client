using System;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
using CSharpRestClient.Test.Models;
using Polly;
using Xunit;

namespace CSharpRestClient.Test {
    public class PollyTest {
        private const string BASE_URL = "http://httpbin.org";

        [Fact]
        public void TestPolly() {
            var policy = Policy
                .Handle<Exception>()
                .Retry(2, (exception, retryCount, context) => {
                    Console.WriteLine("Exception thrown: " + retryCount);
                })
                .ExecuteAndCapture(() => {
                    return 42;
                    throw new Exception("Exception to capture");
                });

            Assert.Null(policy.FinalException);
            Assert.Equal(42, policy.Result);
        }

        public ModelWithGuid fallback() {
            return new ModelWithGuid() {
                Uuid = Guid.Parse("b6fca409-12fa-4b09-8c67-29699e286871")
            };
        }

        [Fact]
        public async Task Should_Request_Toxy_Server_With_Policy() {
            var model = await HttpClientBuilder.Create("http://invalid.url")
                .Path("uuid")
                .AsyncGet<ModelWithGuid>()
                .Retry(2)
                .Fallback(async can => fallback())
                .GetEntity();

            Assert.NotNull(model);
            Assert.Equal(Guid.Parse("b6fca409-12fa-4b09-8c67-29699e286871"), model.Uuid);
        }
    }
}