using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Languages;
using System.Linq;
using System.Collections.Generic;
using System;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Parser;
using NzbDrone.Common.Instrumentation;
using NLog;
using TagLib;
using TagLib.Id3v2;

namespace NzbDrone.Core.MediaFiles
{
    public class AudioTag
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(AudioTag));

        public string Title { get; set; }
        public string[] Performers { get; set; }
        public string[] AlbumArtists { get; set; }
        public uint Track { get; set; }
        public uint TrackCount { get; set; }
        public string Album { get; set; }
        public uint Disc { get; set; }
        public uint DiscCount { get; set; }
        public uint Year { get; set; }
        public string Publisher { get; set; }
        public TimeSpan Duration { get; set; }
        public string MusicBrainzReleaseCountry { get; set; }
        public string MusicBrainzReleaseStatus { get; set; }
        public string MusicBrainzReleaseType { get; set; }
        public string MusicBrainzReleaseId { get; set; }
        public string MusicBrainzArtistId { get; set; }
        public string MusicBrainzReleaseArtistId { get; set; }
        public string MusicBrainzReleaseGroupId { get; set; }
        public string MusicBrainzTrackId { get; set; }
        public string MusicBrainzReleaseTrackId { get; set; }
        public string MusicBrainzAlbumComment { get; set; }

        public QualityModel Quality { get; set; }
        public MediaInfoModel MediaInfo { get; set; }

        public AudioTag()
        {
        }
        
        public AudioTag(string path)
        {
            Read(path);
        }

        public void Read(string path)
        {
            using(var file = TagLib.File.Create(path))
            {
                Logger.Debug("Starting Tag Parse for {0}", file.Name);

                var tag = file.Tag;

                Title = tag.Title;
                Performers = tag.Performers;
                AlbumArtists = tag.AlbumArtists;
                Track = tag.Track;
                TrackCount = tag.TrackCount;
                Album = tag.Album;
                Disc = tag.Disc;
                DiscCount = tag.DiscCount;
                Year = tag.Year;
                Publisher = tag.Publisher;
                Duration = file.Properties.Duration;
                MusicBrainzReleaseCountry = tag.MusicBrainzReleaseCountry;
                MusicBrainzReleaseStatus = tag.MusicBrainzReleaseStatus;
                MusicBrainzReleaseType = tag.MusicBrainzReleaseType;
                MusicBrainzReleaseId = tag.MusicBrainzReleaseId;
                MusicBrainzArtistId = tag.MusicBrainzArtistId;
                MusicBrainzReleaseArtistId = tag.MusicBrainzReleaseArtistId;
                MusicBrainzReleaseGroupId = tag.MusicBrainzReleaseGroupId;
                MusicBrainzTrackId = tag.MusicBrainzTrackId;

                // Do the ones that aren't handled by the generic taglib implementation
                if (file.TagTypesOnDisk.HasFlag(TagTypes.Id3v2))
                {
                    var id3tag = (TagLib.Id3v2.Tag) file.GetTag(TagTypes.Id3v2);
                    MusicBrainzAlbumComment = UserTextInformationFrame.Get(id3tag, "MusicBrainz Album Comment", false)?.Text.ExclusiveOrDefault();
                    MusicBrainzReleaseTrackId =  UserTextInformationFrame.Get(id3tag, "MusicBrainz Release Track Id", false)?.Text.ExclusiveOrDefault();
                }
                else if (file.TagTypesOnDisk.HasFlag(TagTypes.Xiph))
                {
                    var flactag = (TagLib.Ogg.XiphComment) file.GetTag(TagLib.TagTypes.Xiph);
                    MusicBrainzAlbumComment = flactag.GetField("MUSICBRAINZ_ALBUMCOMMENT").ExclusiveOrDefault();
                    MusicBrainzReleaseTrackId = flactag.GetField("MUSICBRAINZ_RELEASETRACKID").ExclusiveOrDefault();
                }
                else if (file.TagTypesOnDisk.HasFlag(TagTypes.Ape))
                {
                    var apetag = (TagLib.Ape.Tag) file.GetTag(TagTypes.Ape);
                    Publisher = apetag.GetItem("Label")?.ToString();
                    MusicBrainzAlbumComment = apetag.GetItem("MUSICBRAINZ_ALBUMCOMMENT")?.ToString();
                    MusicBrainzReleaseTrackId = apetag.GetItem("MUSICBRAINZ_RELEASETRACKID")?.ToString();
                }
                else if (file.TagTypesOnDisk.HasFlag(TagTypes.Asf))
                {
                    var asftag = (TagLib.Asf.Tag) file.GetTag(TagTypes.Asf);
                    Publisher = asftag.GetDescriptorString("WM/Publisher");
                    MusicBrainzAlbumComment = asftag.GetDescriptorString("MusicBrainz/Album Comment");
                    MusicBrainzReleaseTrackId = asftag.GetDescriptorString("MusicBrainz/Release Track Id");
                }
                else if (file.TagTypesOnDisk.HasFlag(TagTypes.Apple))
                {
                    var appletag = (TagLib.Mpeg4.AppleTag) file.GetTag(TagTypes.Apple);
                    MusicBrainzAlbumComment = appletag.GetDashBox("com.apple.iTunes", "MusicBrainz Album Comment");
                    MusicBrainzReleaseTrackId = appletag.GetDashBox("com.apple.iTunes", "MusicBrainz Release Track Id");
                }

                foreach (ICodec codec in file.Properties.Codecs)
                {
                    IAudioCodec acodec = codec as IAudioCodec;

                    if (acodec != null && (acodec.MediaTypes & MediaTypes.Audio) != MediaTypes.None)
                    {
                        Logger.Debug("Audio Properties : " + acodec.Description + ", Bitrate: " + acodec.AudioBitrate + ", Sample Size: " +
                                     file.Properties.BitsPerSample + ", SampleRate: " + acodec.AudioSampleRate + ", Channels: " + acodec.AudioChannels);

                        Quality = QualityParser.ParseQuality(file.Name, acodec.Description, acodec.AudioBitrate, file.Properties.BitsPerSample);
                        Logger.Debug("Quality parsed: {0}", Quality);

                        MediaInfo = new MediaInfoModel {
                            AudioFormat = acodec.Description,
                            AudioBitrate = acodec.AudioBitrate,
                            AudioChannels = acodec.AudioChannels,
                            AudioBits = file.Properties.BitsPerSample,
                            AudioSampleRate = acodec.AudioSampleRate
                        };
                    }
                }
            }
        }

        private void WriteId3Tag(TagLib.Id3v2.Tag tag, string id, string value)
        {
            var frame = UserTextInformationFrame.Get(tag, id, true);
                
            if (value.IsNotNullOrWhiteSpace())
            {
                frame.Text = value.Split(';');
            }
            else
            {
                tag.RemoveFrame(frame);
            }
        }

        public void Write(string path)
        {
            using(var file = TagLib.File.Create(path))
            {
                var tag = file.Tag;

                // do the ones with direct support in TagLib
                tag.Title = Title;
                tag.Performers = Performers;
                tag.AlbumArtists = AlbumArtists;
                tag.Track = Track;
                tag.TrackCount = TrackCount;
                tag.Album = Album;
                tag.Disc = Disc;
                tag.DiscCount = DiscCount;
                tag.Year = Year;
                tag.Publisher = Publisher;
                tag.MusicBrainzReleaseCountry = MusicBrainzReleaseCountry;
                tag.MusicBrainzReleaseStatus = MusicBrainzReleaseStatus;
                tag.MusicBrainzReleaseType = MusicBrainzReleaseType;
                tag.MusicBrainzReleaseId = MusicBrainzReleaseId;
                tag.MusicBrainzArtistId = MusicBrainzArtistId;
                tag.MusicBrainzReleaseArtistId = MusicBrainzReleaseArtistId;
                tag.MusicBrainzReleaseGroupId = MusicBrainzReleaseGroupId;
                tag.MusicBrainzTrackId = MusicBrainzTrackId;

                if (file.TagTypes.HasFlag(TagTypes.Id3v2))
                {
                    var id3tag = (TagLib.Id3v2.Tag) file.GetTag(TagTypes.Id3v2);
                    WriteId3Tag(id3tag, "MusicBrainz Album Comment", MusicBrainzAlbumComment);
                    WriteId3Tag(id3tag, "MusicBrainz Release Track Id", MusicBrainzReleaseTrackId);
                }
                else if (file.TagTypes.HasFlag(TagTypes.Xiph))
                {
                    var flactag = (TagLib.Ogg.XiphComment) file.GetTag(TagLib.TagTypes.Xiph);
                    flactag.SetField("MUSICBRAINZ_ALBUMCOMMENT", MusicBrainzAlbumComment);
                    flactag.SetField("MUSICBRAINZ_RELEASETRACKID", MusicBrainzReleaseTrackId);
                }
                else if (file.TagTypes.HasFlag(TagTypes.Ape))
                {
                    var apetag = (TagLib.Ape.Tag) file.GetTag(TagTypes.Ape);
                    apetag.SetValue("Label", Publisher);
                    apetag.SetValue("MUSICBRAINZ_ALBUMCOMMENT", MusicBrainzAlbumComment);
                    apetag.SetValue("MUSICBRAINZ_RELEASETRACKID", MusicBrainzReleaseTrackId);
                }
                else if (file.TagTypes.HasFlag(TagTypes.Asf))
                {
                    var asftag = (TagLib.Asf.Tag) file.GetTag(TagTypes.Asf);
                    asftag.SetDescriptorString(Publisher, "WM/Publisher");
                    asftag.SetDescriptorString(MusicBrainzAlbumComment, "MusicBrainz/Album Comment");
                    asftag.SetDescriptorString(MusicBrainzReleaseTrackId, "MusicBrainz/Release Track Id");
                }
                else if (file.TagTypes.HasFlag(TagTypes.Apple))
                {
                    var appletag = (TagLib.Mpeg4.AppleTag) file.GetTag(TagTypes.Apple);
                    appletag.SetDashBox("com.apple.iTunes", "MusicBrainz Album Comment", MusicBrainzAlbumComment);
                    appletag.SetDashBox("com.apple.iTunes", "MusicBrainz Release Track Id", MusicBrainzReleaseTrackId);
                }

                file.Save();
            }
        }

        public Dictionary<string, Tuple<string, string>> Diff(AudioTag other)
        {
            var output = new Dictionary<string, Tuple<string, string>>();

            if (Title != other.Title)
            {
                output.Add("Title", Tuple.Create(Title, other.Title));
            }

            if (!Performers.SequenceEqual(other.Performers))
            {
                var oldValue = Performers.Any() ? string.Join(" / ", Performers) : null;
                var newValue = other.Performers.Any() ? string.Join(" / ", other.Performers) : null;

                output.Add("Artist", Tuple.Create(oldValue, newValue));
            }

            if (Album != other.Album)
            {
                output.Add("Album", Tuple.Create(Album, other.Album));
            }

            if (!AlbumArtists.SequenceEqual(other.AlbumArtists))
            {
                var oldValue = AlbumArtists.Any() ? string.Join(" / ", AlbumArtists) : null;
                var newValue = other.AlbumArtists.Any() ? string.Join(" / ", other.AlbumArtists) : null;

                output.Add("Album Artist", Tuple.Create(oldValue, newValue));
            }

            if (Track != other.Track)
            {
                output.Add("Track", Tuple.Create(Track.ToString(), other.Track.ToString()));
            }

            if (TrackCount != other.TrackCount)
            {
                output.Add("Track Count", Tuple.Create(TrackCount.ToString(), other.TrackCount.ToString()));
            }

            if (Disc != other.Disc)
            {
                output.Add("Disc", Tuple.Create(Disc.ToString(), other.Disc.ToString()));
            }

            if (DiscCount != other.DiscCount)
            {
                output.Add("Disc Count", Tuple.Create(DiscCount.ToString(), other.DiscCount.ToString()));
            }

            if (Year != other.Year)
            {
                output.Add("Year", Tuple.Create(Year.ToString(), other.Year.ToString()));
            }

            if (Publisher != other.Publisher)
            {
                output.Add("Label", Tuple.Create(Publisher, other.Publisher));
            }

            return output;
        }

        public static implicit operator ParsedTrackInfo (AudioTag tag)
        {

            var artist = tag.AlbumArtists.FirstOrDefault();

            if (artist.IsNullOrWhiteSpace())
            {
                artist = tag.Performers.FirstOrDefault();
            }

            var artistTitleInfo = new ArtistTitleInfo
            {
                Title = artist,
                Year = (int)tag.Year
            };
                
            return new ParsedTrackInfo {
                Language = Language.English,
                AlbumTitle = tag.Album,
                ArtistTitle = artist,
                ArtistMBId = tag.MusicBrainzReleaseArtistId,
                AlbumMBId = tag.MusicBrainzReleaseGroupId,
                ReleaseMBId = tag.MusicBrainzReleaseId,
                // SIC: the recording ID is stored in this field.
                // See https://picard.musicbrainz.org/docs/mappings/
                RecordingMBId = tag.MusicBrainzTrackId,
                TrackMBId = tag.MusicBrainzReleaseTrackId,
                DiscNumber = (int)tag.Disc,
                DiscCount = (int)tag.DiscCount,
                Year = tag.Year,
                Label = tag.Publisher,
                TrackNumbers = new [] { (int) tag.Track },
                ArtistTitleInfo = artistTitleInfo,
                Title = tag.Title,
                CleanTitle = tag.Title?.CleanTrackTitle(),
                Country = IsoCountries.Find(tag.MusicBrainzReleaseCountry),
                Duration = tag.Duration,
                Disambiguation = tag.MusicBrainzAlbumComment,
                Quality = tag.Quality,
                MediaInfo = tag.MediaInfo
            };
        }
    }
}
