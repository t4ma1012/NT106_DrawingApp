namespace SharedLib.Payloads
{
    // CMD_LOGIN / CMD_REGISTER
    public class LoginPayload
    {
        public string Username { get; set; }
        public string Password { get; set; } // gửi plaintext, server tự hash
    }

    // CMD_LOGIN_RESPONSE
    public class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string Username { get; set; }
    }
}