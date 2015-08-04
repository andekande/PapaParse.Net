using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PapaParse.Net
{
    // The core parser implements speedy and correct CSV parsing
    public class Parser
    {
        string delim;
        string newline;
        char quoteChar;
        string comments;
        Action<Result, ParserHandle> step;
        int preview;
        bool? fastMode;

        int cursor;
        bool aborted;
        List<List<string>> data;
        List<Error> errors;
        List<string> row;
        
        int lastCursor;

        public Parser(Config config)
        {
            // Unpack the config object
            config = config ?? new Config();
            delim = config.delimiter;
            newline = config.newline;
            comments = config.comments;
            step = config.step;
            preview = config.preview;
            fastMode = config.fastMode;
            quoteChar = config.quoteChar;

            // Quote Character defaults to DoubleQuote
            if (quoteChar == Char.MinValue)
                quoteChar = '"';

            // Delimiter must be valid
            if (String.IsNullOrEmpty(delim) || Array.IndexOf(Papa.BAD_DELIMITERS, delim) > -1 )
                delim = ",";

		    // Comment character must be valid
		    if (comments == delim)
			    throw new Exception("Comment character same as delimiter");
		    else if (comments == "true")
			    comments = "#";
            else if (comments == "false") //[CR]
                comments = "";
            else if (Regex.IsMatch(comments, "^[0-9]+$")) //[CR]
                comments = "";
            else if (String.IsNullOrEmpty(comments) || Array.IndexOf(Papa.BAD_DELIMITERS, comments) > -1)
			    comments = "";

		    // Newline must be valid: \r, \n, or \r\n
		    if (newline != "\n" && newline != "\r" && newline != "\r\n")
			    newline = "\n";

		    // We're gonna need these at the Parser scope
		    cursor = 0;
		    aborted = false;
        }

        public Result parse(string input, int baseIndex = 0, bool ignoreLastRow = false)
        {
            // We don't need to compute some of these every time parse() is called,
            // but having them in a more local scope seems to perform better
            int inputLen = input.Length,
                delimLen = delim.Length,
                newlineLen = newline.Length,
                commentsLen = comments.Length;
            bool stepIsFunction = step != null;
            string[] delimSplit = new string[] { delim },
                    newlineSplit = new string[] { newline };

            
            // Returns an object with the results, errors, and meta.
            Func<bool, Result> returnable = (stopped) =>
		    {
                return new Result()
                {
                    data = data,
                    dataWithHeader = new List<Dictionary<string,string>>(),
                    errors = errors,
                    meta = new Meta()
                    {
                        delimiter = delim,
                        linebreak = newline,
                        aborted = aborted,
                        truncated = stopped,
                        cursor = lastCursor + baseIndex
                    }
                };
		    };

            // Executes the user's step function and resets data & errors.
            Action doStep = () =>
		    {
			    step(returnable(false), null);
			    data = new List<List<string>>();
                errors = new List<Error>();
		    };

            Action<List<string>> pushRow = (newRow) =>
		    {
			    data.Add(newRow);
			    lastCursor = cursor;
		    };

            // Appends the remaining input from cursor to the end into
		    // row, saves the row, calls step, and returns the results.
            Func<string, Result> finish = (newValue) =>
		    {
			    if (ignoreLastRow)
				    return returnable(false);
			    if (newValue == null)
				    newValue = Papa.Substr(input, cursor);
			    row.Add(newValue);
			    cursor = inputLen;	// important in case parsing is paused
			    pushRow(row);
			    if (stepIsFunction)
				    doStep();
			    return returnable(false);
		    };
            //------------------------------------------------------------------------------------------------------------------------------------------------------


            // Establish starting state
            cursor = 0;
            data = new List<List<string>>();
            errors = new List<Error>();
            row = new List<string>();
            lastCursor = 0;

            if (String.IsNullOrEmpty(input))
                return returnable(false);

            if (fastMode == true || (fastMode != false && input.IndexOf(quoteChar) == -1))
			{
                string[] rows = input.Split(newlineSplit, StringSplitOptions.None);
				for (int i = 0; i < rows.Length; i++)
				{
					string rowFast = rows[i];
					cursor += rowFast.Length;
					if (i != rows.Length - 1)
						cursor += newline.Length;
					else if (ignoreLastRow)
						return returnable(false);
					if (!String.IsNullOrEmpty(comments) && Papa.Substr(rowFast, 0, commentsLen) == comments)
						continue;
					if (stepIsFunction)
					{
                        data = new List<List<string>>();
                        pushRow(new List<string>(rowFast.Split(delimSplit, StringSplitOptions.None)));
						doStep();
						if (aborted)
							return returnable(false);
					}
					else
                        pushRow(new List<string>(rowFast.Split(delimSplit, StringSplitOptions.None)));
					if (preview > 0 && i >= preview)
					{
                        data = data.GetRange(0, preview);
						return returnable(true);
					}
				}
				return returnable(false);
			}

            int nextDelim = input.IndexOf(delim, cursor);
            int nextNewline = input.IndexOf(newline, cursor);

		    // Appends the current row to the results. It sets the cursor
		    // to newCursor and finds the nextNewline. The caller should
		    // take care to execute user's step function and check for
		    // preview and end parsing if necessary.
		    Action<int> saveRow = (newCursor) =>
		    {
			    cursor = newCursor;
			    pushRow(row);
			    row = new List<string>();
			    nextNewline = input.IndexOf(newline, cursor);
		    };

            // Parser loop
            for(;;)
            {
                //[CR added so we never look behind the string]
                if (input.Length <= cursor)
                {
                    //System.Diagnostics.Debugger.Break();
                    break;
                }

                // Field has opening quote
                if (input[cursor] == quoteChar)
				{
					// Start our search for the closing quote where the cursor is
					int quoteSearch = cursor;

					// Skip the opening quote
					cursor++;

					for (;;)
					{
						// Find closing quote
						quoteSearch = input.IndexOf(quoteChar, quoteSearch+1);

						if (quoteSearch == -1)
						{
							if (!ignoreLastRow) {
								// No closing quote... what a pity
								errors.Add(new Error(){
									type = "Quotes",
									code = "MissingQuotes",
									message = "Quoted field unterminated",
									row = data.Count,	// row has yet to be inserted
									index = cursor
								});
							}
							return finish(null);
						}

						if (quoteSearch == inputLen-1)
						{
							// Closing quote at EOF
                            string value = Regex.Replace(Papa.Substring(input, cursor, quoteSearch), quoteChar.ToString() + quoteChar.ToString(), quoteChar.ToString(), RegexOptions.Multiline);
							return finish(value);
						}

						// If this quote is escaped, it's part of the data; skip it
						if (input[quoteSearch+1] == quoteChar)
						{
							quoteSearch++;
							continue;
						}

						if (input[quoteSearch+1].ToString() == delim)
						{
							// Closing quote followed by delimiter
                            row.Add(Regex.Replace(Papa.Substring(input, cursor, quoteSearch), quoteChar.ToString() + quoteChar.ToString(), quoteChar.ToString(), RegexOptions.Multiline));
							cursor = quoteSearch + 1 + delimLen;
							nextDelim = input.IndexOf(delim, cursor);
							nextNewline = input.IndexOf(newline, cursor);
							break;
						}

						if (Papa.Substr(input, quoteSearch+1, newlineLen) == newline)
						{
							// Closing quote followed by newline
                            row.Add(Regex.Replace(Papa.Substring(input, cursor, quoteSearch), quoteChar.ToString() + quoteChar.ToString(), quoteChar.ToString(), RegexOptions.Multiline));
							saveRow(quoteSearch + 1 + newlineLen);
							nextDelim = input.IndexOf(delim, cursor);	// because we may have skipped the nextDelim in the quoted field

							if (stepIsFunction)
							{
								doStep();
								if (aborted)
									return returnable(false);
							}
							
							if (preview > 0 && data.Count >= preview)
								return returnable(true);

							break;
						}
					}

					continue;
				}

				// Comment found at start of new line
				if (!String.IsNullOrEmpty(comments) && row.Count == 0 && Papa.Substr(input, cursor, commentsLen) == comments)
				{
					if (nextNewline == -1)	// Comment ends at EOF
						return returnable(false);
					cursor = nextNewline + newlineLen;
					nextNewline = input.IndexOf(newline, cursor);
					nextDelim = input.IndexOf(delim, cursor);
					continue;
				}
                
				// Next delimiter comes before next newline, so we've reached end of field
				if (nextDelim != -1 && (nextDelim < nextNewline || nextNewline == -1))
				{
                    row.Add(Papa.Substring(input, cursor, nextDelim));
					cursor = nextDelim + delimLen;
					nextDelim = input.IndexOf(delim, cursor);
					continue;
				}

                // End of row
				if (nextNewline != -1)
				{
                    row.Add(Papa.Substring(input, cursor, nextNewline));
					saveRow(nextNewline + newlineLen);

					if (stepIsFunction)
					{
						doStep();
						if (aborted)
							return returnable(false);
					}

					if (preview > 0 && data.Count >= preview)
						return returnable(true);

					continue;
				}

				break;
            }

            return finish(null);
        }


        // Sets the abort flag
		public void abort()
		{
			aborted = true;
		}

		// Gets the cursor position
		public int getCharIndex()
		{
			return cursor;
		}
    }
}
