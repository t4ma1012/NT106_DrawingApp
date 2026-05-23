using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedLib.Config;

namespace NT106Tests
{
    [TestClass]
    public class EnvLoaderTests
    {
        [TestMethod]
        public void Load_FromExplicitFile_SetsProcessVariables()
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempPath, "TEST_ALPHA=123\nTEST_BETA='hello world'\n#COMMENT\n");
                EnvLoader.Load(tempPath, reload: true);

                Assert.AreEqual("123", EnvLoader.Get("TEST_ALPHA"));
                Assert.AreEqual("hello world", EnvLoader.Get("TEST_BETA"));
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        [TestMethod]
        public void GetRequired_ThrowsWhenMissing()
        {
            string key = "MISSING_ENV_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
            Assert.ThrowsException<InvalidOperationException>(() => EnvLoader.GetRequired(key));
        }
    }
}
