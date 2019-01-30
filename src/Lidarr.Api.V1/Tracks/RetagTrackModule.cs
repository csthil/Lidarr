using System.Collections.Generic;
using System.Linq;
using Lidarr.Http;
using Lidarr.Http.REST;
using NzbDrone.Core.MediaFiles;

namespace Lidarr.Api.V1.Tracks
{
    public class RetagTrackModule : LidarrRestModule<RetagTrackResource>
    {
        private readonly IAudioTagService _audioTagService;

        public RetagTrackModule(IAudioTagService audioTagService)
            : base("retag")
        {
            _audioTagService = audioTagService;

            GetResourceAll = GetTracks;
        }

        private List<RetagTrackResource> GetTracks()
        {
            int artistId;

            if (Request.Query.ArtistId.HasValue)
            {
                artistId = (int)Request.Query.ArtistId;
            }

            else
            {
                throw new BadRequestException("artistId is missing");
            }

            if (Request.Query.albumId.HasValue)
            {
                var albumId = (int)Request.Query.albumId;
                return _audioTagService.GetRenamePreviews(artistId, albumId).Where(x => x.Changes.Any()).ToResource();
            }

            return _audioTagService.GetRenamePreviews(artistId).ToResource();
        }
    }
}
