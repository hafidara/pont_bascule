namespace PontBascule.Models
{
    public class ScaleConfiguration
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public string Parity { get; set; } = "None";
        public string StopBits { get; set; } = "One";
        public int ReadTimeout { get; set; } = 1000;
        public string ReadCommand { get; set; } = string.Empty;
        public bool ContinuousMode { get; set; } = true;
        public string DeviceName { get; set; } = "Sauraus IND200";
        public string Model { get; set; } = "IND200+RS232";
        public string Certificate { get; set; } = "0200-NAWI-03258";
        public bool RequireStableWeight { get; set; } = true;
    }
}
