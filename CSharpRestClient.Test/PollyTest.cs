using System;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
using CSharpRestClient.Test.Models;
using Polly;
using Xunit;

namespace CSharpRestClient.Test {
    public class PollyTest {
        [Fact]
        public void TestPolly() {
            var policy = Policy
                .Handle<Exception>()
                .Retry(2, (exception, retryCount, context) => {
                    Console.WriteLine("Exception thrown: " + retryCount);
                })
                .ExecuteAndCapture(() => {
                    throw new Exception("Exception to capture");
                    return 42;
                });

            Assert.Null(policy.FinalException);
            Assert.Equal(10, policy.Result);
        }

        public ModelWithGuid fallback() {
            return new ModelWithGuid() {
                Uuid = Guid.Empty
            };
        }

        [Fact]
        public async Task Should_Request_Toxy_Server_With_Policy() {
            var model = await HttpClientBuilder.Create("http://localhost:3000/uuid")
                .AsyncGet<ModelWithGuid>()
                .Retry(2)
                .Fallback(async can => fallback())
                .GetEntity();

            Assert.NotNull(model);
            Assert.NotEqual(Guid.Empty, model.Uuid);
        }
    }
}