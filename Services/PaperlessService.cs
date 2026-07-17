using Microsoft.Extensions.Configuration;
using PontBascule.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public class PaperlessService : IPaperlessService
    {
        private readonly HttpClient _httpClient;
        private readonly PaperlessConfiguration _config;

        public PaperlessService(IConfiguration configuration)
        {
            _config = configuration.GetSection("Paperless").Get<PaperlessConfiguration>()
                      ?? new PaperlessConfiguration();

            _httpClient = new HttpClient();

            if (!string.IsNullOrWhiteSpace(_config.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(_config.BaseUrl.TrimEnd('/') + "/");
            }

            if (!string.IsNullOrWhiteSpace(_config.ApiToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Token", _config.ApiToken);
            }
        }

        public Task<string> GenerateTicketAsync(
            WeighingOperation operation,
            IReadOnlyList<WeighingCycle> cycles)
        {
            var ticketsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PontBascule",
                "Tickets");

            Directory.CreateDirectory(ticketsDirectory);

            var fileName = $"{operation.TicketNumber}.pdf";
            var filePath = Path.Combine(ticketsDirectory, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);

                    page.Header()
                        .AlignCenter()
                        .Text("Ticket de Pesée")
                        .FontSize(22)
                        .Bold();

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text($"Ticket: {operation.TicketNumber}");
                        column.Item().Text($"Type: {operation.EntryType}");
                        column.Item().Text($"Camion: {operation.TruckNumber}");
                        column.Item().Text($"Transporteur: {operation.Transporter}");
                        column.Item().Text($"Produit: {operation.Product}");
                        column.Item().Text("Indicateur: Sauraus IND200+RS232 - Certificat 0200-NAWI-03258");
                        column.Item().Text($"Date création: {operation.CreatedAt:dd/MM/yyyy HH:mm}");

                        if (operation.CompletedAt.HasValue)
                            column.Item().Text($"Date clôture: {operation.CompletedAt:dd/MM/yyyy HH:mm}");

                        column.Item().LineHorizontal(1);

                        if (operation.EntryType == EntryType.Simple)
                        {
                            column.Item().Text($"Poids: {operation.SimpleWeight:N0} kg")
                                .FontSize(16)
                                .Bold();
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Cycle").Bold();
                                    header.Cell().Text("Entrée").Bold();
                                    header.Cell().Text("Sortie").Bold();
                                    header.Cell().Text("Net").Bold();
                                });

                                foreach (var cycle in cycles)
                                {
                                    table.Cell().Text(cycle.CycleNumber.ToString());
                                    table.Cell().Text($"{cycle.EntryWeight:N0} kg");
                                    table.Cell().Text($"{cycle.ExitWeight:N0} kg");
                                    table.Cell().Text($"{cycle.NetWeight:N0} kg");
                                }
                            });
                        }

                        column.Item().LineHorizontal(1);

                        column.Item().Text($"Poids net total: {operation.TotalNetWeight:N0} kg")
                            .FontSize(18)
                            .Bold();

                        column.Item().Text($"Document Sage: {operation.SageDocumentNumber ?? "Non synchronisé"}");

                        column.Item().PaddingTop(20).Text("Document généré électroniquement.")
                            .Italic()
                            .FontSize(10);
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Pont Bascule - ");
                        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });
                });
            }).GeneratePdf(filePath);

            return Task.FromResult(filePath);
        }

        public async Task<PaperlessUploadResult> UploadTicketAsync(
            WeighingOperation operation,
            string filePath)
        {
            if (_httpClient.BaseAddress == null)
                throw new InvalidOperationException("URL Paperless non configurée.");

            if (string.IsNullOrWhiteSpace(_config.ApiToken))
                throw new InvalidOperationException("Token API Paperless non configuré.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Ticket PDF introuvable.", filePath);

            await using var fileStream = File.OpenRead(filePath);
            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "document", Path.GetFileName(filePath));
            form.Add(new StringContent(operation.TicketNumber), "title");
            form.Add(new StringContent(operation.CreatedAt.ToString("yyyy-MM-dd")), "created");
            form.Add(new StringContent(BuildArchiveSerialNumber(operation)), "archive_serial_number");

            if (!string.IsNullOrWhiteSpace(_config.Correspondent))
                form.Add(new StringContent(_config.Correspondent), "correspondent");

            if (!string.IsNullOrWhiteSpace(_config.DocumentType))
                form.Add(new StringContent(_config.DocumentType), "document_type");

            if (!string.IsNullOrWhiteSpace(_config.Tag))
                form.Add(new StringContent(_config.Tag), "tags");

            var response = await _httpClient.PostAsync("api/documents/post_document/", form);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Erreur Paperless {(int)response.StatusCode}: {responseBody}");
            }

            var documentId = ExtractPaperlessId(responseBody);
            var documentUrl = string.IsNullOrWhiteSpace(documentId)
                ? _config.BaseUrl.TrimEnd('/') + "/documents/"
                : _config.BaseUrl.TrimEnd('/') + $"/documents/{documentId}/details";

            return new PaperlessUploadResult
            {
                DocumentId = documentId,
                DocumentUrl = documentUrl
            };
        }

        public void OpenTicket(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Ticket PDF introuvable.", filePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }

        private static string BuildArchiveSerialNumber(WeighingOperation operation)
        {
            return $"PB-{operation.Id}-{operation.TicketNumber}";
        }

        private static string ExtractPaperlessId(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return string.Empty;

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Number)
                    return root.GetInt32().ToString();

                if (root.ValueKind == JsonValueKind.String)
                    return root.GetString() ?? string.Empty;

                if (root.TryGetProperty("id", out var id))
                    return id.ToString();

                if (root.TryGetProperty("document", out var documentId))
                    return documentId.ToString();

                if (root.TryGetProperty("task_id", out var taskId))
                    return taskId.GetString() ?? taskId.ToString();
            }
            catch (JsonException)
            {
                return responseBody.Trim();
            }

            return responseBody.Trim();
        }
    }
}
