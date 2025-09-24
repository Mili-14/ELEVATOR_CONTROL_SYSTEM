using ELEVATOR_CONTROL_SYSTEM.Configuration;
using ELEVATOR_CONTROL_SYSTEM.Interface;
using ELEVATOR_CONTROL_SYSTEM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELEVATOR_CONTROL_SYSTEM.Controller
{
    public class ElevatorController : IElevatorController
    {
        private readonly List<Elevator> _elevators;
        private readonly IRequestDispatcher _dispatcher;
        private readonly IElevatorService _elevatorService;
        private readonly ILoggingService _logger;
        private readonly Dictionary<int, Task> _elevatorTasks;
        private readonly Dictionary<int, CancellationTokenSource> _elevatorCancellationSources;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _mainCancellationSource;
        private volatile bool _isRunning = false;

        public ElevatorController(IRequestDispatcher dispatcher, IElevatorService elevatorService, ILoggingService logger)
        {
            _elevators = new List<Elevator>();
            _dispatcher = dispatcher;
            _elevatorService = elevatorService;
            _logger = logger;
            _elevatorTasks = new Dictionary<int, Task>();
            _elevatorCancellationSources = new Dictionary<int, CancellationTokenSource>();

            InitializeElevators();
        }

        private void InitializeElevators()
        {
            for (int i = 1; i <= BuildingConfiguration.TotalElevators; i++)
            {
                var elevator = new Elevator(i);
                _elevators.Add(elevator);
                _elevatorCancellationSources[i] = new CancellationTokenSource();
            }
        }

        public async Task ProcessRequestAsync(ElevatorRequest request)
        {
            try
            {
                if (!_isRunning) return;

                _logger.LogRequest(request);

                lock (_lockObject)
                {
                    var selectedElevator = _dispatcher.SelectElevatorForRequest(request, _elevators);
                    selectedElevator.AddDestination(request.Floor);

                    if (request.Type == RequestType.Destination && request.DestinationFloor.HasValue)
                    {
                        selectedElevator.AddDestination(request.DestinationFloor.Value);
                    }

                    if (!_elevatorTasks.ContainsKey(selectedElevator.Id) || _elevatorTasks[selectedElevator.Id].IsCompleted)
                    {
                        if (_elevatorCancellationSources[selectedElevator.Id].IsCancellationRequested)
                        {
                            _elevatorCancellationSources[selectedElevator.Id] = new CancellationTokenSource();
                        }

                        _elevatorTasks[selectedElevator.Id] = Task.Run(async () =>
                        {
                            try
                            {
                                await _elevatorService.MoveElevatorAsync(selectedElevator, _elevatorCancellationSources[selectedElevator.Id].Token);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Elevator task error: {ex.Message}");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }

        public IReadOnlyList<Elevator> GetElevators()
        {
            lock (_lockObject)
            {
                return _elevators.ToList().AsReadOnly();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _isRunning = true;
                _mainCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _logger.LogSystemStatus(_elevators);

                while (!_mainCancellationSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _mainCancellationSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in controller: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void Stop()
        {
            try
            {
                _isRunning = false;
                _mainCancellationSource?.Cancel();

                foreach (var cancellationSource in _elevatorCancellationSources.Values)
                {
                    try
                    {
                        cancellationSource.Cancel();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cancelling elevator: {ex.Message}");
                    }
                }

                var tasks = _elevatorTasks.Values.ToArray();
                try
                {
                    Task.WaitAll(tasks, TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error waiting for tasks: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping controller: {ex.Message}");
            }
        }
    }
}
