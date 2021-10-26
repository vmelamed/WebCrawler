using System;
using System.Collections.Generic;

namespace WebCrawler.CrawlerInterfaces
{
    public enum UriStatus
    {
        /// <summary>
        /// The Uri is created and is in the crawler.
        /// </summary>
        Created,

        /// <summary>
        /// The URI must not be downloaded due to some policy, need for authorization (401), etc.
        /// </summary>
        Suspended,

        /// <summary>
        /// The time of the next download from the URI has been set. The URI is updated in the URI, is in
        /// the Activator cache (e.g. Redis queue), or being moved by the Activator from the cache to the download
        /// queue.
        /// </summary>
        Scheduled,

        /// <summary>
        /// The URI is being downloaded.
        /// </summary>
        Downloading,

        /// <summary>
        /// The download failed and the failure is in <see cref="UriState.LastHttpStatus">
        /// </summary>
        DownloadFailed,

        /// <summary>
        /// The downloaded document is being parsed into three streams: stream of tokens to be stored and the staging
        /// area; stream of URI-s; stream of policy updating events (e.g. from `robot.txt`)
        /// </summary>
        Parsing,

        /// The document has been parsed and is ready to be re-scheduled.
        Parsed,
    }

    public class UriState
    {
        public UriState(string url)
        {
            Uri = new Uri(url);
            DateTime now = DateTime.UtcNow;
            Created = now;
            Updated = now;
            DownloadAt = now;
            HostIpAddress = "";
            Cluster = "";
        }

        /// <summary>
        /// The URI managed by the crawler
        /// </summary>
        public Uri Uri { get; init; }

        /// <summary>
        /// The time when the URI enter the system (seeded or referenced)
        /// </summary>
        public DateTime Created { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// The time when the state of the URI was last updated
        /// </summary>
        public DateTime Updated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The DNS resolved IP address of the host (site)
        /// </summary>
        public string HostIpAddress { get; set; }

        /// <summary>
        /// The local cluster assigned that this URI is assigned to
        /// </summary>
        public string Cluster { get; set; }

        // where in the overall workflow the URI is at
        public UriStatus Status { get; set; }

        /// <summary>
        /// When the next download should start.
        /// </summary>
        public DateTime DownloadAt { get; set; }

        /// <summary>
        /// The URI referring to this URI. This member is valid only when this object is created by the
        /// <see cref="DocumentParser"/> and is intended for use by the analytical subsystem
        /// </summary>
        public Uri? ReferencedBy { get; set; }

        /// <summary>
        /// When did the last download started/finished may be useful to the analytical subsystem for evaluating the
        /// site's bandwidth and have impact on the scheduling criteria
        /// </summary>
        public DateTime DownloadStarted { get; set; }
        public DateTime DownloadFinished { get; set; }

        /// <summary>
        /// The HTTP status from the last download.
        /// </summary>
        public int LastHttpStatus { get; set; }

        /// <summary>
        /// The document hash can be useful to skip the indexing process if the content of the document is the same.
        /// </summary>
        public byte[]? DocumentHash { get; set; }

        /// <summary>
        /// The headers from the last download may be usefull to the document parser, and the UriDispatcher.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}
