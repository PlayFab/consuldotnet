using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public interface IOperatorEndpoint
    {
        Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(CancellationToken ct = default(CancellationToken));
        Task<QueryResult<RaftConfiguration>> RaftGetConfiguration(QueryOptions q, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> RaftRemovePeerByAddress(string address, CancellationToken ct = default(CancellationToken));
        Task<WriteResult> RaftRemovePeerByAddress(string address, WriteOptions q, CancellationToken ct = default(CancellationToken));
    }
}
