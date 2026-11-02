using System.IO;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class SaveTests
    {
        private string _dir;
        private string Path => System.IO.Path.Combine(_dir, "test.save");

        [SetUp]
        public void SetUp()
        {
            _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hushlands_save_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Crc32_MatchesStandardCheckValue()
        {
            Assert.AreEqual(0xCBF43926u, FileSaveService.Crc32("123456789"));
        }

        [Test]
        public void RoundTrips_AllTypes_AndAwkwardStrings()
        {
            var s = new FileSaveService(Path);
            s.Save("i", 42);
            s.Save("l", 9_000_000_000L);
            s.Save("f", 0.1f);
            s.Save("d", 1.0 / 3.0);
            s.Save("b", true);
            s.Save("s", "tab\there\nnew line \\ back");
            s.Flush();
            var r = new FileSaveService(Path);
            Assert.IsFalse(r.CorruptionDetected);
            Assert.IsTrue(r.TryLoad("i", out int i) && i == 42);
            Assert.IsTrue(r.TryLoad("l", out long l) && l == 9_000_000_000L);
            Assert.IsTrue(r.TryLoad("f", out float f) && f == 0.1f);
            Assert.IsTrue(r.TryLoad("d", out double d) && d == 1.0 / 3.0);
            Assert.IsTrue(r.TryLoad("b", out bool b) && b);
            Assert.IsTrue(r.TryLoad("s", out string str));
            Assert.AreEqual("tab\there\nnew line \\ back", str);
            Assert.IsFalse(r.TryLoad("i", out string _), "wrong type must not load");
            Assert.IsFalse(r.TryLoad("missing", out int _));
        }

        [Test]
        public void DamagedFile_RecoversFromBackup()
        {
            var s = new FileSaveService(Path);
            s.Save("runs", 1);
            s.Flush();
            s.Save("runs", 2);
            s.Flush();
            File.WriteAllText(Path, "hushlands-save 1\nruns\ti\t999\ncrc 00000000\n");
            var r = new FileSaveService(Path);
            Assert.IsTrue(r.CorruptionDetected);
            Assert.IsTrue(r.RecoveredFromBackup);
            Assert.IsTrue(r.TryLoad("runs", out int runs));
            Assert.AreEqual(1, runs, "backup holds the previous good save");
        }

        [Test]
        public void TruncatedFile_WithoutBackup_StartsFresh_NoThrow()
        {
            File.WriteAllText(Path, "hushlands-save 1\nru");
            var r = new FileSaveService(Path);
            Assert.IsTrue(r.CorruptionDetected);
            Assert.IsFalse(r.RecoveredFromBackup);
            Assert.IsFalse(r.TryLoad("runs", out int _));
        }

        [Test]
        public void NewerVersion_IsNeverOverwritten()
        {
            var tmp = new FileSaveService(Path);
            tmp.Save("runs", 5);
            string text = tmp.Serialize().Replace("hushlands-save 1", "hushlands-save 9");
            string body = text.Substring(0, text.LastIndexOf("crc ", System.StringComparison.Ordinal));
            File.WriteAllText(Path, body + "crc " + FileSaveService.Crc32(body).ToString("x8") + "\n");
            string before = File.ReadAllText(Path);
            var r = new FileSaveService(Path);
            Assert.IsTrue(r.ReadOnlyNewerVersion);
            r.Save("runs", 6);
            r.Flush();
            Assert.AreEqual(before, File.ReadAllText(Path));
        }

        [Test]
        public void Flush_IsAtomic_LeavesNoTempFile()
        {
            var s = new FileSaveService(Path);
            s.Save("a", 1);
            s.Flush();
            s.Save("a", 2);
            s.Flush();
            Assert.IsFalse(File.Exists(Path + ".tmp"));
            Assert.IsTrue(File.Exists(Path + ".bak"));
            Assert.IsFalse(s.Dirty);
        }
    }
}
