namespace SharedLib.Payloads
{
    // CMD_CLAIM_AREA
    public class ClaimAreaPayload
    {
        public string ClaimID { get; set; } = System.Guid.NewGuid().ToString();
        public string Username { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int DurationSeconds { get; set; } = 30;
    }

    // CMD_RELEASE_AREA / CMD_EXTEND_CLAIM
    public class ReleaseAreaPayload
    {
        public string ClaimID { get; set; }
        public string Username { get; set; }
    }

    public class ExtendClaimPayload
    {
        public string ClaimID { get; set; }
        public string Username { get; set; }
        public int ExtraSeconds { get; set; }
    }
}