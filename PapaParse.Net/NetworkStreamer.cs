using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace PapaParse.Net
{
    public class NetworkStreamer : ChunkStreamer
    {
        public NetworkStreamer(Config config)
            : base(setupConfig(config))
        {

        }

        private static Config setupConfig(Config config)
        {
            config = config ?? new Config();
            if (config.chunkSize == 0)
                config.chunkSize = Papa.RemoteChunkSize;

            return config;
        }

        HttpWebRequest xhr;
        IAsyncResult result;

        public void stream(Uri url)
        {
            base._inputUrl = url;
            this._nextChunk();	// Starts streaming
        }

        protected override Result _nextChunk() 
        {
            if (Papa.IS_WORKER)
            {
                this._readChunk();
                this._chunkLoaded(result);
            }
            else
            {
			    this._readChunk();
            }

            return null;
        }

        private void _readChunk()
        {
            if (base._finished)
            {
                this._chunkLoaded(result);
                return;
            }

            xhr = HttpWebRequest.CreateHttp(base._inputUrl);
            xhr.Method = "GET";

            if (base._config.chunkSize > 0)
            {
                long end = base._start + base._config.chunkSize - 1;	// minus one because byte range is inclusive
                //xhr.Headers["Range"] = "bytes="+this._start+"-"+end;
                //xhr.AddRange(); //does not exist in PCL, done via Reflection
                xhr.Headers["If-None-Match"] = "webkit-no-cache"; // https://bugs.webkit.org/show_bug.cgi?id=82672

                try
                {
                    Type type = typeof(HttpWebRequest);
//#if NET_4_0 || SILVERLIGHT
//                    MethodInfo addrange = type.GetRuntimeMethod("AddRange", new Type[] { typeof(string), typeof(Int64), typeof(Int64) });
//                    addrange.Invoke(xhr, new object[] { "bytes", base._start, end });
//#else
                    MethodInfo addrange = type.GetMethod("AddRange", new Type[] { typeof(string), typeof(Int32), typeof(Int32) });
                    addrange.Invoke(xhr, new object[] { "bytes", (int)base._start, (int)end });       
//#endif
                }
                catch { }
            }

            result = xhr.BeginGetResponse(this._chunkLoaded, null);

            if (Papa.IS_WORKER && xhr.HaveResponse == false)
                this._chunkError("");
            else
                base._start += base._config.chunkSize;
        }

        private void _chunkLoaded(IAsyncResult ar)
        {
            try
            {
                HttpWebResponse response = (HttpWebResponse)xhr.EndGetResponse(ar);

                if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 400)
			    {
                    this._chunkError(response.StatusDescription);
				    return;
			    }

                using (var stream = response.GetResponseStream())
                {
                    base._finished = base._config.chunkSize == 0 || base._start > getFileSize(response);
                    var reader = new StreamReader(stream);
                    base.parseChunk(reader.ReadToEnd());
                }
            }
            catch (WebException ex)
            {
                _chunkError(ex.Message);
            }
        }

        private void _chunkError(string errorMessage)
		{
            base._sendError(new Error() { message = errorMessage });
		}

        private long getFileSize(HttpWebResponse xhr)
		{
            string contentRange = xhr.Headers["Content-Range"];
			return Int64.Parse(contentRange.Substr(contentRange.LastIndexOf("/") + 1));
		}
    }
}
