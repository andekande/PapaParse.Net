using System;
using System.IO;

namespace PapaParse.Net
{
    public class FileStreamer : ChunkStreamer
    {
        BinaryReader reader;

        public FileStreamer(Config config)
            : base(setupConfig(config))
        {

        }

        private static Config setupConfig(Config config)
        {
            config = config ?? new Config();
            if (config.chunkSize == 0)
                config.chunkSize = Papa.LocalChunkSize;

            return config;
        }

        public void stream(Stream file)
		{
			base._inputFile = file;

            file.Seek(0, SeekOrigin.Begin);
            reader = new BinaryReader(file, base._config.encoding);

			this._nextChunk();	// Starts streaming

            reader.Dispose();
		}

        protected override Result _nextChunk()
        {
            if (!base._finished && (base._config.preview == 0 || base._rowCount < base._config.preview))
                this._readChunk();
            
            return null;
        }

        private void _readChunk()
		{
            byte[] input = null;

            try
            {
                if (base._config.chunkSize > 0)
                {
                    input = reader.ReadBytes(base._config.chunkSize);
                }
                else
                {
                    input = reader.ReadBytes((int)reader.BaseStream.Length);
                }               
            }
            catch (Exception ex)
            {
                _chunkError(new Error() { message = ex.Message });
            }

            this._chunkLoaded(input);
		}

		private void _chunkLoaded(byte[] input)
		{
			// Very important to increment start each time before handling results
			base._start += base._config.chunkSize;
			base._finished = base._config.chunkSize == 0 || base._start >= base._inputFile.Length;

            string txt = base._config.encoding.GetString(input, 0, input.Length);
            base.parseChunk(txt);
		}

		private void _chunkError(Error error)
		{
            reader.Dispose();
			base._sendError(error);
		}
    }
}
