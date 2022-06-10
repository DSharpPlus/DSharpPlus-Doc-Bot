using System.Collections.Generic;

namespace DocBot.src.XMLDocs
{
    public class XMLMember
    {
        public string? Name { get; set; }
        public string? Summary { get; set; }
        public string? Remarks { get; set; }
        public string? Returns { get; set; }
        public Dictionary<string, string>? Params { get; set; }
        public Dictionary<string, string>? TypeParams { get; set; }
        public Dictionary<string, string>? Exceptions { get; set; }
    }
}
