// ============================================================
// SharedLib/Payloads/AuthPayload.cs
// ============================================================
using System;

namespace SharedLib.Payloads
{
    public class LoginPayload
    {
        public string Username { get; set; }
        public string Password { get; set; }  // SHA-256 hash từ client
    }

    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string Username { get; set; }
        public int UserID { get; set; }
    }

    public class RegisterPayload
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class RegisterResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }
}
