namespace VIMS.Application.DTOs
{
    public class InitiateTransferDTO
    {
        public int PolicyId { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;
    }
}
