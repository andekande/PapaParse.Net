using System;
using System.Text;

namespace PapaParse.Net
{
    public class Config
    {
        public string delimiter = "";   // auto-detect;
        public string newline = "";     // auto-detect;
        public char quoteChar = Char.MinValue;
        public bool header = false;
        public bool dynamicTyping = false;
        public int preview = 0;
        public Encoding encoding = Encoding.UTF8;
        public bool worker = false;
        public string comments = "false";
        public Action<Result, ParserHandle> step = null;
        public Action<Result> complete = null;
        public Action<Error> error = null;
        public bool skipEmptyLines = false;
        public Action<Result, ParserHandle> chunk = null;
        public bool? fastMode = null;
        public Func<string, string> beforeFirstChunk = null;
        public int chunkSize = 0;
    }
}
