using PontBascule.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public class WeighingWorkflowService : IWeighingWorkflowService
    {
        private readonly IDatabaseService _databaseService;

        public WeighingWorkflowService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<WeighingOperation> SaveSimpleEntryAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID,
            decimal weight)
        {
            var operation = new WeighingOperation
            {
                TicketNumber = GenerateTicketNumber(),
                EntryType = EntryType.Simple,
                Status = OperationStatus.Completed,
                TruckNumber = truckNumber,
                Transporter = transporter,
                Product = product,
                SageID = sageID,
                SimpleWeight = weight,
                TotalNetWeight = weight,
                CreatedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            };

            operation.Id = await _databaseService.CreateOperationAsync(operation);
            return operation;
        }

        public async Task<WeighingOperation> SaveDoubleEntryInAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID,
            decimal weight)
        {
            var existing = await _databaseService.GetOpenOperationByTruckAsync(truckNumber);
            if (existing != null)
                throw new InvalidOperationException("Ce camion a déjà une opération ouverte.");

            var operation = new WeighingOperation
            {
                TicketNumber = GenerateTicketNumber(),
                EntryType = EntryType.Double,
                Status = OperationStatus.PendingExit,
                TruckNumber = truckNumber,
                Transporter = transporter,
                Product = product,
                SageID = sageID,
                CreatedAt = DateTime.Now
            };

            operation.Id = await _databaseService.CreateOperationAsync(operation);

            var cycle = new WeighingCycle
            {
                OperationId = operation.Id,
                CycleNumber = 1,
                EntryWeight = weight,
                EntryTime = DateTime.Now
            };

            await _databaseService.CreateCycleAsync(cycle);

            return operation;
        }

        public async Task<WeighingOperation> SaveDoubleEntryOutAsync(
            string truckNumber,
            decimal weight)
        {
            var operation = await _databaseService.GetOpenOperationByTruckAsync(truckNumber);
            if (operation == null)
                throw new InvalidOperationException("Aucune entrée ouverte trouvée pour ce camion.");

            if (operation.EntryType != EntryType.Double)
                throw new InvalidOperationException("L'opération ouverte n'est pas une double entrée.");

            var cycle = await _databaseService.GetLastOpenCycleAsync(operation.Id);
            if (cycle == null || !cycle.EntryWeight.HasValue)
                throw new InvalidOperationException("Aucune pesée d'entrée trouvée.");

            cycle.ExitWeight = weight;
            cycle.ExitTime = DateTime.Now;
            cycle.NetWeight = Math.Abs(cycle.EntryWeight.Value - cycle.ExitWeight.Value);

            await _databaseService.UpdateCycleAsync(cycle);

            operation.TotalNetWeight = cycle.NetWeight;
            operation.Status = OperationStatus.Completed;
            operation.CompletedAt = DateTime.Now;

            await _databaseService.UpdateOperationAsync(operation);

            return operation;
        }

        public async Task<WeighingOperation> StartMultipleEntryAsync(
            string truckNumber,
            string transporter,
            string product,
            string sageID)
        {
            var existing = await _databaseService.GetOpenOperationByTruckAsync(truckNumber);
            if (existing != null)
                throw new InvalidOperationException("Ce camion a déjà une opération ouverte.");

            var operation = new WeighingOperation
            {
                TicketNumber = GenerateTicketNumber(),
                EntryType = EntryType.Multiple,
                Status = OperationStatus.Open,
                TruckNumber = truckNumber,
                Transporter = transporter,
                Product = product,
                SageID = sageID,
                CreatedAt = DateTime.Now
            };

            operation.Id = await _databaseService.CreateOperationAsync(operation);
            return operation;
        }

        public async Task<WeighingCycle> SaveMultipleEntryInAsync(int operationId, decimal weight)
        {
            var operation = await _databaseService.GetOperationByIdAsync(operationId);
            if (operation == null)
                throw new InvalidOperationException("Opération introuvable.");

            if (operation.EntryType != EntryType.Multiple)
                throw new InvalidOperationException("Cette opération n'est pas une entrée multiple.");

            var openCycle = await _databaseService.GetLastOpenCycleAsync(operationId);
            if (openCycle != null)
                throw new InvalidOperationException("Un cycle est déjà ouvert. Enregistrez d'abord la sortie.");

            var cycles = await _databaseService.GetCyclesByOperationIdAsync(operationId);

            var cycle = new WeighingCycle
            {
                OperationId = operationId,
                CycleNumber = cycles.Count + 1,
                EntryWeight = weight,
                EntryTime = DateTime.Now
            };

            cycle.Id = await _databaseService.CreateCycleAsync(cycle);
            return cycle;
        }

        public async Task<WeighingOperation> SaveMultipleEntryOutAsync(int operationId, decimal weight)
        {
            var operation = await _databaseService.GetOperationByIdAsync(operationId);
            if (operation == null)
                throw new InvalidOperationException("Opération introuvable.");

            var cycle = await _databaseService.GetLastOpenCycleAsync(operationId);
            if (cycle == null || !cycle.EntryWeight.HasValue)
                throw new InvalidOperationException("Aucun cycle ouvert trouvé.");

            cycle.ExitWeight = weight;
            cycle.ExitTime = DateTime.Now;
            cycle.NetWeight = Math.Abs(cycle.EntryWeight.Value - cycle.ExitWeight.Value);

            await _databaseService.UpdateCycleAsync(cycle);

            var cycles = await _databaseService.GetCyclesByOperationIdAsync(operationId);
            operation.TotalNetWeight = cycles.Sum(c => c.NetWeight);

            await _databaseService.UpdateOperationAsync(operation);

            return operation;
        }

        public async Task<WeighingOperation> CloseMultipleEntryAsync(int operationId)
        {
            var operation = await _databaseService.GetOperationByIdAsync(operationId);
            if (operation == null)
                throw new InvalidOperationException("Opération introuvable.");

            var openCycle = await _databaseService.GetLastOpenCycleAsync(operationId);
            if (openCycle != null)
                throw new InvalidOperationException("Impossible de fermer: un cycle entrée/sortie est incomplet.");

            operation.Status = OperationStatus.Completed;
            operation.CompletedAt = DateTime.Now;

            await _databaseService.UpdateOperationAsync(operation);
            return operation;
        }

        private static string GenerateTicketNumber()
        {
            return $"PB-{DateTime.Now:yyyyMMdd-HHmmss}";
        }
    }
}
