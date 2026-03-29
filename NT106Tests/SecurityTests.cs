// ============================================================
// NT106Tests/SecurityTests.cs
// Tuần 9 — Unit Tests cho module bảo mật
// Test: AES-256 encryption, SecurityConfig, Packet serialization
// ============================================================
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using SharedLib.Security;
using SharedLib.Packets;
using SharedLib.Payloads;

namespace NT106Tests
{
    [TestClass]
    public class SecurityTests
    {
        [TestMethod]
        [Description("Test AES Encrypt/Decrypt round-trip: data mã hóa → giải mã phải bằng original")]
        public void AesEncryptDecryptRoundTrip()
        {
            // Arrange
            string originalText = "Hello Network! This is NT106 Drawing App";
            byte[] originalData = Encoding.UTF8.GetBytes(originalText);

            // Act
            byte[] encrypted = AesHelper.Encrypt(originalData);
            byte[] decrypted = AesHelper.Decrypt(encrypted);
            string decryptedText = Encoding.UTF8.GetString(decrypted);

            // Assert
            Assert.AreEqual(originalText, decryptedText, "Decrypted text should match original");
            Assert.AreNotEqual(originalText, Encoding.UTF8.GetString(encrypted), "Encrypted data should be different");
            Assert.IsTrue(encrypted.Length >= originalData.Length + 16, "Encrypted size should >= original + IV");
        }

        [TestMethod]
        [Description("Test AES Encrypt result structure: [IV(16B)] + [CipherText]")]
        public void AesEncryptStructure()
        {
            // Arrange
            byte[] testData = Encoding.UTF8.GetBytes("test");

            // Act
            byte[] encrypted = AesHelper.Encrypt(testData);

            // Assert
            Assert.IsTrue(encrypted.Length >= 16, "Encrypted data must contain at least IV (16 bytes)");
            
            // Verify can decrypt
            byte[] decrypted = AesHelper.Decrypt(encrypted);
            Assert.AreEqual("test", Encoding.UTF8.GetString(decrypted));
        }

        [TestMethod]
        [Description("Test SecurityConfig: AES key phải là 32 bytes")]
        public void SecurityConfigKeySize()
        {
            // Act
            byte[] aesKey = SecurityConfig.AesKey;

            // Assert
            Assert.AreEqual(32, aesKey.Length, "AES key must be 32 bytes (256 bits)");
            Assert.AreEqual(16, SecurityConfig.IvSize, "IV size must be 16 bytes");
        }

        [TestMethod]
        [Description("Test SecurityConfig: AES key không được là tất cả zeros")]
        public void SecurityConfigKeyNotZero()
        {
            // Act
            byte[] aesKey = SecurityConfig.AesKey;

            // Assert
            int nonZeroCount = 0;
            foreach (byte b in aesKey)
            {
                if (b != 0) nonZeroCount++;
            }
            Assert.IsTrue(nonZeroCount >= 20, "AES key should mostly have non-zero bytes");
        }

        [TestMethod]
        [Description("Test AES Encrypt multiple times: IV khác nhau, ciphertext khác nhau")]
        public void AesEncryptDifferentIvs()
        {
            // Arrange
            byte[] data = Encoding.UTF8.GetBytes("Same data");

            // Act
            byte[] encrypted1 = AesHelper.Encrypt(data);
            byte[] encrypted2 = AesHelper.Encrypt(data);

            // Assert
            Assert.AreNotEqual(
                Convert.ToBase64String(encrypted1),
                Convert.ToBase64String(encrypted2),
                "Two encryptions of same data should produce different ciphertext (due to random IV)"
            );
            
            // But both should decrypt to same value
            Assert.AreEqual(
                Encoding.UTF8.GetString(AesHelper.Decrypt(encrypted1)),
                Encoding.UTF8.GetString(AesHelper.Decrypt(encrypted2))
            );
        }
    }

    [TestClass]
    public class PacketTests
    {
        [TestMethod]
        [Description("Test Packet Serialize/Deserialize round-trip")]
        public void PacketSerializeDeserializeRoundTrip()
        {
            // Arrange
            var loginPayload = new LoginPayload 
            { 
                Username = "testuser", 
                Password = "password123" 
            };
            var packet1 = PacketHelper.Create(CommandType.LOGIN, loginPayload);

            // Act
            byte[] serialized = packet1.Serialize();
            var packet2 = Packet.Deserialize(serialized);
            var deserializedPayload = PacketHelper.GetPayload<LoginPayload>(packet2);

            // Assert
            Assert.AreEqual(CommandType.LOGIN, packet2.Cmd, "Command type should match");
            Assert.AreEqual("testuser", deserializedPayload.Username, "Username should match");
            Assert.AreEqual("password123", deserializedPayload.Password, "Password should match");
        }

        [TestMethod]
        [Description("Test PacketHelper.Create returns valid packet")]
        public void PacketHelperCreate()
        {
            // Arrange
            var roomPayload = new CreateRoomPayload 
            { 
                CanvasWidth = 1280, 
                CanvasHeight = 720 
            };

            // Act
            var packet = PacketHelper.Create(CommandType.CREATE_ROOM, roomPayload);

            // Assert
            Assert.IsNotNull(packet, "Packet should be created");
            Assert.AreEqual(CommandType.CREATE_ROOM, packet.Cmd, "Command should match");
            Assert.AreEqual(Packet.HEADER_BYTE, packet.Header, "Header should be 0xFF");
        }

        [TestMethod]
        [Description("Test different payload types serialize correctly")]
        public void PacketMultiplePayloadTypes()
        {
            // Test with different payload types
            var testCases = new[]
            {
                (CommandType.DRAW, (object)new DrawPayload { X = 100, Y = 200, Color = -1 }),
                (CommandType.CHAT, (object)new ChatPayload { Username = "user1", Message = "hello" }),
                (CommandType.UNDO, (object)new UndoPayload { ActionID = "guid123", Username = "user1" }),
            };

            foreach (var (cmd, payload) in testCases)
            {
                // Act
                var packet = PacketHelper.Create(cmd, payload);
                byte[] serialized = packet.Serialize();
                var deserialized = Packet.Deserialize(serialized);

                // Assert
                Assert.AreEqual(cmd, deserialized.Cmd, $"Command {cmd} should match after round-trip");
                Assert.IsTrue(serialized.Length > 0, $"Serialized data for {cmd} should not be empty");
            }
        }
    }

    [TestClass]
    public class LoggerTests
    {
        [TestMethod]
        [Description("Test Logger initialization")]
        public void LoggerInitialize()
        {
            // Act
            SharedLib.Logging.Logger.Initialize("test_log.txt");
            SharedLib.Logging.Logger.Info("LoggerTests", "Test log message");

            // Assert
            // Logger should not throw
            Assert.IsTrue(true, "Logger initialization should succeed");
        }

        [TestMethod]
        [Description("Test Logger doesn't crash on various input")]
        public void LoggerVariouscalls()
        {
            // Act & Assert
            try
            {
                SharedLib.Logging.Logger.Info("Component1", "Info message");
                SharedLib.Logging.Logger.Warning("Component2", "Warning message");
                SharedLib.Logging.Logger.Error("Component3", "Error message");
                SharedLib.Logging.Logger.Debug("Component4", "Debug message");
                
                var ex = new InvalidOperationException("Test exception");
                SharedLib.Logging.Logger.Exception("Component5", ex);

                Assert.IsTrue(true, "All logger calls should succeed");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Logger should not throw: {ex.Message}");
            }
        }
    }
}
