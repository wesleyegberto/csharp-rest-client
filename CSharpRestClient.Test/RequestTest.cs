using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CSharpRestClient.Builder;
using CSharpRestClient.Test.Models;
using Xunit;

namespace CSharpRestClient.Test
{
    public class RequestTest {
        private const string BASE_URL = "http://httpbin.org";

        [Fact]
        public void Should_Format_Url_With_Query_Params() {
            var queryParam = new Dictionary<string, string>();
            queryParam.Add("q", "languages");
            queryParam.Add("lang", "en");
            var queryParams = UrlHelper.FormatQueryParamsToUrl(queryParam);

            Assert.Equal("q=languages&lang=en", queryParams);
        }

        [Fact]
        public async Task Should_Request_And_Deserialize_Model_With_Guid() {
            var model = await HttpClientBuilder.Create(BASE_URL)
                .Path("uuid")
                .AsyncGet<ModelWithGuid>()
                .GetEntity();

            Assert.NotNull(model);
            Assert.NotEqual(Guid.Empty, model.Uuid);
        }

        [Fact]
        public async Task Should_Make_Get_Request_With_Query_And_Headers_Params() {
            var getModel = await HttpClientBuilder.Create(BASE_URL)
                .Path("anything")
                .Query("q", "Test")
                .Query("v", 1)
                .Header("X-Pwd", "x-123")
                .AsyncGet<HttpBinGetModel>()
                .GetEntity();

            Assert.NotNull(getModel);
            Assert.Equal("https://httpbin.org/anything?q=Test&v=1", getModel.Url);

            Assert.NotNull(getModel.Args);
            Assert.Equal("Test", getModel.Args["q"]);
            Assert.Equal("1", getModel.Args["v"]);
    
            Assert.NotNull(getModel.Headers);
            Assert.Equal("x-123", getModel.Headers["X-Pwd"]);
        }
    }
}