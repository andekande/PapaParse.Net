using System;

namespace PapaParse.Net
{
    public class StringStreamer : ChunkStreamer
    {
        public StringStreamer(Config config)
            : base(setupConfig(config))
        {

        }

        private static Config setupConfig(Config config)
        {
            config = config ?? new Config();
            return config;
        }

        string input;
        string remaining;

        public Result stream(string s)
        {
            input = s;
            remaining = s;
            return this._nextChunk();
        }

        protected override Result _nextChunk() 
        {
            if (base._finished) return null;
            int size = base._config.chunkSize;
            string chunk = size > 0 ? Papa.Substr(remaining, 0, size) : remaining;
            remaining = size > 0 ? Papa.Substr(remaining, size) : "";
            base._finished = String.IsNullOrEmpty(remaining);
            return base.parseChunk(chunk);
        }
    }
}
