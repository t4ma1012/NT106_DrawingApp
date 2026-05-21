// ============================================================
// SharedLib/Payloads/ClaimPayload.cs
// ============================================================
namespace SharedLib.Payloads
{
    public class ClaimAreaPayload
    {
        public string ClaimID { get; set; }    // GUID
        public string Username { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int DurationSeconds { get; set; } = 30;
    }

    public class ReleaseAreaPayload
    {
        public string ClaimID { get; set; }
        public string Username { get; set; }
    }

    public class ExtendClaimPayload
    {
        public string ClaimID { get; set; }
        public string Username { get; set; }
        public int ExtraSeconds { get; set; } = 30;
    }
}
