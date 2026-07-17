namespace PontBascule.Models
{
    public class SageDatabaseConfiguration
    {
        public string Provider { get; set; } = "SqlServer";

        public string Server { get; set; } = string.Empty;

        public string Database { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool TrustServerCertificate { get; set; } = true;
    }
}