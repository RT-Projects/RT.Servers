using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RT.Servers
{
    /// <summary>Keeps track of various server performance statistics.</summary>
    public class HttpServerStats
    {
        private long _totalRequestsReceived = 0;
        /// <summary>Gets the total number of requests received by the server.</summary>
        public long TotalRequestsReceived { get { return Interlocked.Read(ref _totalRequestsReceived); } }
        /// <summary>Used internally to count a received request.</summary>
        public void AddRequestReceived() { Interlocked.Increment(ref _totalRequestsReceived); }
        // bytes sent/received
        // max open connections
        // request serve time stats
    }
}
