using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PapaParse.Net
{
    // ChunkStreamer is the base prototype for various streamer implementations.
    public class ChunkStreamer
    {
        ParserHandle _handle = null;
        bool _paused = false;
        protected bool _finished = false;
        protected Stream _inputFile = null;
        protected Uri _inputUrl = null;
        int _baseIndex = 0;
        string _partialLine = "";
        protected int _rowCount = 0;
        protected long _start = 0;
        bool isFirstChunk = true;
        Result _completeResults = new Result() {
            data = new List<List<string>>(),
            dataWithHeader = new List<Dictionary<string,string>>(),
            errors = new List<Error>(),
            meta = new Meta()
        };

        protected Config _config;


        public ChunkStreamer(Config config)
        {
            replaceConfig(config);
        }

        public Result parseChunk(string chunk)
        {
            // First chunk pre-processing
            if (this.isFirstChunk && Papa.isFunction(this._config.beforeFirstChunk))
            {
                string modifiedChunk = this._config.beforeFirstChunk(chunk);
                if (modifiedChunk != null)
                    chunk = modifiedChunk;
            }
            this.isFirstChunk = false;

            // Rejoin the line we likely just split in two by chunking the file
            string aggregate = this._partialLine + chunk;
            this._partialLine = "";

            Result results = this._handle.parse(aggregate, this._baseIndex, !this._finished);

            if (this._handle.paused() || this._handle.aborted())
                return null;

            int lastIndex = results.meta.cursor;

            if (!this._finished)
            {
                this._partialLine = Papa.Substring(aggregate, lastIndex - this._baseIndex);
                this._baseIndex = lastIndex;
            }

            if (results != null && results.data != null)
                this._rowCount += results.data.Count;

            bool finishedIncludingPreview = this._finished || (this._config.preview > 0 && this._rowCount >= this._config.preview);

            if (Papa.IS_WORKER)
            {
                //global.postMessage({
                //    results: results,
                //    workerId: Papa.WORKER_ID,
                //    finished: finishedIncludingPreview
                //});
            }
            else if (Papa.isFunction(this._config.chunk))
            {
                this._config.chunk(results, this._handle);
                if (this._paused)
                    return null;
                results = null;
                this._completeResults = null;
            }

            if (this._config.step == null && this._config.chunk == null) {
                this._completeResults.data = this._completeResults.data.Concat(results.data).ToList();
                this._completeResults.dataWithHeader = this._completeResults.dataWithHeader.Concat(results.dataWithHeader).ToList();
                this._completeResults.errors = this._completeResults.errors.Concat(results.errors).ToList();
                this._completeResults.meta = results.meta;
            }

            if (finishedIncludingPreview && Papa.isFunction(this._config.complete) && (results == null || !results.meta.aborted))
                this._config.complete(this._completeResults);

            if (!finishedIncludingPreview && (results == null || !results.meta.paused))
                this._nextChunk();

            return results;
        }

        protected virtual Result _nextChunk()
        {
            return null;
        }

        protected void _sendError(Error error)
        {
            if (Papa.isFunction(this._config.error))
                this._config.error(error);
            else if (Papa.IS_WORKER && this._config.error != null)
            {
                //global.postMessage({
                //    workerId: Papa.WORKER_ID,
                //    error: error,
                //    finished: false
                //});
            }
        }

        private void replaceConfig(Config config)
        {
            // Deep-copy the config so we can edit it
            Config configCopy = Papa.copy(config);
            //configCopy.chunkSize = parseInt(configCopy.chunkSize);	// parseInt VERY important so we don't concatenate strings!
            if (config.step == null && config.chunk == null)
                configCopy.chunkSize = 0;  // disable Range header if not streaming; bad values break IIS - see issue #196
            this._handle = new ParserHandle(configCopy);
            this._handle.streamer = this;
            this._config = configCopy;	// persist the copy to the caller
        }
    }
}
