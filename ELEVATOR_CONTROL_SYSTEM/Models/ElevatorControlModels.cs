using ELEVATOR_CONTROL_SYSTEM.Configuration;
using ElevatorControlSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELEVATOR_CONTROL_SYSTEM.Models
{
    // Domain Models
    public enum ElevatorDirection
    {
        Up,
        Down,
        Idle
    }

    public enum RequestType
    {
        Up,
        Down,
        Destination
    }

    public class ElevatorRequest
    {
        public int Floor { get; set; }
        public RequestType Type { get; set; }
        public DateTime RequestTime { get; set; }
        public int? DestinationFloor { get; set; }

        public ElevatorRequest(int floor, RequestType type, int? destinationFloor = null)
        {
            Floor = floor;
            Type = type;
            DestinationFloor = destinationFloor;
            RequestTime = DateTime.UtcNow;
        }
    }

    public class Elevator
    {
        public int Id { get; }
        public int CurrentFloor { get; private set; }
        public ElevatorDirection Direction { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsDoorOpen { get; private set; }
        public HashSet<int> DestinationFloors { get; private set; }

        public Elevator(int id, int initialFloor = 1)
        {
            Id = id;
            CurrentFloor = initialFloor;
            Direction = ElevatorDirection.Idle;
            IsMoving = false;
            IsDoorOpen = false;
            DestinationFloors = new HashSet<int>();
        }

        public void AddDestination(int floor)
        {
            if (floor >= 1 && floor <= BuildingConfiguration.TotalFloors && floor != CurrentFloor)
            {
                DestinationFloors.Add(floor);
            }
        }

        public void RemoveDestination(int floor)
        {
            DestinationFloors.Remove(floor);
        }

        public void SetDirection(ElevatorDirection direction)
        {
            Direction = direction;
        }

        public void SetMoving(bool moving)
        {
            IsMoving = moving;
        }

        public void SetDoorOpen(bool doorOpen)
        {
            IsDoorOpen = doorOpen;
        }

        public void MoveToFloor(int floor)
        {
            CurrentFloor = floor;
        }

        public bool HasDestinations()
        {
            return DestinationFloors.Any();
        }

        public int? GetNextDestination()
        {
            if (!HasDestinations()) return null;

            return Direction switch
            {
                ElevatorDirection.Up => DestinationFloors.Where(f => f > CurrentFloor).DefaultIfEmpty().Min(),
                ElevatorDirection.Down => DestinationFloors.Where(f => f < CurrentFloor).DefaultIfEmpty().Max(),
                _ => DestinationFloors.OrderBy(f => Math.Abs(f - CurrentFloor)).First()
            };
        }

        public override string ToString()
        {
            var destinations = DestinationFloors.Any() ? $"[{string.Join(",", DestinationFloors.OrderBy(x => x))}]" : "[]";
            return $"Elevator {Id}: Floor {CurrentFloor}, Direction {Direction}, Moving {IsMoving}, Destinations {destinations}";
        }
    }
}
