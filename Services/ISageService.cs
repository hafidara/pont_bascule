using PontBascule.Models;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public interface ISageService
    {
        Task<bool> TestConnectionAsync();

        Task<string> SendOperationAsync(WeighingOperation operation);
    }
}
