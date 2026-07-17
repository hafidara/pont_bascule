using PontBascule.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public interface IDatabaseService
    {
        Task InitializeDatabaseAsync();
        Task<int> SaveWeighingAsync(Weighing weighing);
        Task<Weighing?> GetWeighingByIdAsync(int id);
        Task<List<Weighing>> GetRecentWeighingsAsync(int count = 50);
        Task UpdateWeighingAsync(Weighing weighing);

        //new methods for WeighingOperation
        Task<int> CreateOperationAsync(WeighingOperation operation);
        Task UpdateOperationAsync(WeighingOperation operation);
        Task<WeighingOperation?> GetOperationByIdAsync(int id);
        Task<WeighingOperation?> GetOpenOperationByTruckAsync(string truckNumber);
        Task<List<WeighingOperation>> GetRecentOperationsAsync(int count = 50);

        Task<int> CreateCycleAsync(WeighingCycle cycle);
        Task UpdateCycleAsync(WeighingCycle cycle);
        Task<List<WeighingCycle>> GetCyclesByOperationIdAsync(int operationId);
        Task<WeighingCycle?> GetLastOpenCycleAsync(int operationId);
    }
}
