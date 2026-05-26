using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedLib.Config;

namespace NT106Tests
{
    [TestClass]
    public class PostgresConnectionStringTests
    {
        [TestMethod]
        public void Normalize_PostgresUri_ConvertsDigitalOceanStyleUrl()
        {
            string input = "postgresql://doadmin:p%40ss%3Aword@db.example.com:25060/defaultdb?sslmode=require";

            string normalized = PostgresConnectionString.Normalize(input);

            Assert.IsFalse(normalized.Contains("postgresql://"));
            StringAssert.Contains(normalized, "Host=db.example.com");
            StringAssert.Contains(normalized, "Port=25060");
            StringAssert.Contains(normalized, "Database=defaultdb");
            StringAssert.Contains(normalized, "Username=doadmin");
            StringAssert.Contains(normalized, "Password=p@ss:word");
            StringAssert.Contains(normalized, "SSL Mode=Require");
        }

        [TestMethod]
        public void Normalize_KeyValue_NormalizesSslAndDropsChannelBinding()
        {
            string input = "Host=db.example.com;Database=neondb;Username=user;Password=pass;sslmode=require;Channel Binding=Require;";

            string normalized = PostgresConnectionString.Normalize(input);

            StringAssert.Contains(normalized, "Host=db.example.com");
            StringAssert.Contains(normalized, "SSL Mode=Require");
            Assert.IsFalse(normalized.Contains("Channel Binding"));
        }

        [TestMethod]
        public void Normalize_PostgresUri_AllowsSslModeWithoutExplicitValue()
        {
            string input = "postgresql://user:pass@db.example.com/defaultdb?sslmode";

            string normalized = PostgresConnectionString.Normalize(input);

            StringAssert.Contains(normalized, "SSL Mode=Require");
        }

        [TestMethod]
        public void Normalize_DoesNotInjectMaxPoolSizeKeyword()
        {
            string input = "postgresql://user:pass@db.example.com/defaultdb?sslmode=require";

            string normalized = PostgresConnectionString.Normalize(input);

            Assert.IsFalse(normalized.ToLowerInvariant().Contains("max pool size"));
            StringAssert.Contains(normalized, "Timeout=15");
        }
    }
}
