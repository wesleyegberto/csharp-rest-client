using System.Net;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
using CSharpRestClient.Exceptions;
using Xunit;

namespace CSharpRestClient.Test {
    public class ResponseStatusTest {
        private const string BASE_URL = "http://httpbin.org";

        [Theory]
        [InlineData(200)]
        [InlineData(201)]
        [InlineData(203)]
        [InlineData(204)]
        [InlineData(205)]
        public async Task Should_Accept_Successful_Http_Status(int statusCode) {
            await HttpClientBuilder.Create(BASE_URL)
                .Path("status").Path(statusCode)
                .AsyncGet()
                .GetResponse();
        }

        [Fact]
        public async Task Should_Throw_Timeout_Error() {
            var requestTask = HttpClientBuilder.Create(BASE_URL)
                .Path("status").Path(200)
                .Timeout(10)
                .AsyncGet()
                .GetResponse();
            
            var exception = await Assert.ThrowsAnyAsync<RestClientTimeoutException>(() => requestTask);
            Assert.True(exception is RestClientTimeoutException);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(405)]
        [InlineData(408)]
        [InlineData(409)]
        [InlineData(415)]
        [InlineData(422)]
        [InlineData(500)]
        [InlineData(501)]
        public async Task Should_Accept_Defined_Http_Status(int statusCode) {
            var response = await HttpClientBuilder.Create(BASE_URL)
                .Path("status").Path(statusCode)
                .AsyncGet()
                .AcceptStatusCodes(statusCode)
                .GetResponse();
        }

        [Theory]
        [InlineData(400, HttpStatusCode.BadRequest)]
        [InlineData(401, HttpStatusCode.Unauthorized)]
        [InlineData(403, HttpStatusCode.Forbidden)]
        [InlineData(404, HttpStatusCode.NotFound)]
        [InlineData(405, HttpStatusCode.MethodNotAllowed)]
        [InlineData(408, HttpStatusCode.RequestTimeout)]
        [InlineData(409, HttpStatusCode.Conflict)]
        [InlineData(415, HttpStatusCode.UnsupportedMediaType)]
        [InlineData(500, HttpStatusCode.InternalServerError)]
        [InlineData(501, HttpStatusCode.NotImplemented)]
        [InlineData(502, HttpStatusCode.BadGateway)]
        public async Task Should_Throw_Error_With_Status_Code_When_Response_Isnt_Ok(int statusCode, HttpStatusCode httpStatusCode) {
            var requestTask = HttpClientBuilder.Create(BASE_URL)
                .Path("status").Path(statusCode)
                .AsyncGet()
                .GetResponse();

            var exception = await Assert.ThrowsAnyAsync<RestClientException>(() => requestTask);
            Assert.True(exception is RestClientException);
            Assert.Equal(httpStatusCode, (exception as RestClientException).HttpStatusCode);
        }
    }
}