// ============================================================
// SharedLib/Security/SecurityConfig.cs
// Tuần 4 — AES-256 key cho mã hóa UDP
// KHÔNG commit file này lên GitHub public!
// ============================================================

namespace SharedLib.Security
{
    /// <summary>
    /// Cấu hình bảo mật — B là người DUY NHẤT quản lý file này.
    /// AES key 32 bytes = "NT106_DrawingApp2025_SecureKeyXY"
    /// </summary>
    public static class SecurityConfig
    {
        public static readonly byte[] AesKey = new byte[32]
        {
            0x4E, 0x54, 0x31, 0x30, 0x36, 0x5F, 0x44, 0x72,  // NT106_Dr
            0x61, 0x77, 0x69, 0x6E, 0x67, 0x41, 0x70, 0x70,  // awingApp
            0x32, 0x30, 0x32, 0x35, 0x5F, 0x53, 0x65, 0x63,  // 2025_Sec
            0x75, 0x72, 0x65, 0x4B, 0x65, 0x79, 0x58, 0x59   // ureKeyXY
        };

        /// <summary>AES mode: CBC, padding PKCS7, IV random 16 bytes mỗi lần.</summary>
        public const int IvSize = 16;

        /// <summary>UDP packet: [IV(16B)] [EncryptedPayload(N)]</summary>
        public const int UdpHeaderReserved = IvSize;
    }
}
