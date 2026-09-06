using NzbDrone.Core.Cache;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupOldDownloadCacheEntries(IDownloadCacheService downloadCacheService,
                                                QueryCacheService queryCacheService) : IHousekeepingTask
    {
        public void Clean()
        {
            if (downloadCacheService.IsEnabled)
            {
                downloadCacheService.Cleanup();
            }

            queryCacheService.Cleanup();
        }
    }
}
