using Microsoft.Extensions.Configuration;
using PontBascule.Models;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace PontBascule.Services
{
    public class ScaleService : IScaleService, IDisposable
    {
        private SerialPort? _serialPort;
        private readonly ScaleConfiguration _config;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _readingTask;

        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public event EventHandler<decimal>? WeightChanged;

        public ScaleService(IConfiguration configuration)
        {
            _config = configuration.GetSection("Scale").Get<ScaleConfiguration>() 
                      ?? new ScaleConfiguration();
        }

        public Task<bool> ConnectAsync()
        {
            try
            {
                _serialPort = new SerialPort
                {
                    PortName = _config.PortName,
                    BaudRate = _config.BaudRate,
                    DataBits = _config.DataBits,
                    Parity = Enum.Parse<Parity>(_config.Parity),
                    StopBits = Enum.Parse<StopBits>(_config.StopBits),
                    ReadTimeout = _config.ReadTimeout
                };

                _serialPort.Open();

                // Démarrer la lecture en continu
                _cancellationTokenSource = new CancellationTokenSource();
                _readingTask = Task.Run(() => ContinuousRead(_cancellationTokenSource.Token));

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur connexion balance: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task DisconnectAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _readingTask?.Wait(TimeSpan.FromSeconds(2));
                
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                
                _serialPort?.Dispose();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur déconnexion balance: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private static bool IsStableWeight(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            var value = rawValue.ToUpperInvariant();

            if (value.Contains("US") || value.Contains("UNSTABLE"))
                return false;

            if (value.Contains("ST") || value.Contains("STABLE"))
                return true;

            return true;
        }

        private decimal ParseInd200Weight(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new InvalidOperationException("Réponse IND200 vide.");

            var stable = IsStableWeight(rawValue);

            if (_config.RequireStableWeight && !stable)
                throw new InvalidOperationException($"Poids instable ignoré: {rawValue}");

            var cleaned = rawValue
                .Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                .Replace("g", "", StringComparison.OrdinalIgnoreCase)
                .Replace("t", "", StringComparison.OrdinalIgnoreCase)
                .Replace("+", "")
                .Trim();

            var numericPart = new string(cleaned
                .Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-')
                .ToArray());

            numericPart = numericPart.Replace(',', '.');

            if (!decimal.TryParse(
                numericPart,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var weight))
            {
                throw new InvalidOperationException($"Format poids IND200 non reconnu: {rawValue}");
            }

            return weight;
        }

        private static decimal ParseWeight(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                throw new InvalidOperationException("Réponse balance vide.");

            var cleaned = rawValue
                .Replace("kg", "", StringComparison.OrdinalIgnoreCase)
                .Replace("KG", "", StringComparison.OrdinalIgnoreCase)
                .Replace("+", "")
                .Trim();

            var numericPart = new string(cleaned
                .Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-')
                .ToArray());

            numericPart = numericPart.Replace(',', '.');

            if (!decimal.TryParse(
                numericPart,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var weight))
            {
                throw new InvalidOperationException($"Format poids non reconnu: {rawValue}");
            }

            return weight;
        }

        public async Task<decimal> ReadWeightAsync()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Balance non connectée");

            try
            {
                return await Task.Run(() =>
                {
                    if (!_config.ContinuousMode && !string.IsNullOrWhiteSpace(_config.ReadCommand))
                    {
                        _serialPort!.WriteLine(_config.ReadCommand);
                    }

                    var line = _serialPort!.ReadLine();

                    return ParseInd200Weight(line);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lecture poids IND200: {ex.Message}");
                return 0;
            }
        }

        private async Task ContinuousRead(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var weight = await ReadWeightAsync();
                    WeightChanged?.Invoke(this, weight);
                    await Task.Delay(500, cancellationToken); // Lecture toutes les 500ms
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lecture continue: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }
}
