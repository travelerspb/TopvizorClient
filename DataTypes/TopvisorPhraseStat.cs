using System;

namespace ITA.Topvisor
{
    public sealed class TopvisorPhraseStat
    {
        public int ToVisId { get; set; }
        public string Text { get; set; }
        public string Url { get; set; }
        public DateTime Date { get; set; }
        public int? Position { get; set; }
    }
}