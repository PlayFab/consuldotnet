using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Consul
{
    internal class TxnOp
    {
        public KVTxnOp KV { get; set; }
    }

    internal class TxnResult
    {
        public KVPair KV { get; set; }
    }
   
    public class TxnError
    {
        [JsonProperty]
        public int OpIndex { get; private set; }
        [JsonProperty]
        public string What { get; private set; }
    }

    internal class TxnResponse
    {
        [JsonProperty]
        internal List<TxnResult> Results { get; set; }
        [JsonProperty]
        internal List<TxnError> Errors { get; set; }
    }
}
