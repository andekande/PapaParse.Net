using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PapaParse.Net
{
    public class ParserHandle
    {
		// One goal is to minimize the use of regular expressions...
		Regex FLOAT = new Regex("^\\s*-?(\\d*\\.?\\d+|\\d+\\.?\\d*)(e[-+]?\\d+)?\\s*$", RegexOptions.IgnoreCase);

		int _stepCounter = 0;	// Number of times step was called (number of rows parsed)
		string _input;				// The input being parsed
		Parser _parser;			// The core parser being used
		bool _paused = false;	// Whether we are paused or not
		bool _aborted = false;   // Whether the parser has aborted or not
		bool _delimiterError;	// Temporary state between delimiter detection and processing results
		List<string> _fields = new List<string>();		// Fields are from the header row of the input, if there is one
		Result _results = new Result() {		// The last results returned from the parser
            data = new List<List<string>>(),
            dataWithHeader = new List<Dictionary<string,string>>(),
			errors = new List<Error>(),
			meta = new Meta()
		};

        Config _config;

        public ParserHandle(Config config)
        {
            _config = config;
            //[CR if(isFunction(_config.step))... moved inside of parse #L157]
        }

        public ChunkStreamer streamer = null;

        // Parses input. Most users won't need, and shouldn't mess with, the baseIndex
		// and ignoreLastRow parameters. They are used by streamers (wrapper functions)
		// when an input comes in multiple chunks, like from a file.
		public Result parse(string input, int baseIndex = 0, bool ignoreLastRow = false)
		{
            Func<bool> needsHeaderRow = () =>
            {
                return _config.header && _fields.Count == 0;
            };

            Action fillHeaderFields = () =>
            {
                if (_results == null || _results.data.Count == 0)
                    return;
                for (int i = 0; needsHeaderRow() && i < _results.data.Count; i++)
                    for (int j = 0; j < _results.data[i].Count; j++)
                        _fields.Add(_results.data[i][j]);
                _results.data.RemoveRange(0, 1);
            };

            Func<Result> applyHeaderAndDynamicTyping = () =>
            {
                if (_results == null || (!_config.header && !_config.dynamicTyping))
                    return _results;

                for (int i = 0; i < _results.data.Count; i++)
                {
                    Dictionary<string, string> rowWithHeader = new Dictionary<string, string>();

                    int j;
                    for (j = 0; j < _results.data[i].Count; j++)
                    {
                        //[TODO]
                        //if (_config.dynamicTyping)
                        //{
                        //    var value = _results.data[i][j];
                        //    if (value == "true" || value == "TRUE")
                        //        _results.data[i][j] = true;
                        //    else if (value == "false" || value == "FALSE")
                        //        _results.data[i][j] = false;
                        //    else
                        //        _results.data[i][j] = tryParseFloat(value);
                        //}

                        if (_config.header)
                        {
                            if (j >= _fields.Count)
                            {
                                if (!rowWithHeader.ContainsKey("__parsed_extra"))
                                    rowWithHeader.Add("__parsed_extra", "");
                                rowWithHeader["__parsed_extra"] += _results.data[i][j];
                                //[CR we can not simply put an Array into __parsed_extra, so juste pipe it]
                                if (j < _results.data[i].Count - 1)
                                    rowWithHeader["__parsed_extra"] += "|";
                            }
                            else
                                rowWithHeader[_fields[j]] = _results.data[i][j];
                        }
                    }

                    if (_config.header)
                    {
                        _results.dataWithHeader.Add(rowWithHeader); //[CR we are not overwriting _results.data here but instead fill another List]
                        if (j > _fields.Count)
                            addError("FieldMismatch", "TooManyFields", "Too many fields: expected " + _fields.Count + " fields but parsed " + j, i);
                        else if (j < _fields.Count)
                            addError("FieldMismatch", "TooFewFields", "Too few fields: expected " + _fields.Count + " fields but parsed " + j, i);
                    }
                }

                if (_config.header && _results.meta != null)
                    _results.meta.fields = _fields;
                return _results;
            };

            Func<Result> processResults = () =>
            {
                if (_results != null && _delimiterError)
                {
                    addError("Delimiter", "UndetectableDelimiter", "Unable to auto-detect delimiting character; defaulted to '" + Papa.DefaultDelimiter + "'");
                    _delimiterError = false;
                }

                if (_config.skipEmptyLines)
                {
                    for (int i = 0; i < _results.data.Count; i++)
                        if (_results.data[i].Count == 1 && _results.data[i][0] == "")
                            _results.data.RemoveRange(i--, 1);
                }

                if (needsHeaderRow())
                    fillHeaderFields();

                return applyHeaderAndDynamicTyping();
            };
            //------------------------------------------------------------------------------------------------------------------------------------------------------

			if (String.IsNullOrEmpty(_config.newline))
				_config.newline = guessLineEndings(input);

			_delimiterError = false;
			if (String.IsNullOrEmpty(_config.delimiter))
			{
				DelimiterResult delimGuess = guessDelimiter(input);
				if (delimGuess.successful)
					_config.delimiter = delimGuess.bestDelimiter;
				else
				{
					_delimiterError = true;	// add error after parsing (otherwise it would be overwritten)
					_config.delimiter = Papa.DefaultDelimiter;
				}
				_results.meta.delimiter = _config.delimiter;
			}

            if (_config.quoteChar == Char.MinValue)
		    {
                if (Papa.Substr(input, 0, 1) == "'" && Papa.Substr(input, input.IndexOf(_config.delimiter, 0) - 1, 1) == "'")
                    _config.quoteChar = '\'';
			    else
                    _config.quoteChar = '"';
            }

            Config parserConfig = Papa.copy(_config);
			if (_config.preview > 0 && _config.header)
				parserConfig.preview++;	// to compensate for header row

            if (Papa.isFunction(_config.step))
            {
                Action<Result, ParserHandle> userStep = _config.step;
                parserConfig.step = (results, parser) =>
                {
                    _results = results;

                    if (needsHeaderRow())
                        processResults();
                    else	// only call user's step function after header row
                    {
                        processResults();

                        // It's possbile that this line was empty and there's no row here after all
                        if (_results.data.Count == 0)
                            return;

                        _stepCounter += results.data.Count;
                        if (parserConfig.preview > 0 && _stepCounter > parserConfig.preview)
                            _parser.abort();
                        else
                            userStep(_results, this);
                    }
                };
            }
            //----------------------------------------------------------------------

			_input = input;
			_parser = new Parser(parserConfig);
			_results = _parser.parse(_input, baseIndex, ignoreLastRow);
			processResults();

            if(_paused)
                return new Result() { meta = new Meta() { paused = true} };
            else if (_results != null)
                return _results;
            else
                return new Result() { meta = new Meta() { paused = false} };
		}



		public bool paused()
		{
			return _paused;
		}

		public void pause()
		{
			_paused = true;
			_parser.abort();
			_input = Papa.Substr(_input, _parser.getCharIndex());
		}

        public void resume()
        {
            _paused = false;
            this.streamer.parseChunk(_input);
        }

		public bool aborted() {
			return _aborted;
		}

		public void abort()
		{
			_aborted = true;
			_parser.abort();
			_results.meta.aborted = true;
			if (Papa.isFunction(_config.complete))
				_config.complete(_results);
			_input = "";
		}









        private class DelimiterResult
        {
            public bool successful;
            public string bestDelimiter;
        }

        private DelimiterResult guessDelimiter(string input)
		{
			string[] delimChoices = {",", "\t", "|", ";", Papa.RECORD_SEP, Papa.UNIT_SEP};
			string bestDelim = "";
            int? bestDelta = null, fieldCountPrevRow = null;

			for (int i = 0; i < delimChoices.Length; i++)
			{
				string delim = delimChoices[i];
				int delta = 0, avgFieldCount = 0;
				fieldCountPrevRow = null;

				Result preview = new Parser(new Config() {
					delimiter = delim,
					preview = 10
				}).parse(input);

				for (int j = 0; j < preview.data.Count; j++)
				{
					int fieldCount = preview.data[j].Count;
					avgFieldCount += fieldCount;

					if (fieldCountPrevRow == null)
					{
						fieldCountPrevRow = fieldCount;
						continue;
					}
					else if (fieldCount > 1)
					{
						delta += Math.Abs(fieldCount - fieldCountPrevRow.Value);
						fieldCountPrevRow = fieldCount;
					}
				}

                if (preview.data.Count > 0)
				    avgFieldCount /= preview.data.Count;

				if ((bestDelta == null || delta < bestDelta) && avgFieldCount > 1.99)
				{
					bestDelta = delta;
					bestDelim = delim;
				}
			}

			_config.delimiter = bestDelim;

            return new DelimiterResult()
            {
                successful = !String.IsNullOrEmpty(bestDelim),
                bestDelimiter = bestDelim
            };
		}
    
    	private string guessLineEndings(string input)
		{
            input = Papa.Substr(input, 0, 1024 * 1024);	// max length 1 MB

			string[] r = input.Split('\r');

			if (r.Length == 1)
				return "\n";

			int numWithN = 0;
			for (int i = 0; i < r.Length; i++)
			{
				if (r[i][0] == '\n')
					numWithN++;
			}

			return numWithN >= r.Length / 2 ? "\r\n" : "\r";
		}
    
        //[TODO]
        //private float tryParseFloat(string val)
        //{
        //    bool isNumber = FLOAT.IsMatch(val);
        //    return isNumber ? float.Parse(val) : val;
        //}
    
        private void addError(string type, string code, string msg, int? row = null)
		{
			_results.errors.Add(new Error() {
				type = type,
				code = code,
				message = msg,
                row = row
			});
		}
    }
}
