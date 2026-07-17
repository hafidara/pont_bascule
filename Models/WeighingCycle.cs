using System;

namespace PontBascule.Models
{
    public class WeighingCycle
    {
        public int Id { get; set; }

        public int OperationId { get; set; }

        public int CycleNumber { get; set; }

        public decimal? EntryWeight { get; set; }

        public DateTime? EntryTime { get; set; }

        public decimal? ExitWeight { get; set; }

        public DateTime? ExitTime { get; set; }

        public decimal NetWeight { get; set; }

        public bool IsCompleted => EntryWeight.HasValue && ExitWeight.HasValue;
    }
}