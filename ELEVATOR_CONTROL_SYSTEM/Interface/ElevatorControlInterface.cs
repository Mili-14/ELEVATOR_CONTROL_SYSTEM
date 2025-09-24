using ELEVATOR_CONTROL_SYSTEM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELEVATOR_CONTROL_SYSTEM.Interface
{
    // Interfaces
    public interface IElevatorController
    {
        Task ProcessRequestAsync(ElevatorRequest request);
        IReadOnlyList<Elevator> GetElevators();
        Task StartAsync(CancellationToken cancellationToken);
        void Stop();
    }

    public interface IRequestDispatcher
    {
        Elevator SelectElevatorForRequest(ElevatorRequest request, IReadOnlyList<Elevator> elevators);
    }

    public interface IElevatorService
    {
        Task MoveElevatorAsync(Elevator elevator, CancellationToken cancellationToken);
    }

    public interface ILoggingService
    {
        void LogRequest(ElevatorRequest request);
        void LogElevatorStatus(Elevator elevator);
        void LogElevatorMovement(int elevatorId, int fromFloor, int toFloor);
        void LogElevatorArrival(int elevatorId, int floor);
        void LogSystemStatus(IReadOnlyList<Elevator> elevators);
    }

    public interface IRandomRequestGenerator
    {
        ElevatorRequest GenerateRandomRequest();
    }
}
