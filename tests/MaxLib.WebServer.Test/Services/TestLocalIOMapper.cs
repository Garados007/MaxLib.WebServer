using MaxLib.WebServer.Services;
using MaxLib.WebServer.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace MaxLib.WebServer.Test.Services
{
    [TestClass]
    public class TestLocalIOMapperShortenPath
    {
        [TestMethod]
        public void TestNormalPathIsUnchanged()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "foo", "bar" });
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new[] { "foo", "bar" }, result.Value.ToArray());
        }

        [TestMethod]
        public void TestDotSegmentIsRemoved()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "foo", ".", "bar" });
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new[] { "foo", "bar" }, result.Value.ToArray());
        }

        [TestMethod]
        public void TestParentSegmentGoesUpOneLevel()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "foo", "bar", "..", "baz" });
            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(new[] { "foo", "baz" }, result.Value.ToArray());
        }

        [TestMethod]
        public void TestLeadingParentSegmentIsRejected()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "..", "foo" });
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestTileWithEmbeddedForwardSlashIsRejected()
        {
            // Simulates a %2f-encoded tile that decodes to "../../../etc/passwd" as a single,
            // opaque path segment after splitting on literal '/'. Since it never equals ".."
            // exactly, the plain segment-based guard would let it through.
            var result = LocalIOMapper.ShortenPath(new[] { "static", "../../../etc/passwd" });
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestTileWithEmbeddedBackslashIsRejected()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "static", "foo\\..\\bar" });
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestRootedTileIsRejected()
        {
            var result = LocalIOMapper.ShortenPath(new[] { "static", "/etc/passwd" });
            Assert.IsNull(result);
        }

        [TestMethod]
        public void TestWindowsDriveRelativeTileIsRejected()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("drive-relative paths are only considered rooted on Windows");
            var result = LocalIOMapper.ShortenPath(new[] { "static", "C:" });
            Assert.IsNull(result);
        }
    }

    [TestClass]
    public class TestLocalIOMapperFileMappingRule
    {
        TestWebServer server;
        TestTask test;
        string baseDir;
        string publicDir;
        string secretFile;

        [TestInitialize]
        public void Init()
        {
            baseDir = Path.Combine(Path.GetTempPath(), "MaxLibWebServerTest_" + Guid.NewGuid().ToString("N"));
            publicDir = Path.Combine(baseDir, "public");
            Directory.CreateDirectory(publicDir);
            File.WriteAllText(Path.Combine(publicDir, "hello.txt"), "hi");
            secretFile = Path.Combine(baseDir, "secret.txt");
            File.WriteAllText(secretFile, "top secret");

            server = new TestWebServer();
            var mapper = new LocalIOMapper();
            mapper.AddFileMapping("static", publicDir);
            server.AddWebService(mapper);
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.CreateDocument,
                TerminationStage = ServerStage.CreateDocument,
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var source in test.GetDataSources())
                source.Dispose();
            Directory.Delete(baseDir, true);
        }

        [TestMethod]
        public void TestValidFileIsServed()
        {
            test.Request.Url = "/static/hello.txt";
            var mapper = server.WebServiceGroups[ServerStage.CreateDocument]
                .Get<LocalIOMapper>();
            Assert.IsTrue(mapper.CanWorkWith(test.Task));
            Assert.AreEqual(1, test.GetDataSources().Count);
            var source = Assert.IsInstanceOfType<HttpFileDataSource>(test.GetDataSources()[0]);
            Assert.AreEqual(Path.Combine(publicDir, "hello.txt"), source.Path);
        }

        [TestMethod]
        public void TestEncodedSlashTraversalIsRejected()
        {
            // %2f is decoded only after the URL is split on literal '/', so this whole
            // segment survives the split as a single tile that decodes to "../secret.txt".
            test.Request.Url = "/static/..%2fsecret.txt";
            var mapper = server.WebServiceGroups[ServerStage.CreateDocument]
                .Get<LocalIOMapper>();
            Assert.IsFalse(mapper.CanWorkWith(test.Task));
            Assert.AreEqual(0, test.GetDataSources().Count);
        }

        [TestMethod]
        public void TestEncodedAbsolutePathOverrideIsRejected()
        {
            // decodes to a single, rooted tile "/etc/passwd" which Path.Combine would
            // otherwise return verbatim, discarding the configured base directory.
            test.Request.Url = "/static/%2fetc%2fpasswd";
            var mapper = server.WebServiceGroups[ServerStage.CreateDocument]
                .Get<LocalIOMapper>();
            Assert.IsFalse(mapper.CanWorkWith(test.Task));
            Assert.AreEqual(0, test.GetDataSources().Count);
        }
    }

    [TestClass]
    public class TestLocalIOMapperDirectoryMappingRule
    {
        TestWebServer server;
        TestTask test;
        string baseDir;
        string publicDir;

        [TestInitialize]
        public void Init()
        {
            baseDir = Path.Combine(Path.GetTempPath(), "MaxLibWebServerTest_" + Guid.NewGuid().ToString("N"));
            publicDir = Path.Combine(baseDir, "public");
            Directory.CreateDirectory(publicDir);
            File.WriteAllText(Path.Combine(publicDir, "hello.txt"), "hi");
            File.WriteAllText(Path.Combine(baseDir, "secret.txt"), "top secret");

            server = new TestWebServer();
            var mapper = new LocalIOMapper();
            mapper.AddDirectoryMapping("static", publicDir);
            server.AddWebService(mapper);
            test = new TestTask(server)
            {
                CurrentStage = ServerStage.CreateDocument,
                TerminationStage = ServerStage.CreateDocument,
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var source in test.GetDataSources())
                source.Dispose();
            Directory.Delete(baseDir, true);
        }

        [TestMethod]
        public void TestValidDirectoryIsListed()
        {
            test.Request.Url = "/static/";
            var mapper = server.WebServiceGroups[ServerStage.CreateDocument]
                .Get<LocalIOMapper>();
            Assert.IsTrue(mapper.CanWorkWith(test.Task));
            Assert.AreEqual(1, test.GetDataSources().Count);
        }

        [TestMethod]
        public void TestEncodedSlashTraversalIsRejected()
        {
            test.Request.Url = "/static/..%2f";
            var mapper = server.WebServiceGroups[ServerStage.CreateDocument]
                .Get<LocalIOMapper>();
            Assert.IsFalse(mapper.CanWorkWith(test.Task));
            Assert.AreEqual(0, test.GetDataSources().Count);
        }
    }
}
