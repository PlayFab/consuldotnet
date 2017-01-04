using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consul
{
    public class Snapshot : ISnapshotEndpoint
    {
        private readonly ConsulClient _client;

        /// <summary>
        /// Snapshot can be used to query the /v1/snapshot endpoint to take snapshots of
        /// Consul's internal state and restore snapshots for disaster recovery.
        /// </summary>
        /// <param name="c"></param>
        internal Snapshot(ConsulClient c)
        {
            _client = c;
        }

        public Task<WriteResult> Restore(Stream s, CancellationToken ct = default(CancellationToken))
        {
            return Restore(s, WriteOptions.Default, ct);
        }

        public Task<WriteResult> Restore(Stream s, WriteOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Put("/v1/snapshot", s, q).Execute(ct);
        }

        public Task<QueryResult<Stream>> Save(CancellationToken ct = default(CancellationToken))
        {
            return Save(QueryOptions.Default, ct);
        }

        public Task<QueryResult<Stream>> Save(QueryOptions q, CancellationToken ct = default(CancellationToken))
        {
            return _client.Get<Stream>("/v1/snapshot", q).ExecuteStreaming(ct);
        }
    }

    public partial class ConsulClient : IConsulClient
    {
        private Lazy<Snapshot> _snapshot;

        /// <summary>
        /// Catalog returns a handle to the snapshot endpoints
        /// </summary>
        public ISnapshotEndpoint Snapshot
        {
            get { return _snapshot.Value; }
        }
    }
}
