using System;
using System.IO;

namespace PapaParse.Net
{
    public static class Papa
    {
        //[TODO]
        public const bool IS_WORKER = false;

        public static readonly string RECORD_SEP = ((char)30).ToString(); //Char.ConvertFromUtf32(30)
        public static readonly string UNIT_SEP = ((char)31).ToString(); //Char.ConvertFromUtf32(31)
        public static readonly string BYTE_ORDER_MARK = ((char)65279).ToString(); //Char.ConvertFromUtf32(65279) //"\ufeff";
        public static readonly string[] BAD_DELIMITERS = { "\r", "\n", "\"", Papa.BYTE_ORDER_MARK };

        // Configurable chunk sizes for local and remote files, respectively
	    public const int LocalChunkSize = 1024 * 1024 * 10;	// 10 MB
        public const int RemoteChunkSize = 1024 * 1024 * 5;	// 5 MB
        public const string DefaultDelimiter = ",";

        public static Result parse(string input, Config config = null)
        {
            return CsvToJson(input, config);
        }
        public static void parse(Stream file, Config config = null)
        {
            CsvToJson(file, config);
        }
        public static void parse(Uri url, Config config = null)
        {
            CsvToJson(url, config);
        }

	    private static Result CsvToJson(string _input, Config _config)
	    {
            StringStreamer streamer = new StringStreamer(_config);

		    return streamer.stream(_input);
	    }

        private static void CsvToJson(Uri _url, Config _config)
        {
            NetworkStreamer streamer = new NetworkStreamer(_config);

            streamer.stream(_url);
        }

        private static void CsvToJson(Stream _file, Config _config)
        {
            FileStreamer streamer = new FileStreamer(_config);

            streamer.stream(_file);
        }



        public static Config copy(Config obj)
        {
            Config _conf = new Config();
            _conf.beforeFirstChunk = obj.beforeFirstChunk;
            _conf.chunk = obj.chunk;
            _conf.chunkSize = obj.chunkSize;
            _conf.comments = obj.comments;
            _conf.complete = obj.complete;
            _conf.delimiter = obj.delimiter;
            _conf.dynamicTyping = obj.dynamicTyping;
            _conf.encoding = obj.encoding;
            _conf.error = obj.error;
            _conf.fastMode = obj.fastMode;
            _conf.header = obj.header;
            _conf.newline = obj.newline;
            _conf.preview = obj.preview;
            _conf.skipEmptyLines = obj.skipEmptyLines;
            _conf.step = obj.step;
            _conf.worker = obj.worker;
            _conf.quoteChar = obj.quoteChar;

            return _conf;
        }

        public static bool isFunction(Delegate func)
        {
            return func != null;
        }

        public static string Substring(this string input, int startIndex, int endIndex)
        {
            if (startIndex >= input.Length)
                return "";
            if (endIndex <= input.Length)
                return input.Substring(startIndex, endIndex - startIndex);
            else
                return input.Substring(startIndex);
        }

        public static string Substring(this string input, int startIndex)
        {
            return startIndex < input.Length ? input.Substring(startIndex) : "";
        }

        public static string Substr(this string input, int startIndex, int length)
        {
            if (startIndex >= input.Length)
                return "";
            if (startIndex + length <= input.Length)
                return input.Substring(startIndex, length);
            else
                return input.Substring(startIndex);
        }

        public static string Substr(this string input, int startIndex)
        {
            return Papa.Substring(input, startIndex);
        }
    }
}
