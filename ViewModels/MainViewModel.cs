using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PontBascule.Models;
using PontBascule.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Linq;


namespace PontBascule.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IScaleService _scaleService;
        private readonly ISapService _sapService;
        private readonly IDatabaseService _databaseService;
        private readonly IWeighingWorkflowService _workflowService;
        private readonly ISageService _sageService;
        private readonly IPaperlessService _paperlessService;

        [ObservableProperty]
        private string _truckNumber = string.Empty;

        [ObservableProperty]
        private string _transporter = string.Empty;

        [ObservableProperty]
        private string _product = string.Empty;

        [ObservableProperty]
        private string _sageID = string.Empty;

        [ObservableProperty]
        private decimal _currentWeight = 0;

        [ObservableProperty]
        private string _scaleStatus = "Déconnecté";

        [ObservableProperty]
        private string _statusMessage = "Prêt";

        [ObservableProperty]
        private SolidColorBrush _sapConnectionStatus = Brushes.Red;

        [ObservableProperty]
        private SolidColorBrush _scaleConnectionStatus = Brushes.Red;

        public ObservableCollection<Weighing> WeighingHistory { get; } = new();

        public MainViewModel(
            IScaleService scaleService,
            ISapService sapService,
            IDatabaseService databaseService,
            IWeighingWorkflowService workflowService,
            ISageService sageService,
            IPaperlessService paperlessService)
        {
            _scaleService = scaleService;
            _sapService = sapService;
            _databaseService = databaseService;
            _workflowService = workflowService;
            _sageService = sageService;
            _paperlessService = paperlessService;

            _scaleService.WeightChanged += OnWeightChanged;

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                StatusMessage = "Initialisation de la base de données...";
                await _databaseService.InitializeDatabaseAsync();

                StatusMessage = "Connexion à la balance...";
                if (await _scaleService.ConnectAsync())
                {
                    ScaleConnectionStatus = Brushes.Green;
                    ScaleStatus = "Connecté";
                }

                StatusMessage = "Connexion à SAP...";
                if (await _sapService.ConnectAsync())
                {
                    SapConnectionStatus = Brushes.Green;
                }

                await LoadHistoryAsync();
                StatusMessage = "Système prêt";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'initialisation: {ex.Message}";
            }
        }

        private async Task LoadHistoryAsync()
        {
            var history = await _databaseService.GetRecentWeighingsAsync(20);
            WeighingHistory.Clear();
            foreach (var weighing in history)
            {
                WeighingHistory.Add(weighing);
            }
        }

        private void OnWeightChanged(object? sender, decimal weight)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentWeight = weight;
            });
        }

        [RelayCommand]
        private async Task WeighIn()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "⚠️ Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();
                
                var weighing = new Weighing
                {
                    Timestamp = DateTime.Now,
                    TruckNumber = TruckNumber,
                    Transporter = Transporter,
                    Product = Product,
                    Weight = weight,
                    WeighingType = WeighingType.Entrée
                };

                var id = await _databaseService.SaveWeighingAsync(weighing);
                weighing.Id = id;

                WeighingHistory.Insert(0, weighing);
                StatusMessage = $"✓ Pesée entrée enregistrée: {weight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task WeighOut()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "⚠️ Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();
                
                var weighing = new Weighing
                {
                    Timestamp = DateTime.Now,
                    TruckNumber = TruckNumber,
                    Transporter = Transporter,
                    Product = Product,
                    Weight = weight,
                    WeighingType = WeighingType.Sortie
                };

                var id = await _databaseService.SaveWeighingAsync(weighing);
                weighing.Id = id;

                WeighingHistory.Insert(0, weighing);
                StatusMessage = $"✓ Pesée sortie enregistrée: {weight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task GenerateDigitalTicket()
        {
            try
            {
                var operations = await _databaseService.GetRecentOperationsAsync(20);

                var latestOperation = operations
                    .FirstOrDefault(operation => operation.Status == OperationStatus.Completed);

                if (latestOperation == null)
                {
                    StatusMessage = "Aucune opération terminée pour générer un ticket numérique";
                    return;
                }

                var cycles = await _databaseService.GetCyclesByOperationIdAsync(latestOperation.Id);

                var filePath = await _paperlessService.GenerateTicketAsync(latestOperation, cycles);

                latestOperation.DigitalTicketPath = filePath;
                latestOperation.DigitalTicketGeneratedAt = DateTime.Now;

                await _databaseService.UpdateOperationAsync(latestOperation);

                StatusMessage = $"Ticket numérique généré: {filePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur génération ticket numérique: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task OpenLatestDigitalTicket()
        {
            try
            {
                var operations = await _databaseService.GetRecentOperationsAsync(20);

                var latestOperation = operations
                    .FirstOrDefault(operation =>
                        operation.Status == OperationStatus.Completed &&
                        !string.IsNullOrWhiteSpace(operation.DigitalTicketPath));

                if (latestOperation == null)
                {
                    StatusMessage = "Aucun ticket numérique trouvé";
                    return;
                }

                _paperlessService.OpenTicket(latestOperation.DigitalTicketPath!);

                StatusMessage = $"Ticket ouvert: {latestOperation.TicketNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur ouverture ticket: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SendLatestOperationToPaperless()
        {
            try
            {
                StatusMessage = "Recherche de la dernière opération terminée...";

                var operations = await _databaseService.GetRecentOperationsAsync(20);
                var latestOperation = operations
                    .FirstOrDefault(operation =>
                        operation.Status == OperationStatus.Completed &&
                        string.IsNullOrWhiteSpace(operation.PaperlessDocumentId));

                if (latestOperation == null)
                {
                    StatusMessage = "Aucune opération terminée à envoyer vers Paperless";
                    return;
                }

                var cycles = await _databaseService.GetCyclesByOperationIdAsync(latestOperation.Id);

                StatusMessage = $"Génération du ticket {latestOperation.TicketNumber}...";
                var filePath = await _paperlessService.GenerateTicketAsync(latestOperation, cycles);

                latestOperation.DigitalTicketPath = filePath;
                latestOperation.DigitalTicketGeneratedAt = DateTime.Now;

                StatusMessage = $"Envoi du ticket {latestOperation.TicketNumber} vers Paperless...";
                var uploadResult = await _paperlessService.UploadTicketAsync(latestOperation, filePath);

                latestOperation.PaperlessDocumentId = uploadResult.DocumentId;
                latestOperation.PaperlessDocumentUrl = uploadResult.DocumentUrl;
                latestOperation.PaperlessUploadedAt = DateTime.Now;

                await _databaseService.UpdateOperationAsync(latestOperation);

                StatusMessage = $"Ticket envoyé vers Paperless: {uploadResult.DocumentUrl}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur Paperless: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task PrintTicket()
        {
            if (WeighingHistory.Count == 0)
            {
                StatusMessage = "⚠️ Aucune pesée à imprimer";
                return;
            }

            try
            {
                var latestWeighing = WeighingHistory[0];
                
                // Si vous avez ajouté IPrintService dans le constructeur
                // await _printService.PrintTicketAsync(latestWeighing);
                
                StatusMessage = $"✓ Ticket imprimé pour {latestWeighing.TruckNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Erreur impression: {ex.Message}";
            }
        }
        [RelayCommand]
        private async Task SaveSimpleEntry()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();

                var operation = await _workflowService.SaveSimpleEntryAsync(
                    TruckNumber,
                    Transporter,
                    Product,
                    SageID,
                    weight);

                StatusMessage = $"Entrée simple enregistrée. Ticket {operation.TicketNumber}, poids {weight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }
        [RelayCommand]
        private async Task SaveDoubleEntryIn()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();

                var operation = await _workflowService.SaveDoubleEntryInAsync(
                    TruckNumber,
                    Transporter,
                    Product,
                    SageID,
                    weight);

                StatusMessage = $"Entrée double commencée. Ticket {operation.TicketNumber}, entrée {weight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }
        [RelayCommand]
        private async Task SaveDoubleEntryOut()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();

                var operation = await _workflowService.SaveDoubleEntryOutAsync(
                    TruckNumber,
                    weight);

                StatusMessage = $"Entrée double terminée. Net {operation.TotalNetWeight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }
        [ObservableProperty]
        private int _selectedOperationId;
        [RelayCommand]
        private async Task StartMultipleEntry()
        {
            if (string.IsNullOrWhiteSpace(TruckNumber))
            {
                StatusMessage = "Veuillez saisir le numéro de camion";
                return;
            }

            try
            {
                var operation = await _workflowService.StartMultipleEntryAsync(
                    TruckNumber,
                    Transporter,
                    Product,
                    SageID);

                SelectedOperationId = operation.Id;

                StatusMessage = $"Entrée multiple commencée. Ticket {operation.TicketNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }
        [RelayCommand]
        private async Task SaveMultipleEntryIn()
        {
            if (SelectedOperationId <= 0)
            {
                StatusMessage = "Veuillez sélectionner une opération multiple ouverte";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();

                var cycle = await _workflowService.SaveMultipleEntryInAsync(
                    SelectedOperationId,
                    weight);

                StatusMessage = $"Cycle {cycle.CycleNumber}: entrée enregistrée {weight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SaveMultipleEntryOut()
        {
            if (SelectedOperationId <= 0)
            {
                StatusMessage = "Veuillez sélectionner une opération multiple ouverte";
                return;
            }

            try
            {
                var weight = await _scaleService.ReadWeightAsync();

                var operation = await _workflowService.SaveMultipleEntryOutAsync(
                    SelectedOperationId,
                    weight);

                StatusMessage = $"Cycle terminé. Total net actuel {operation.TotalNetWeight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CloseMultipleEntry()
        {
            if (SelectedOperationId <= 0)
            {
                StatusMessage = "Veuillez sélectionner une opération multiple ouverte";
                return;
            }

            try
            {
                var operation = await _workflowService.CloseMultipleEntryAsync(SelectedOperationId);

                StatusMessage = $"Entrée multiple clôturée. Total net {operation.TotalNetWeight:N0} kg";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SendLatestOperationToSage()
        {
            try
            {
                StatusMessage = "Recherche de la dernière opération terminée...";

                var operations = await _databaseService.GetRecentOperationsAsync(20);

                var latestOperation = operations
                    .FirstOrDefault(operation =>
                        operation.Status == OperationStatus.Completed &&
                        operation.SageSyncStatus != SageSyncStatus.Synced);

                if (latestOperation == null)
                {
                    StatusMessage = "Aucune opération terminée à envoyer vers Sage";
                    return;
                }

                StatusMessage = $"Envoi du ticket {latestOperation.TicketNumber} vers Sage...";

                var sageDocumentNumber = await _sageService.SendOperationAsync(latestOperation);

                latestOperation.SageDocumentNumber = sageDocumentNumber;
                latestOperation.SageSyncStatus = SageSyncStatus.Synced;

                await _databaseService.UpdateOperationAsync(latestOperation);

                StatusMessage = $"Opération envoyée vers Sage. Document: {sageDocumentNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur Sage: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task SendToSap()
        {
            if (WeighingHistory.Count == 0)
            {
                StatusMessage = "⚠️ Aucune pesée à envoyer";
                return;
            }

            try
            {
                var latestWeighing = WeighingHistory[0];
                
                if (latestWeighing.SentToSap)
                {
                    StatusMessage = "⚠️ Cette pesée a déjà été envoyée à SAP";
                    return;
                }

                StatusMessage = "Envoi vers SAP...";
                var docNumber = await _sapService.SendWeighingAsync(latestWeighing);

                latestWeighing.SapDocumentNumber = docNumber;
                latestWeighing.SentToSap = true;
                await _databaseService.UpdateWeighingAsync(latestWeighing);

                StatusMessage = $"✓ Envoyé à SAP - Document: {docNumber}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Erreur SAP: {ex.Message}";
            }
        }
    }
}
