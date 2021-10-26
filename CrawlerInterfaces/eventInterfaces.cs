using System.Threading.Tasks;

namespace WebCrawler.CrawlerInterfaces
{
    public interface IUpdateTasks
    {
        /// <summary>
        /// Updates the <see cref=UriStatus> objects
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="uriStatus"></param>
        /// <returns></returns>
        Task AsyncUpdate(UriState uri, UriStatus uriStatus);
    }

    public interface IScheduleUriTasks
    {
        /// <summary>
        /// Schedules the next download from the specified `UriState`
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        Task AsyncSchedule(UriState uri);
    }


    public interface IUpdateUriCacheTasks
    {
        /// <summary>
        /// Updates the URI cache
        /// </summary>
        /// <returns></returns>
        Task AsyncRefreshUriCache();
    }
}
