using System;
using System.Collections.Generic;

namespace PapaParse.Net
{
    public class Meta
    {
        public string delimiter;
        public string linebreak;
        public bool aborted;
        public List<string> fields;
        public bool truncated;
        public int cursor;
        public bool paused;

        public override string ToString()
        {
            return delimiter + "|" + linebreak + "|" + aborted + "|" + truncated + "|" + cursor + "|" + paused;
        }

        public override bool Equals(object obj)
        {
            return this.ToString() == obj.ToString();
        }
    }
}
