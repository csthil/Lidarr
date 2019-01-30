using System.IO;
using NUnit.Framework;
using FluentAssertions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Configuration;
using FizzWare.NBuilder;
using System;
using System.Collections;
using System.Linq;

namespace NzbDrone.Core.Test.MediaFiles.AudioTagServiceFixture
{
    [TestFixture]
    public class AudioTagServiceFixture : CoreTest<AudioTagService>
    {
        // various tests will be run for all files listed here
        private static string[] MediaFiles = new [] { "nin.mp3", "nin.flac", "nin.m4a", "nin.wma", "nin.ape" };

        // properties to skip standard check of equality/difference
        private static string[] AudioTagPropertiesToSkip = new [] { "Duration", "Quality", "MediaInfo" };
        
        private string testdir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media");
        private string copiedFile = null;
        private AudioTag testTags;
        
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.WriteAudioTags)
                .Returns(WriteAudioTagsType.Sync);

            // have to manually set the arrays of string parameters and integers to values > 1
            testTags = Builder<AudioTag>.CreateNew()
                .With(x => x.Track = 22)
                .With(x => x.TrackCount = 33)
                .With(x => x.Disc = 44)
                .With(x => x.DiscCount = 55)
                .With(x => x.Year = 8686)
                .With(x => x.Performers = new [] { "Performer1" })
                .With(x => x.AlbumArtists = new [] { "Album Artist1" })
                .Build();
        }

        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(copiedFile))
            {
                File.Delete(copiedFile);
            }
        }

        private void GivenFileCopy(string filename)
        {
            var original = Path.Combine(testdir, filename);
            var tempname = Path.GetRandomFileName() + "." + Path.GetExtension(filename);
            copiedFile = Path.Combine(testdir, tempname);

            File.Copy(original, copiedFile);
        }

        private void VerifyDifferent(AudioTag a, AudioTag b)
        {
            foreach (var property in typeof(AudioTag).GetProperties())
            {
                if (AudioTagPropertiesToSkip.Contains(property.Name))
                {
                    continue;
                }
                
                if (property.CanRead)
                {
                    if (property.PropertyType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEquatable<>)))
                    {
                        var val1 = property.GetValue(a, null);
                        var val2 = property.GetValue(b, null);
                        val1.Should().NotBe(val2);
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                    {
                        var val1 = (IEnumerable) property.GetValue(a, null);
                        var val2 = (IEnumerable) property.GetValue(b, null);

                        if (val1 != null && val2 != null)
                        {
                            val1.Should().NotBeEquivalentTo(val2);
                        }
                    }
                }
            }
        }

        private void VerifySame(AudioTag a, AudioTag b)
        {
            foreach (var property in typeof(AudioTag).GetProperties())
            {
                if (AudioTagPropertiesToSkip.Contains(property.Name))
                {
                    continue;
                }

                if (property.CanRead)
                {
                    if (property.PropertyType.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEquatable<>)))
                    {
                        var val1 = property.GetValue(a, null);
                        var val2 = property.GetValue(b, null);
                        val1.Should().Be(val2);
                    }
                    else if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
                    {
                        var val1 = (IEnumerable) property.GetValue(a, null);
                        var val2 = (IEnumerable) property.GetValue(b, null);
                        val1.Should().BeEquivalentTo(val2);
                    }
                }
            }
        }

        [Test, TestCaseSource("MediaFiles")]
        public void should_read_duration(string filename)
        {
            var path = Path.Combine(testdir, filename);

            var tags = Subject.ReadTags(path);

            tags.Duration.Should().BeCloseTo(new TimeSpan(0, 0, 1, 25, 130), 50);
        }

        [Test, TestCaseSource("MediaFiles")]
        public void should_read_write_tags(string filename)
        {
            GivenFileCopy(filename);
            var path = copiedFile;

            var initialtags = Subject.ReadAudioTag(path);

            VerifyDifferent(initialtags, testTags);

            testTags.Write(path);

            var writtentags = Subject.ReadAudioTag(path);

            VerifySame(writtentags, testTags);
        }

        [Test, TestCaseSource("MediaFiles")]
        public void should_remove_mb_tags(string filename)
        {
            GivenFileCopy(filename);
            var path = copiedFile;

            var track = new TrackFile {
                Artist = new Artist {
                    Path = Path.GetDirectoryName(path)
                },
                RelativePath = Path.GetFileName(path)
            };

            testTags.Write(path);

            var withmb = Subject.ReadAudioTag(path);

            VerifySame(withmb, testTags);

            Subject.RemoveMusicBrainzTags(track);

            var tag = Subject.ReadAudioTag(path);

            tag.MusicBrainzReleaseCountry.Should().BeNull();
            tag.MusicBrainzReleaseStatus.Should().BeNull();
            tag.MusicBrainzReleaseType.Should().BeNull();
            tag.MusicBrainzReleaseId.Should().BeNull();
            tag.MusicBrainzArtistId.Should().BeNull();
            tag.MusicBrainzReleaseArtistId.Should().BeNull();
            tag.MusicBrainzReleaseGroupId.Should().BeNull();
            tag.MusicBrainzTrackId.Should().BeNull();
            tag.MusicBrainzAlbumComment.Should().BeNull();
            tag.MusicBrainzReleaseTrackId.Should().BeNull();
        }
    }
}
