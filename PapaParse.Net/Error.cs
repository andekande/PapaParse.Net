using System;

namespace PapaParse.Net
{
    public class Error
    {
        public string type;
        public string code;
        public string message;
        public int? row;
        public int? index;

        public override string ToString()
        {
            return type + "|" + code + "|" + message + "|" + row + "|" + index;
        }
        public override bool Equals(object obj)
        {
            return this.ToString() == obj.ToString();
        }
    }
}
