using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ProviderAddedEvent<IDownloadClient>))]
    [CheckOn(typeof(ProviderUpdatedEvent<IDownloadClient>))]
    [CheckOn(typeof(ProviderDeletedEvent<IDownloadClient>))]
    [CheckOn(typeof(ModelEvent<RemotePathMapping>))]
    [CheckOn(typeof(TrackImportedEvent), CheckOnCondition.FailedOnly)]
    [CheckOn(typeof(TrackImportFailedEvent), CheckOnCondition.SuccessfulOnly)]
    public class RemotePathMappingCheck : HealthCheckBase, IProvideHealthCheckWithMessage
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly Logger _logger;

        public RemotePathMappingCheck(IDiskProvider diskProvider,
                                      IProvideDownloadClient downloadClientProvider,
                                      Logger logger)
        {
            _diskProvider = diskProvider;
            _downloadClientProvider = downloadClientProvider;
            _logger = logger;
        }

        public override HealthCheck Check()
        {
            var clients = _downloadClientProvider.GetDownloadClients();

            foreach (var client in clients)
            {
                var folders = client.GetStatus().OutputRootFolders;
                if (folders != null)
                {
                    foreach (var folder in folders)
                    {
                        if (!_diskProvider.FolderExists(folder.FullPath))
                        {
                            if (OsInfo.IsDocker)
                            {
                                return new HealthCheck(GetType(), HealthCheckResult.Error, $"You are using docker; download client {client.Definition.Name} places downloads in {folder.FullPath} but this directory does not appear to exist inside the container.  Review your remote path mappings and container volume settings.", "#docker-bad-remote-path-mapping");
                            }
                            else if (!client.GetStatus().IsLocalhost)
                            {
                                return new HealthCheck(GetType(), HealthCheckResult.Error, $"Remote download client {client.Definition.Name} places downloads in {folder.FullPath} but this directory does not appear to exist.  Likely missing or incorrect remote path mapping.", "#bad-remote-path-mapping");
                            }
                            else
                            {
                                return new HealthCheck(GetType(), HealthCheckResult.Error, $"Download client {client.Definition.Name} places downloads in {folder.FullPath} but Lidarr cannot see this directory.  Check that the user running Lidarr has the necessary permissions.", "#permissions-error");
                            }
                        }
                    }
                }
            }
            return new HealthCheck(GetType());
        }

        public HealthCheck Check(IEvent message)
        {
            if (typeof(TrackImportFailedEvent).IsAssignableFrom(message.GetType()))
            {
                var failureMessage = (TrackImportFailedEvent) message;
                var client = _downloadClientProvider.GetDownloadClients().First(x => x.Definition.Name == failureMessage.DownloadClient);
                var item = client.GetItems().First(x => x.DownloadId == failureMessage.DownloadId);

                // if we can see the file exists but the import failed then likely a permissions issue
                if (failureMessage.TrackInfo != null)
                {
                    var trackPath = failureMessage.TrackInfo.Path;
                    if (_diskProvider.FileExists(trackPath))
                    {
                        return new HealthCheck(GetType(), HealthCheckResult.Error, $"Lidarr can see but not access downloaded track {trackPath}.  Likely permissions error.", "#permissions-error");
                    }
                }

                var dlpath = item.OutputPath.FullPath;
                if (_diskProvider.FolderExists(dlpath))
                {
                    return new HealthCheck(GetType(), HealthCheckResult.Error, $"Lidarr can see but not access download directory {dlpath}.  Likely permissions error.", "#permissions-error");
                }
                
                // if it's a remote client/docker, likely missing path mappings
                if (OsInfo.IsDocker)
                {
                    return new HealthCheck(GetType(), HealthCheckResult.Error, $"You are using docker; download client {client.Definition.Name} reported files in {dlpath} but this directory does not appear to exist inside the container.  Review your remote path mappings and container volume settings.", "#docker-bad-remote-path-mapping");
                }
                else if (!client.GetStatus().IsLocalhost)
                {
                    return new HealthCheck(GetType(), HealthCheckResult.Error, $"Remote download client {client.Definition.Name} reported files in {dlpath} but this directory does not appear to exist.  Likely missing remote path mapping.", "#bad-remote-path-mapping");
                }
                else
                {
                    // path mappings shouldn't be needed locally so probably a permissions issue
                    return new HealthCheck(GetType(), HealthCheckResult.Error, $"Download client {client.Definition.Name} reported files in {dlpath} but Lidarr cannot see this directory.  Check the user running Lidarr has the necessary permissions.", "#permissions-error");
                }
            }
            else
            {
                return Check();
            }
        }
    }
}
