using ELEVATOR_CONTROL_SYSTEM.Configuration;
using ELEVATOR_CONTROL_SYSTEM.Interface;
using ELEVATOR_CONTROL_SYSTEM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELEVATOR_CONTROL_SYSTEM.Services
{
    // Services Implementation
    public class ConsoleLoggingService : ILoggingService
    {
        private readonly object _lockObject = new object();

        public void LogRequest(ElevatorRequest request)
        {
            lock (_lockObject)
            {
                try
                {
                    var requestType = request.Type == RequestType.Destination ? $"Destination to floor {request.DestinationFloor}" : $"{request.Type}";
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] REQUEST: {requestType} request on floor {request.Floor}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }

        public void LogElevatorStatus(Elevator elevator)
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] STATUS: {elevator}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }

        public void LogElevatorMovement(int elevatorId, int fromFloor, int toFloor)
        {
            lock (_lockObject)
            {
                try
                {
                    var direction = toFloor > fromFloor ? "UP" : "DOWN";
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MOVEMENT: Elevator {elevatorId} moving {direction} from floor {fromFloor} to floor {toFloor}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }

        public void LogElevatorArrival(int elevatorId, int floor)
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ARRIVAL: Elevator {elevatorId} arrived at floor {floor}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }

        public void LogSystemStatus(IReadOnlyList<Elevator> elevators)
        {
            lock (_lockObject)
            {
                try
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SYSTEM STATUS:");
                    foreach (var elevator in elevators)
                    {
                        Console.WriteLine($"  {elevator}");
                    }
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Logging error: {ex.Message}");
                }
            }
        }
    }

    public class RandomRequestGenerator : IRandomRequestGenerator
    {
        private readonly Random _random;

        public RandomRequestGenerator()
        {
            _random = new Random();
        }

        public ElevatorRequest GenerateRandomRequest()
        {
            try
            {
                var floor = _random.Next(1, BuildingConfiguration.TotalFloors + 1);
                var requestType = (RequestType)_random.Next(0, 2);

                if (_random.Next(0, 3) == 0)
                {
                    var destinationFloor = _random.Next(1, BuildingConfiguration.TotalFloors + 1);
                    while (destinationFloor == floor)
                    {
                        destinationFloor = _random.Next(1, BuildingConfiguration.TotalFloors + 1);
                    }
                    return new ElevatorRequest(floor, RequestType.Destination, destinationFloor);
                }

                if (floor == BuildingConfiguration.TotalFloors)
                    requestType = RequestType.Down;
                else if (floor == 1)
                    requestType = RequestType.Up;

                return new ElevatorRequest(floor, requestType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating request: {ex.Message}");
                return new ElevatorRequest(1, RequestType.Up);
            }
        }
    }

    public class OptimalRequestDispatcher : IRequestDispatcher
    {
        public Elevator SelectElevatorForRequest(ElevatorRequest request, IReadOnlyList<Elevator> elevators)
        {
            try
            {
                return elevators
                    .Select(elevator => new
                    {
                        Elevator = elevator,
                        Score = CalculateScore(elevator, request)
                    })
                    .OrderBy(x => x.Score)
                    .First()
                    .Elevator;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in dispatcher: {ex.Message}");
                return elevators.First();
            }
        }

        private double CalculateScore(Elevator elevator, ElevatorRequest request)
        {
            try
            {
                var distance = Math.Abs(elevator.CurrentFloor - request.Floor);
                var score = distance * 1.0;

                if (request.Type == RequestType.Up && elevator.Direction == ElevatorDirection.Up && elevator.CurrentFloor <= request.Floor)
                    score *= 0.5;
                else if (request.Type == RequestType.Down && elevator.Direction == ElevatorDirection.Down && elevator.CurrentFloor >= request.Floor)
                    score *= 0.5;

                if (elevator.Direction == ElevatorDirection.Idle)
                    score *= 0.8;

                score += elevator.DestinationFloors.Count * 2;

                return score;
            }
            catch (Exception)
            {
                return double.MaxValue;
            }
        }
    }

    public class ElevatorService : IElevatorService
    {
        private readonly ILoggingService _logger;

        public ElevatorService(ILoggingService logger)
        {
            _logger = logger;
        }

        public async Task MoveElevatorAsync(Elevator elevator, CancellationToken cancellationToken)
        {
            try
            {
                while (elevator.HasDestinations() && !cancellationToken.IsCancellationRequested)
                {
                    var nextDestination = elevator.GetNextDestination();
                    if (!nextDestination.HasValue) break;

                    var targetFloor = nextDestination.Value;
                    await MoveToFloorAsync(elevator, targetFloor, cancellationToken);

                    if (cancellationToken.IsCancellationRequested) break;

                    elevator.SetDoorOpen(true);
                    elevator.RemoveDestination(targetFloor);
                    _logger.LogElevatorArrival(elevator.Id, targetFloor);

                    await Task.Delay(TimeConfiguration.DoorOperationTime, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        elevator.SetDoorOpen(false);
                    }
                }

                if (!elevator.HasDestinations())
                {
                    elevator.SetDirection(ElevatorDirection.Idle);
                    elevator.SetMoving(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                elevator.SetMoving(false);
                elevator.SetDoorOpen(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in elevator service: {ex.Message}");
                elevator.SetMoving(false);
                elevator.SetDirection(ElevatorDirection.Idle);
            }
        }

        private async Task MoveToFloorAsync(Elevator elevator, int targetFloor, CancellationToken cancellationToken)
        {
            try
            {
                var currentFloor = elevator.CurrentFloor;

                if (currentFloor == targetFloor) return;

                var direction = targetFloor > currentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
                elevator.SetDirection(direction);
                elevator.SetMoving(true);

                _logger.LogElevatorMovement(elevator.Id, currentFloor, targetFloor);

                while (elevator.CurrentFloor != targetFloor && !cancellationToken.IsCancellationRequested)
                {
                    var nextFloor = direction == ElevatorDirection.Up ? elevator.CurrentFloor + 1 : elevator.CurrentFloor - 1;

                    await Task.Delay(TimeConfiguration.FloorTravelTime, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        elevator.MoveToFloor(nextFloor);

                        if (elevator.DestinationFloors.Contains(nextFloor) && nextFloor != targetFloor)
                        {
                            elevator.SetMoving(false);
                            elevator.SetDoorOpen(true);
                            elevator.RemoveDestination(nextFloor);
                            _logger.LogElevatorArrival(elevator.Id, nextFloor);

                            await Task.Delay(TimeConfiguration.DoorOperationTime, cancellationToken);

                            if (!cancellationToken.IsCancellationRequested)
                            {
                                elevator.SetDoorOpen(false);
                                elevator.SetMoving(true);
                            }
                        }
                    }
                }

                elevator.SetMoving(false);
            }
            catch (OperationCanceledException)
            {
                elevator.SetMoving(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving elevator: {ex.Message}");
                elevator.SetMoving(false);
            }
        }
    }
}
