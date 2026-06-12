namespace DormManagement.Models
{
    public class Room
    {
        public int RoomId { get; set; }
        public int BuildingId { get; set; }
        public string RoomNo { get; set; } = "";
        public int Capacity { get; set; }
        public int OccupiedCount { get; set; }
        public string Status { get; set; } = "";
    }
}
