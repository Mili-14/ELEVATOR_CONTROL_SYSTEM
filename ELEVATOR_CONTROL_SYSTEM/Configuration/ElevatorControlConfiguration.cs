using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELEVATOR_CONTROL_SYSTEM.Configuration
{
    // Configuration
    public static class BuildingConfiguration
    {
        public const int TotalFloors = 10;
        public const int TotalElevators = 4;
        public const int FloorTravelTimeMs = 10000; // 10 seconds
        public const int DoorOperationTimeMs = 10000; // 10 seconds
        public const int SimulationSpeedMultiplier = 100; // Speed up for demo (100x faster)
    }

    public static class TimeConfiguration
    {
        public static int FloorTravelTime => BuildingConfiguration.FloorTravelTimeMs / BuildingConfiguration.SimulationSpeedMultiplier;
        public static int DoorOperationTime => BuildingConfiguration.DoorOperationTimeMs / BuildingConfiguration.SimulationSpeedMultiplier;
    }
}
