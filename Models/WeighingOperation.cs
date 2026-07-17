using System;

namespace PontBascule.Models
{
    public class WeighingOperation
    {
        public int Id { get; set; }

        public string TicketNumber { get; set; } = string.Empty;

        public EntryType EntryType { get; set; }
        public OperationStatus Status { get; set; } = OperationStatus.Open;

        public string TruckNumber { get; set; } = string.Empty;

        public string Transporter { get; set; } = string.Empty;

        public string Product { get; set; } = string.Empty;

        public string SageID { get; set; } = string.Empty;

        public string CustomerCode { get; set; } = string.Empty;

        public string SupplierCode { get; set; } = string.Empty;

        public string DriverName { get; set; } = string.Empty;

        public decimal? SimpleWeight { get; set; }

        public decimal TotalNetWeight { get; set; }

        public SageSyncStatus SageSyncStatus { get; set; } = SageSyncStatus.NotSynced;

        public string? SageDocumentNumber { get; set; }

        public string? DigitalTicketPath { get; set; }

        public DateTime? DigitalTicketGeneratedAt { get; set; }

        public string? PaperlessDocumentId { get; set; }

        public string? PaperlessDocumentUrl { get; set; }

        public DateTime? PaperlessUploadedAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }
    }
}
