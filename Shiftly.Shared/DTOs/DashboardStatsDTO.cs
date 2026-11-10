namespace Shiftly.Shared.DTOs
{
    public class DashboardStatsDTO
    {
        public int ActiveWorkersCount { get; set; }
        public int PendingAvailabilitiesCount { get; set; }
        public int MorningRequestsCount { get; set; }
        public int EveningRequestsCount { get; set; }
        public int DefinedShiftsCount { get; set; }
    }
}

