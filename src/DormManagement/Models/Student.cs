namespace DormManagement.Models
{
    public class Student
    {
        public int StudentId { get; set; }
        public string StudentNo { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Gender { get; set; }
        public int? ClassId { get; set; }
        public string? Phone { get; set; }
    }
}
