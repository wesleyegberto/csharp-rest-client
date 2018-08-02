using System.Collections.Generic;

namespace CSharpRestClient.Test.Models {
    public class HttpBinGetModel {
        public string Url { get; set; }
        public Dictionary<string, string> Args { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}