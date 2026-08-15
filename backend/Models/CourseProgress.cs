using System;
using System.Collections.Generic;

namespace EquipamentosMedicosApi.Models
{
    public class CourseProgress
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public double Percent { get; set; }
        public List<int> CompletedLessons { get; set; } = new();
        public DateTime? LastSeenAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public User? User { get; set; }
    }
}
