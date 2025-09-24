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

        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== Professional Elevator Control System ===");
            Console.WriteLine($"Building Configuration: {BuildingConfiguration.TotalFloors} floors, {BuildingConfiguration.TotalElevators} elevators");
            Console.WriteLine($"Simulation Speed: {BuildingConfiguration.SimulationSpeedMultiplier}x faster than real-time");
            Console.WriteLine("Press 'q' to quit, 's' to show system status\n");

            InitializeServices();

            _cancellationTokenSource = new CancellationTokenSource();

            // Start the elevator controller
            var controllerTask = _elevatorController.StartAsync(_cancellationTokenSource.Token);

            // Start random request generation
            var requestTask = GenerateRandomRequestsAsync(_cancellationTokenSource.Token);

            // Handle user input
            var inputTask = HandleUserInputAsync(_cancellationTokenSource.Token);

            try
            {
                await Task.WhenAny(controllerTask, requestTask, inputTask);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            finally
            {
                _elevatorController.Stop();
                Console.WriteLine("\nElevator system stopped.");
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

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = _requestGenerator.GenerateRandomRequest();
                    await _elevatorController.ProcessRequestAsync(request);

                    // Random delay between requests (2-8 seconds in simulation time)
                    var delayMs = random.Next(200, 800); // 2-8 seconds at 100x speed
                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static async Task HandleUserInputAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
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

                    await Task.Delay(100, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}