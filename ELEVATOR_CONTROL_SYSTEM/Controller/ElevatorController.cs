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
        private readonly Queue<ElevatorRequest> _pendingRequests;
        private readonly object _lockObject = new object();
        private CancellationTokenSource _mainCancellationSource;

        public ElevatorController(IRequestDispatcher dispatcher, IElevatorService elevatorService, ILoggingService logger)
        {
            _elevators = new List<Elevator>();
            _dispatcher = dispatcher;
            _elevatorService = elevatorService;
            _logger = logger;
            _elevatorTasks = new Dictionary<int, Task>();
            _elevatorCancellationSources = new Dictionary<int, CancellationTokenSource>();
            _pendingRequests = new Queue<ElevatorRequest>();

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
            _logger.LogRequest(request);

            lock (_lockObject)
            {
                var selectedElevator = _dispatcher.SelectElevatorForRequest(request, _elevators);
                selectedElevator.AddDestination(request.Floor);

                // Add destination floor if it's a destination request
                if (request.Type == RequestType.Destination && request.DestinationFloor.HasValue)
                {
                    selectedElevator.AddDestination(request.DestinationFloor.Value);
                }

                // Start elevator task if not already running
                if (!_elevatorTasks.ContainsKey(selectedElevator.Id) || _elevatorTasks[selectedElevator.Id].IsCompleted)
                {
                    _elevatorCancellationSources[selectedElevator.Id] = new CancellationTokenSource();
                    _elevatorTasks[selectedElevator.Id] = Task.Run(() =>
                        _elevatorService.MoveElevatorAsync(selectedElevator, _elevatorCancellationSources[selectedElevator.Id].Token));
                }
            }
        }

        public IReadOnlyList<Elevator> GetElevators()
        {
            lock (_lockObject)
            {
                return _elevators.AsReadOnly();
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _mainCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _logger.LogSystemStatus(_elevators);

            // Keep the controller running
            while (!_mainCancellationSource.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, _mainCancellationSource.Token);
            }
        }

        public void Stop()
        {
            _mainCancellationSource?.Cancel();

            foreach (var cancellationSource in _elevatorCancellationSources.Values)
            {
                cancellationSource.Cancel();
            }

            Task.WaitAll(_elevatorTasks.Values.ToArray(), TimeSpan.FromSeconds(5));
        }
    }
}
