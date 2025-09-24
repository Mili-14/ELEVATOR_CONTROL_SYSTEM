using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using ELEVATOR_CONTROL_SYSTEM.Models;
using ELEVATOR_CONTROL_SYSTEM.Configuration;
using ELEVATOR_CONTROL_SYSTEM.Interface;
using ELEVATOR_CONTROL_SYSTEM.Services;
using ELEVATOR_CONTROL_SYSTEM.Controller;

namespace ElevatorControlSystem
{
    // Main Application
    public class Program
    {
        private static IElevatorController _elevatorController;
        private static IRandomRequestGenerator _requestGenerator;
        private static ILoggingService _logger;
        private static CancellationTokenSource _cancellationTokenSource;
        private static volatile bool _isRunning = false;

        public static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== Professional Elevator Control System ===");
                Console.WriteLine($"Building Configuration: {BuildingConfiguration.TotalFloors} floors, {BuildingConfiguration.TotalElevators} elevators");
                Console.WriteLine($"Simulation Speed: {BuildingConfiguration.SimulationSpeedMultiplier}x faster than real-time");
                Console.WriteLine("Press 'q' to quit, 's' to show system status\n");

                InitializeServices();

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                // Handle Ctrl+C gracefully
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\nShutting down gracefully...");
                    _cancellationTokenSource.Cancel();
                };

                var controllerTask = _elevatorController.StartAsync(_cancellationTokenSource.Token);
                var requestTask = GenerateRandomRequestsAsync(_cancellationTokenSource.Token);
                var inputTask = HandleUserInputAsync(_cancellationTokenSource.Token);

                await Task.WhenAny(controllerTask, requestTask, inputTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application error: {ex.Message}");
            }
            finally
            {
                try
                {
                    _isRunning = false;
                    _elevatorController?.Stop();
                    Console.WriteLine("\nElevator system stopped.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Shutdown error: {ex.Message}");
                }
            }
        }

        private static void InitializeServices()
        {
            _logger = new ConsoleLoggingService();
            var dispatcher = new OptimalRequestDispatcher();
            var elevatorService = new ElevatorService(_logger);
            _elevatorController = new ElevatorController(dispatcher, elevatorService, _logger);
            _requestGenerator = new RandomRequestGenerator();
        }

        private static async Task GenerateRandomRequestsAsync(CancellationToken cancellationToken)
        {
            var random = new Random();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        var request = _requestGenerator.GenerateRandomRequest();
                        await _elevatorController.ProcessRequestAsync(request);

                        var delayMs = random.Next(200, 800);
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Request generation error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken); // Wait before retry
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        private static async Task HandleUserInputAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    try
                    {
                        // Check if input is available (Mac compatible)
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);

                            switch (key.KeyChar)
                            {
                                case 'q':
                                case 'Q':
                                    _cancellationTokenSource.Cancel();
                                    return;
                                case 's':
                                case 'S':
                                    _logger.LogSystemStatus(_elevatorController.GetElevators());
                                    break;
                            }
                        }

                        await Task.Delay(100, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Input handling error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }
}