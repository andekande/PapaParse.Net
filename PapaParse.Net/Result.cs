using System;
using System.Collections.Generic;

namespace PapaParse.Net
{
    public class Result
    {
        public List<List<string>> data;
        public List<Dictionary<string, string>> dataWithHeader;
        public List<Error> errors;
        public Meta meta;
    }
}
