using System;

namespace ITA.Topvisor
{
    public class ProjectInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? UpdateDateTime { get; set; }
        public int Status { get; set; }

        public override string ToString()
        {
            return String.Format("{0} ({1})", Name, Id);
        }
    }
}
