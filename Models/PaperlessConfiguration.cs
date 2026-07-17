namespace PontBascule.Models
{
    public class PaperlessConfiguration
    {
        public string BaseUrl { get; set; } = string.Empty;

        public string ApiToken { get; set; } = string.Empty;

        public string Correspondent { get; set; } = "Pont Bascule";

        public string DocumentType { get; set; } = "Ticket de pesée";

        public string Tag { get; set; } = "pont-bascule";
    }
}
