using System;
using System.Collections;
using System.Threading.Tasks;

namespace WebCrawler.CrawlerInterfaces
{
    /// <summary>
    /// Interface for managing and tracking individual URI-s
    /// </summary>
    public interface IMangeUriTasks
    {
        /// <summary>
        /// Creates a new URI to be managed by the crawler
        /// </summary>
        /// <param name="uri">The URI</param>
        Task AsyncCreateUri(Uri uri, string uriHostIPAddress = "");

        /// <summary>
        ///
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task<UriState> GetUri(Uri uri);

        /// <summary>
        /// Sets the status of an URI to <see cref"UriStatus.Suspended"> or <see cref"UriStatus.Created">
        /// </summary>
        /// <param name="uri">The URI which status should be set to set the status</param>
        /// <returns></returns>
        Task DownloadUri(Uri uri, bool download);

        /// <summary>
        /// Remove a URI from the crawler
        /// </summary>
        /// <param name="uri">the URI to remove</param>
        Task AsyncRemoveUri(Uri uri);

        /// <summary>
        /// Remove a whole site (set of URI-s with the same host) from the crawler.
        /// </summary>
        /// <param name="uri">the site's host address</param>
        Task AsyncRemoveSite(Uri uri);
    }

    /// <summary>
    /// This interface should serve mostly UI needs to list URI-s subject to various criteria.
    /// ADMITTEDLY, this interface is a little bit of a handwaiving and should be extended or at least the method
    /// <see cref="GetUris"> should have reacher set of criteria.
    /// Possible criteria: filter(s), sorting, grouping.
    /// </summary>
    public interface IListUriTasks
    {
        /// <summary>
        /// This method gets a list of URI states.
        /// </summary>
        /// <param name="site">This is the site to list. This parameter MUST be revised to allow for a rich set of
        /// criteria: filter(s), sorting, possibly even grouping.
        /// </param>
        /// <param name="skip">where to start the current "page"</param>
        /// <param name="take">how long the current page should be</param>
        /// <returns>List of <see cref="UriState"></returns>
        Task<(IList list, long totalLength)> AsyncGetUris(string site, int skip = 0, int take = 100);
    }
}
