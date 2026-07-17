using PontBascule.Models;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public interface IWeighingWorkflowService
    {
        Task<WeighingOperation> SaveSimpleEntryAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID,
            decimal weight);

        Task<WeighingOperation> SaveDoubleEntryInAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID,
            decimal weight);

        Task<WeighingOperation> SaveDoubleEntryOutAsync(
            string truckNumber,
            decimal weight);

        Task<WeighingOperation> StartMultipleEntryAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID);

        Task<WeighingCycle> SaveMultipleEntryInAsync(
            int operationId,
            decimal weight);

        Task<WeighingOperation> SaveMultipleEntryOutAsync(
            int operationId,
            decimal weight);

        Task<WeighingOperation> CloseMultipleEntryAsync(int operationId);
    }
}
