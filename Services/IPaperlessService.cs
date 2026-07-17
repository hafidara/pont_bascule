using PontBascule.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public interface IPaperlessService
    {
        Task<string> GenerateTicketAsync(
            WeighingOperation operation,
            IReadOnlyList<WeighingCycle> cycles);

        Task<PaperlessUploadResult> UploadTicketAsync(
            WeighingOperation operation,
            string filePath);

        void OpenTicket(string filePath);
    }

    public class PaperlessUploadResult
    {
        public string DocumentId { get; set; } = string.Empty;

        public string DocumentUrl { get; set; } = string.Empty;
    }
}
