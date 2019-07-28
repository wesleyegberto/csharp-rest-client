using System;
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
    }
}