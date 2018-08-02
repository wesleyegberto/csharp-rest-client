using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CSharpRestClient.Options {
    public class SerializerOption {
        ///<summary>
        ///  Default settings to request to .Net-based REST.
        ///</summary>
        public static readonly JsonSerializerSettings DOTNET_CAMELCASE = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            NullValueHandling = NullValueHandling.Ignore
        };

        ///<summary>
        ///  Default settings to request to Java-based REST.
        ///  It serialize DateTime using Java data format.
        ///</summary>
        public static readonly JsonSerializerSettings JAVA_CAMELCASE = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-ddTHH:mm:ss"
        };
    }
}