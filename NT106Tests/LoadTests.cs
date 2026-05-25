// ============================================================
// NT106Tests/LoadTests.cs
// Kiểm thử sức chịu tải cho DrawingServer
// ============================================================
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SharedLib.Packets;
using SharedLib.Payloads;
using SharedLib.Security;

namespace NT106Tests
{
    [TestClass]
    public class LoadTests
    {
        public const string SERVER_IP   = "127.0.0.1";
        public const int    SERVER_PORT = 8888;
        private const int   TIMEOUT_MS  = 5000;

        // ── Test 1: Serialize 1000 packet DRAW ──────────────────
        [TestMethod]
        [Description("Đo hiệu năng serialize/deserialize 1000 packet DRAW liên tiếp")]
        public void PacketSerializePerformance_1000Packets()
        {
            const int COUNT = 1000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < COUNT; i++)
            {
                var payload = new DrawPayload
                {
                    X1 = i % 1280,
                    Y1 = i % 720,
                    X2 = (i + 1) % 1280,
                    Y2 = (i + 1) % 720,
                    ColorARGB = -16777216,
                    Thickness = 2,
                    ToolType = "Pen",
                    Username = "LoadTestUser",
                    ActionID = Guid.NewGuid().ToString()
                };
                var packet = PacketHelper.Create(CommandType.DRAW, payload);
                byte[] data = packet.Serialize();
                var back = Packet.Deserialize(data);
                Assert.AreEqual(CommandType.DRAW, back.Cmd);
            }

            sw.Stop();
            Console.WriteLine($"[LoadTest] {COUNT} packet serialize/deserialize: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"[LoadTest] Trung binh: {sw.ElapsedMilliseconds / (double)COUNT:F3}ms/packet");
            Assert.IsTrue(sw.ElapsedMilliseconds < 3000,
                $"1000 packets phai serialize xong trong 3s, thuc te: {sw.ElapsedMilliseconds}ms");
        }

        // ── Test 2: AES 500 round-trip ───────────────────────────
        [TestMethod]
        [Description("Đo hiệu năng AES-256 encrypt/decrypt 500 lần")]
        public void AesEncryptDecryptPerformance_500RoundTrips()
        {
            const int COUNT = 500;
            var payload = new DrawPayload
            {
                X1 = 100,
                Y1 = 200,
                X2 = 110,
                Y2 = 210,
                ColorARGB = -1,
                Thickness = 2,
                ToolType = "Pen",
                Username = "TestUser"
            };
            var packet  = PacketHelper.Create(CommandType.DRAW, payload);
            byte[] raw  = packet.Serialize();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < COUNT; i++)
            {
                byte[] encrypted = AesHelper.Encrypt(raw);
                byte[] decrypted = AesHelper.Decrypt(encrypted);
                Assert.AreEqual(raw.Length, decrypted.Length);
            }
            sw.Stop();

            Console.WriteLine($"[LoadTest] {COUNT} AES encrypt+decrypt: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"[LoadTest] Trung binh: {sw.ElapsedMilliseconds / (double)COUNT:F3}ms/round-trip");
            Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
                $"500 AES round-trip phai xong trong 5s, thuc te: {sw.ElapsedMilliseconds}ms");
        }

        // ── Test 3: Packet ảnh lớn ~100KB ────────────────────────
        [TestMethod]
        [Description("Test serialize/encrypt packet ảnh lớn ~100KB (mô phỏng AI result)")]
        public void LargeImagePacket_SerializeAndEncrypt()
        {
            byte[] fakeImage = new byte[75000];
            new Random(42).NextBytes(fakeImage);
            string base64Image = Convert.ToBase64String(fakeImage);

            var payload = new AiTextToImageResultPayload
            {
                RequesterUsername = "LoadTestUser",
                ActionID = Guid.NewGuid().ToString(),
                ImageData = base64Image,
                X = 0, Y = 0, Width = 512, Height = 512
            };

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var packet  = PacketHelper.Create(CommandType.AI_TEXT_TO_IMAGE, payload);
                byte[] data = packet.Serialize();
                byte[] enc  = AesHelper.Encrypt(data);
                byte[] dec  = AesHelper.Decrypt(enc);
                var back    = Packet.Deserialize(dec);
                Assert.AreEqual(CommandType.AI_TEXT_TO_IMAGE, back.Cmd);
            }
            sw.Stop();

            Console.WriteLine($"[LoadTest] 10x large packet (~100KB) serialize+encrypt: {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(sw.ElapsedMilliseconds < 10000, "10 large packet phai xong trong 10s");
        }

        // ── Test 5: TCP kết nối (cần server đang chạy) ──────────
        [TestMethod]
        [Description("Thử kết nối TCP 10 client đồng thời — bỏ [Ignore] khi server đang chạy")]
        [Ignore]
        public async Task TcpConcurrentConnections_10Clients()
        {
            const int CLIENT_COUNT = 10;
            int successCount = 0;
            var tasks = new List<Task>();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < CLIENT_COUNT; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var tcp = new TcpClient();
                        await tcp.ConnectAsync(SERVER_IP, SERVER_PORT);
                        if (tcp.Connected) Interlocked.Increment(ref successCount);
                        await Task.Delay(500);
                    }
                    catch { }
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine($"[LoadTest] {CLIENT_COUNT} TCP connections: {successCount} thanh cong trong {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(successCount >= CLIENT_COUNT * 0.8,
                $"It nhat 80% client phai ket noi duoc ({successCount}/{CLIENT_COUNT})");
        }
    }

    // ── Báo cáo tổng hợp ────────────────────────────────────────
    [TestClass]
    public class LoadTestSummary
    {
        [TestMethod]
        [Description("In thông số hệ thống khi chạy test")]
        public void PrintSystemInfo()
        {
            Console.WriteLine("============================================");
            Console.WriteLine("  NT106 Drawing App - Load Test Report");
            Console.WriteLine("============================================");
            Console.WriteLine($"OS        : {Environment.OSVersion}");
            Console.WriteLine($"CPU cores : {Environment.ProcessorCount}");
            Console.WriteLine($"RAM (MB)  : {GC.GetTotalMemory(false) / 1024 / 1024}");
            Console.WriteLine($"Server    : {LoadTests.SERVER_IP}:{LoadTests.SERVER_PORT}");
            Console.WriteLine($"Time      : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine("============================================");
            Assert.IsTrue(true);
        }
    }
}
