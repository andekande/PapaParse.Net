using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace PapaParse.Net.Tests
{
    public class Resultdatacomparer : IEqualityComparer<List<string>>
    {
        public bool Equals(List<string> x, List<string> y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(List<string> obj)
        {
            return obj.GetHashCode();
        }
    }

    public class ResultdataWithHeadercomparer : IEqualityComparer<Dictionary<string, string>>
    {

        public bool Equals(Dictionary<string, string> x, Dictionary<string, string> y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(Dictionary<string, string> obj)
        {
            return obj.GetHashCode();
        }
    }

    public class TestFileHandler : HttpMessageHandler
    {
        protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            HttpResponseMessage _response = new HttpResponseMessage();
            _response.Headers.AcceptRanges.Add("bytes");

            if (request.Method == HttpMethod.Get)
            {
                string filename = request.RequestUri.AbsolutePath.TrimStart('/');
                long from = request.Headers.Range.Ranges.First().From.Value;
                long to = request.Headers.Range.Ranges.First().To.Value + 1;

                byte[] filecontent;
                long filesize;
                using (FileStream stream = File.OpenRead(filename))
                {
                    filesize = stream.Length;
                    filecontent = new byte[Math.Min(to - from, stream.Length - from)];
                    stream.Seek(from, SeekOrigin.Begin);
                    stream.Read(filecontent, 0, filecontent.Length);
                }
                _response.Content = new ByteArrayContent(filecontent);
                _response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(filesize);
                if (filesize != filecontent.Length)
                    _response.StatusCode = System.Net.HttpStatusCode.PartialContent;
            }

            var tcs = new System.Threading.Tasks.TaskCompletionSource<HttpResponseMessage>();
            _response.RequestMessage = request;
            tcs.SetResult(_response);
            return tcs.Task;
        }
    }

    [TestClass]
    public class TestCases
    {
        private static HttpSelfHostServer server;

        [AssemblyInitialize]
        public static void StartSelfHostedWebServer(TestContext context)
        {
            HttpSelfHostConfiguration config = new HttpSelfHostConfiguration("http://localhost:5588/");
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;
            try
            {
                server = new HttpSelfHostServer(config, new TestFileHandler());
                server.OpenAsync().Wait();
            }
            catch (Exception ex)
            {
                //Visual Studio needs to be elavated in order to grab a Port. So please run it with Administrator rights.
                //continuing execution, but all chunk related Tests will fail...
                Console.WriteLine("VisualStudio needs to be run as Administrator in order to pass all Tests.");
            }
        }

        [AssemblyCleanup]
        public static void TearDownSelfHostedWebServer()
        {
            if (server != null)
            {
                server.CloseAsync().Wait();
                server.Dispose();
            }
        }

        string RECORD_SEP = Char.ConvertFromUtf32(30);
        string UNIT_SEP = Char.ConvertFromUtf32(31);

        // Tests for the core parser using new Papa.Parser().parse() (CSV to JSON)
        #region CORE_PARSER_TESTS
        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void One_row()
        {
            Result actual = new Parser(null).parse("A,b,c");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Two_rows()
        {
            Result actual = new Parser(null).parse("A,b,c\nd,E,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "b", "c"},
                new List<string>() {"d", "E", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Three_rows()
        {
            Result actual = new Parser(null).parse("A,b,c\nd,E,f\nG,h,i");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "b", "c"},
                new List<string>() {"d", "E", "f"},
                new List<string>() {"G", "h", "i"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Whitespace_at_edges_of_unquoted_field()
        {
            Result actual = new Parser(null).parse("a,  b ,c");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "  b ", "c"}
            }, new Resultdatacomparer()), "Extra whitespace should graciously be preserved");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field()
        {
            Result actual = new Parser(null).parse("A,\"B\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_extra_whitespace_on_edges()
        {
            Result actual = new Parser(null).parse("A,\" B  \",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", " B  ", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_delimiter()
        {
            Result actual = new Parser(null).parse("A,\"B,B\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B,B", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_line_break()
        {
            Result actual = new Parser(null).parse("A,\"B\nB\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\nB", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_fields_with_line_breaks()
        {
            Result actual = new Parser(null).parse("A,\"B\nB\",\"C\nC\nC\"");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\nB", "C\nC\nC"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_fields_at_end_of_row_with_delimiter_and_line_break()
        {
            Result actual = new Parser(null).parse("a,b,\"c,c\nc\"\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c,c\nc"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_escaped_quotes()
        {
            Result actual = new Parser(null).parse("A,\"B\"\"B\"\"B\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\"B\"B", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_escaped_quotes_at_boundaries()
        {
            Result actual = new Parser(null).parse("A,\"\"\"B\"\"\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "\"B\"", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Unquoted_field_with_quotes_at_end_of_field()
        {
            Result actual = new Parser(null).parse("A,B\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\"", "C"}
            }, new Resultdatacomparer()), "The quotes character is misplaced, but shouldn't generate an error or break the parser");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_quotes_around_delimiter()
        {
            Result actual = new Parser(null).parse("A,\"\"\",\"\"\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "\",\"", "C"}
            }, new Resultdatacomparer()), "For a boundary to exist immediately before the quotes, we must not already be in quotes");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_quotes_on_right_side_of_delimiter()
        {
            Result actual = new Parser(null).parse("A,\",\"\"\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", ",\"", "C"}
            }, new Resultdatacomparer()), "Similar to the test above but with quotes only after the comma");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_quotes_on_left_side_of_delimiter()
        {
            Result actual = new Parser(null).parse("A,\"\"\",\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "\",", "C"}
            }, new Resultdatacomparer()), "Similar to the test above but with quotes only before the comma");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_5_quotes_in_a_row_and_a_delimiter_in_there_too()
        {
            Result actual = new Parser(null).parse("\"1\",\"cnonce=\"\"\"\",nc=\"\"\"\"\",\"2\"");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"1", "cnonce=\"\",nc=\"\"", "2"}
            }, new Resultdatacomparer()), "Actual input reported in issue #121");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_with_whitespace_around_quotes()
        {
            Result actual = new Parser(null).parse("A, \"B\" ,C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", " \"B\" ", "C"}
            }, new Resultdatacomparer()), "The quotes must be immediately adjacent to the delimiter to indicate a quoted field");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Misplaced_quotes_in_data_not_as_opening_quotes()
        {
            Result actual = new Parser(null).parse("A,B \"B\",C");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B \"B\"", "C"}
            }, new Resultdatacomparer()), "The input is technically malformed, but this syntax should not cause an error");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_has_no_closing_quote()
        {
            Result actual = new Parser(null).parse("a,\"b,c\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b,c\nd,e,f"}
            }, new Resultdatacomparer()));

            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "Quotes",
                    code = "MissingQuotes",
                    message = "Quoted field unterminated",
                    row = 0,
                    index = 3
                }
            }, actual.errors);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Line_starts_with_quoted_field()
        {
            Result actual = new Parser(null).parse("a,b,c\n\"d\",e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Line_ends_with_quoted_field()
        {
            Result actual = new Parser(null).parse("a,b,c\nd,e,f\n\"g\",\"h\",\"i\"\n\"j\",\"k\",\"l\"");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
                new List<string>() {"g", "h", "i"},
                new List<string>() {"j", "k", "l"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Quoted_field_at_end_of_row_but_not_at_EOF_has_quotes()
        {
            Result actual = new Parser(null).parse("a,b,\"c\"\"c\"\"\"\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c\"c\""},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Empty_quoted_field_at_EOF_is_empty()
        {
            Result actual = new Parser(null).parse("a,b,\"\"\na,b,\"\"");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", ""},
                new List<string>() {"a", "b", ""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Multiple_consecutive_empty_fields()
        {
            Result actual = new Parser(null).parse("a,b,,,c,d\n,,e,,,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "", "", "c", "d"},
                new List<string>() {"", "", "e", "", "", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Empty_input_string()
        {
            Result actual = new Parser(null).parse("");

            Assert.AreEqual(0, actual.data.Count);
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_is_just_the_delimiter_2_empty_fields()
        {
            Result actual = new Parser(null).parse(",");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"", ""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_is_just_empty_fields()
        {
            Result actual = new Parser(null).parse(",,\n,,,");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"", "", ""},
                new List<string>() {"", "", "", ""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_is_just_a_string_a_single_field()
        {
            Result actual = new Parser(null).parse("Abc def");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"Abc def"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Commented_line_at_beginning()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("# Comment!\na,b,c");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Commented_line_in_middle()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("a,b,c\n# Comment\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Commented_line_at_end()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("a,true,false\n# Comment");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "true", "false"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Two_comment_lines_consecutively()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("a,b,c\n#comment1\n#comment2\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Two_comment_lines_consecutively_at_end_of_file()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("a,b,c\n#comment1\n#comment2");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Three_comment_lines_consecutively_at_beginning_of_file()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("#comment1\n#comment2\n#comment3\na,b,c");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Entire_file_is_comment_lines()
        {
            Result actual = new Parser(new Config() { comments = "true" }).parse("#comment1\n#comment2\n#comment3");

            Assert.AreEqual(0, actual.data.Count);
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Comment_with_non_default_character()
        {
            Result actual = new Parser(new Config() { comments = "!" }).parse("a,b,c\n!Comment goes here\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Bad_comments_value_specified()
        {
            Result actual = new Parser(new Config() { comments = "5" }).parse("a,b,c\n5comment\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"5comment"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()), "Should silently disable comment parsing");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Multi_character_comment_string()
        {
            Result actual = new Parser(new Config() { comments = "=N(" }).parse("a,b,c\n=N(Comment)\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_with_only_a_commented_line()
        {
            Result actual = new Parser(new Config() { comments = "true", delimiter = "," }).parse("#commented line");

            Assert.AreEqual(0, actual.data.Count);
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_with_only_a_commented_line_and_blank_line_after()
        {
            Result actual = new Parser(new Config() { comments = "true", delimiter = "," }).parse("#commented line\n");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_with_only_a_commented_without_comments_enabled()
        {
            Result actual = new Parser(new Config() { delimiter = "," }).parse("#commented line");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"#commented line"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Input_without_comments_with_line_starting_with_whitespace()
        {
            Result actual = new Parser(new Config() { delimiter = "," }).parse("a\n b\nc");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a"},
                new List<string>() {" b"},
                new List<string>() {"c"}
            }, new Resultdatacomparer()), "\" \" == false, but \" \" !== false, so === comparison is required");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Multiple_rows_one_column_no_delimiter_found()
        {
            Result actual = new Parser(null).parse("a\nb\nc\nd\ne");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a"},
                new List<string>() {"b"},
                new List<string>() {"c"},
                new List<string>() {"d"},
                new List<string>() {"e"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void One_column_input_with_empty_fields()
        {
            Result actual = new Parser(null).parse("a\nb\n\n\nc\nd\ne\n");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a"},
                new List<string>() {"b"},
                new List<string>() {""},
                new List<string>() {""},
                new List<string>() {"c"},
                new List<string>() {"d"},
                new List<string>() {"e"},
                new List<string>() {""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Fast_mode_basic()
        {
            Result actual = new Parser(new Config() { fastMode = true }).parse("a,b,c\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Fast_mode_with_comments()
        {
            Result actual = new Parser(new Config() { fastMode = true, comments = "//" }).parse("// Commented line\na,b,c");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Fast_mode_with_preview()
        {
            Result actual = new Parser(new Config() { fastMode = true, preview = 2 }).parse("a,b,c\nd,e,f\nh,j,i\n");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("CORE_PARSER_TESTS")]
        [TestMethod]
        public void Fast_mode_with_blank_line_at_end()
        {
            Result actual = new Parser(new Config() { fastMode = true }).parse("a,b,c\n");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {""},
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }
        #endregion

        // Tests for Papa.parse() function -- high-level wrapped parser (CSV to JSON)
        #region PARSE_TESTS
        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Two_rows_just_r()
        {
            Result actual = Papa.parse("A,b,c\rd,E,f", null);

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "b", "c"},
                new List<string>() {"d", "E", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Two_rows_r_n()
        {
            Result actual = Papa.parse("A,b,c\r\nd,E,f", null);

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "b", "c"},
                new List<string>() {"d", "E", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Quoted_field_with_r_n()
        {
            Result actual = Papa.parse("A,\"B\r\nB\",C", null);

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\r\nB", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Quoted_field_with_r()
        {
            Result actual = Papa.parse("A,\"B\rB\",C", null);

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\rB", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Quoted_field_with_n()
        {
            Result actual = Papa.parse("A,\"B\nB\",C", null);

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"A", "B\nB", "C"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Header_row_with_one_row_of_data()
        {
            Result actual = Papa.parse("A,B,C\r\na,b,c", new Config() { header = true });

            Assert.IsTrue(actual.dataWithHeader.SequenceEqual(new List<Dictionary<string, string>>() { 
                new Dictionary<string, string>() { {"A", "a"}, { "B", "b" }, { "C", "c" } }
            }, new ResultdataWithHeadercomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Header_row_only()
        {
            Result actual = Papa.parse("A,B,C", new Config() { header = true });

            Assert.AreEqual(0, actual.dataWithHeader.Count);
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Row_with_too_few_fields()
        {
            Result actual = Papa.parse("A,B,C\r\na,b", new Config() { header = true });

            Assert.IsTrue(actual.dataWithHeader.SequenceEqual(new List<Dictionary<string, string>>() { 
                new Dictionary<string, string>() { {"A", "a"}, { "B", "b" } }
            }, new ResultdataWithHeadercomparer()));

            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "FieldMismatch",
                    code = "TooFewFields",
                    message = "Too few fields: expected 3 fields but parsed 2",
                    row = 0
                }
            }, actual.errors);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Row_with_too_many_fields()
        {
            Result actual = Papa.parse("A,B,C\r\na,b,c,d,e\r\nf,g,h", new Config() { header = true });

            Assert.IsTrue(actual.dataWithHeader.SequenceEqual(new List<Dictionary<string, string>>() { 
                new Dictionary<string, string>() { {"A", "a"}, { "B", "b" }, { "C", "c" }, { "__parsed_extra", "d|e" } },
                new Dictionary<string, string>() { {"A", "f"}, { "B", "g" }, { "C", "h" } }
            }, new ResultdataWithHeadercomparer()));

            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "FieldMismatch",
                    code = "TooManyFields",
                    message = "Too many fields: expected 3 fields but parsed 5",
                    row = 0
                }
            }, actual.errors);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Row_with_enough_fields_but_blank_field_at_end()
        {
            Result actual = Papa.parse("A,B,C\r\na,b,", new Config() { header = true });

            Assert.IsTrue(actual.dataWithHeader.SequenceEqual(new List<Dictionary<string, string>>() { 
                new Dictionary<string, string>() { {"A", "a"}, { "B", "b" }, { "C", "" } }
            }, new ResultdataWithHeadercomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Tab_delimiter()
        {
            Result actual = Papa.parse("a\tb\tc\r\nd\te\tf", new Config() { delimiter = "\t" });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Pipe_delimiter()
        {
            Result actual = Papa.parse("a|b|c\r\nd|e|f", new Config() { delimiter = "|" });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void ASCII_30_delimiter()
        {
            Result actual = Papa.parse("a"+RECORD_SEP+"b"+RECORD_SEP+"c\r\nd"+RECORD_SEP+"e"+RECORD_SEP+"f", new Config() { delimiter = RECORD_SEP });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void ASCII_31_delimiter()
        {
            Result actual = Papa.parse("a"+UNIT_SEP+"b"+UNIT_SEP+"c\r\nd"+UNIT_SEP+"e"+UNIT_SEP+"f", new Config() { delimiter = UNIT_SEP });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Bad_delimiter_n()
        {
            Result actual = Papa.parse("a,b,c", new Config() { delimiter = "\n" });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()), "Should silently default to comma");
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Multi_character_delimiter()
        {
            Result actual = Papa.parse("a, b, c", new Config() { delimiter = ", " });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        //[TODO]
        //[TestCategory("PARSE_TESTS")]
        //[TestMethod]
        //public void Dynamic_typing_converts_numeric_literals()
        //{
        //    Result actual = Papa.parse("1,2.2,1e3\r\n-4,-4.5,-4e-5\r\n-,5a,5-2", new Config() { dynamicTyping = true });

        //    Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
        //        new List<string>() {1, 2.2, 1000},
        //        new List<string>() {-4, -4.5, -0.00004},
        //        new List<string>() {"-", "5a", "5-2"}
        //    }, new Resultdatacomparer()));
        //    Assert.AreEqual(0, actual.errors.Count);
        //}

        //[TODO]
        //[TestCategory("PARSE_TESTS")]
        //[TestMethod]
        //public void Dynamic_typing_converts_boolean_literals()
        //{
        //    Result actual = Papa.parse("true,false,T,F,TRUE,FALSE,True,False", new Config() { dynamicTyping = true });

        //    Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() { 
        //        new List<string>() {true, false, "T", "F", true, false, "True", "False"}
        //    }, new Resultdatacomparer()));
        //    Assert.AreEqual(0, actual.errors.Count);
        //}

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Dynamic_typing_doesnt_convert_other_types()
        {
            Result actual = Papa.parse("A,B,C\r\nundefined,null,[\r\nvar,float,if", new Config() { dynamicTyping = true });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"A", "B", "C"},
                new List<string>() {"undefined", "null", "["},
                new List<string>() {"var", "float", "if"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Blank_line_at_beginning()
        {
            Result actual = Papa.parse("\r\na,b,c\r\nd,e,f", new Config() { newline = "\r\n" });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {""},
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Blank_line_in_middle()
        {
            Result actual = Papa.parse("a,b,c\r\n\r\nd,e,f", new Config() { newline = "\r\n" });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {""},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Blank_lines_at_end()
        {
            Result actual = Papa.parse("a,b,c\nd,e,f\n\n");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
                new List<string>() {""},
                new List<string>() {""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Blank_line_in_middle_with_whitespace()
        {
            Result actual = Papa.parse("a,b,c\r\n \r\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {" "},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void First_field_of_a_line_is_empty()
        {
            Result actual = Papa.parse("a,b,c\r\n,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Last_field_of_a_line_is_empty()
        {
            Result actual = Papa.parse("a,b,\r\nd,e,f");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", ""},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Other_fields_are_empty()
        {
            Result actual = Papa.parse("a,,c\r\n,,");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "", "c"},
                new List<string>() {"", "", ""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Empty_input_string2()
        {
            Result actual = Papa.parse("");

            Assert.AreEqual(0, actual.data.Count);
            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "Delimiter",
                    code = "UndetectableDelimiter",
                    message = "Unable to auto-detect delimiting character; defaulted to ','"
                }
            }, actual.errors);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Input_is_just_the_delimiter_2_empty_fields2()
        {
            Result actual = Papa.parse(",");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"", ""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Input_is_just_a_string_a_single_field2()
        {
            Result actual = Papa.parse("Abc def");

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"Abc def"}
            }, new Resultdatacomparer()));
            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "Delimiter",
                    code = "UndetectableDelimiter",
                    message = "Unable to auto-detect delimiting character; defaulted to ','"
                }
            }, actual.errors);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_0_rows_should_default_to_parsing_all()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i", new Config() { preview = 0 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
                new List<string>() {"g", "h", "i"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_1_row()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i", new Config() { preview = 1 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_2_rows()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i", new Config() { preview = 2 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_all_3_rows()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i", new Config() { preview = 3 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
                new List<string>() {"g", "h", "i"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_more_rows_than_input_has()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i", new Config() { preview = 4 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"},
                new List<string>() {"g", "h", "i"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_should_count_rows_not_lines()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,\"f\r\nf\",g,h,i", new Config() { preview = 2 });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f\r\nf", "g", "h", "i"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Preview_with_header_row()
        {
            Result actual = Papa.parse("a,b,c\r\nd,e,f\r\ng,h,i\r\nj,k,l", new Config() { header = true, preview = 2 });

            Assert.IsTrue(actual.dataWithHeader.SequenceEqual(new List<Dictionary<string, string>>() { 
                new Dictionary<string, string>() { {"a", "d"}, { "b", "e" }, { "c", "f" } },
                new Dictionary<string, string>() { {"a", "g"}, { "b", "h" }, { "c", "i" } }
            }, new ResultdataWithHeadercomparer()), "Preview is defined to be number of rows of input not including header row");

            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Empty_lines()
        {
            Result actual = Papa.parse("\na,b,c\n\nd,e,f\n\n", new Config() { delimiter = "," });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {""},
                new List<string>() {"a", "b", "c"},
                new List<string>() {""},
                new List<string>() {"d", "e", "f"},
                new List<string>() {""},
                new List<string>() {""}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Skip_empty_lines()
        {
            Result actual = Papa.parse("a,b,c\n\nd,e,f", new Config() { skipEmptyLines = true });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Skip_empty_lines_with_newline_at_end_of_input()
        {
            Result actual = Papa.parse("a,b,c\r\n\r\nd,e,f\r\n", new Config() { skipEmptyLines = true });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {"a", "b", "c"},
                new List<string>() {"d", "e", "f"}
            }, new Resultdatacomparer()));
            Assert.AreEqual(0, actual.errors.Count);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Skip_empty_lines_with_empty_input()
        {
            Result actual = Papa.parse("", new Config() { skipEmptyLines = true });

            Assert.AreEqual(0, actual.data.Count);
            CollectionAssert.AreEqual(new List<Error>() {
                new Error() {
                    type = "Delimiter",
                    code = "UndetectableDelimiter",
                    message = "Unable to auto-detect delimiting character; defaulted to ','"
                }
            }, actual.errors);
        }

        [TestCategory("PARSE_TESTS")]
        [TestMethod]
        public void Skip_empty_lines_with_first_line_only_whitespace()
        {
            Result actual = Papa.parse(" \na,b,c", new Config() { skipEmptyLines = true, delimiter = "," });

            Assert.IsTrue(actual.data.SequenceEqual(new List<List<string>>() {
                new List<string>() {" "},
                new List<string>() {"a", "b", "c"}
            }, new Resultdatacomparer()), "A line must be absolutely empty to be considered empty");
            Assert.AreEqual(0, actual.errors.Count);
        }
        #endregion

        #region CUSTOM_TESTS
        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Complete_is_called_with_all_results_if_neither_step_nor_chunk_is_defined()
        {
            byte[] sample = Encoding.UTF8.GetBytes("A,b,c\nd,E,f\nG,h,i");
            using (MemoryStream file = new MemoryStream(sample))
            {
                Papa.parse(file, new Config()
                {
                    chunkSize = 3,
                    complete = (response) =>
                    {
                        Assert.IsTrue(response.data.SequenceEqual(new List<List<string>>() {
                            new List<string>() {"A", "b", "c"},
                            new List<string>() {"d", "E", "f"},
                            new List<string>() {"G", "h", "i"}
                        }, new Resultdatacomparer()));
                        Assert.AreEqual(0, response.errors.Count);
                    }
                });
            }
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_is_called_for_each_row()
        {
            int callCount = 0;
            Papa.parse("A,b,c\rd,E,f", new Config() {
                step = (response, handle) => callCount++,
                complete = (response) =>
                {
                    Assert.AreEqual(2, callCount);
                    Assert.AreEqual(0, response.errors.Count);
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_is_called_with_the_contents_of_the_row()
        {
            Papa.parse("A,b,c", new Config()
            {
                step = (response, handle) =>
                {
                    CollectionAssert.AreEqual(new List<string>() { "A", "b", "c" }, response.data[0]);
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_is_called_with_the_last_cursor_position()
        {
            List<int> updates = new List<int>();
            Papa.parse("A,b,c\nd,E,f\nG,h,i", new Config()
            {
                step = (response, handle) =>
                {
                    updates.Add(response.meta.cursor);
                },
                complete = (response) =>
                {
                    CollectionAssert.AreEqual(new List<int> { 6, 12, 17 }, updates);
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_exposes_cursor_for_downloads()
        {
            List<int> updates = new List<int>();

            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                step = (response, handle) =>
                {
                    updates.Add(response.meta.cursor);
                },
                complete = (response) =>
                {
                    CollectionAssert.AreEqual(new List<int> { 129, 287, 452, 595, 727, 865, 1031, 1209 }, updates);
                }
            });

            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(updates.Count == 8);
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_exposes_cursor_for_chunked_downloads()
        {
            List<int> updates = new List<int>();
            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                chunkSize = 500,
                step = (response, handle) =>
                {
                    updates.Add(response.meta.cursor);
                },
                complete = (response) =>
                {
                    CollectionAssert.AreEqual(new List<int> { 129, 287, 452, 595, 727, 865, 1031, 1209 }, updates);
                }
            });

            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(updates.Count == 8);
        }

        //[TODO]
        //[TestCategory("CUSTOM_TESTS")]
        //[TestMethod]
        //public void Step_exposes_cursor_for_workers()
        //{
        //    List<int> updates = new List<int>();
        //    Papa.parse("/tests/long-sample.csv", new Config()
        //    {
        //        download = true,
        //        chunkSize = 500,
        //        worker = true,
        //        step = (response, handle) =>
        //        {
        //            updates.Add(response.meta.cursor);
        //        },
        //        complete = (response, fileInfo) =>
        //        {
        //            CollectionAssert.AreEqual(new List<int> { 452, 452, 452, 865, 865, 865, 1209, 1209 }, updates);
        //        }
        //    });
        //}

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Chunk_is_called_for_each_chunk()
        {
            List<int> updates = new List<int>();
            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                chunkSize = 500,
                chunk = (response, handle) =>
                {
                    updates.Add(response.data.Count);
                },
                complete = (response) =>
                {
                    CollectionAssert.AreEqual(new List<int> { 3, 3, 2 }, updates);
                }
            });

            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(updates.Count == 3);
        }


        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Chunk_is_called_with_cursor_position()
        {
            List<int> updates = new List<int>();
            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                chunkSize = 500,
                chunk = (response, handle) =>
                {
                    updates.Add(response.meta.cursor);
                },
                complete = (response) =>
                {
                    CollectionAssert.AreEqual(new List<int> { 452, 865, 1209 }, updates);
                }
            });

            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(updates.Count == 3);
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_exposes_indexes_for_files()
        {
            byte[] sample = Encoding.UTF8.GetBytes("A,b,c\nd,E,f\nG,h,i");
            using (MemoryStream file = new MemoryStream(sample))
            {
                List<int> updates = new List<int>();
                Papa.parse(file, new Config()
                {
                    step = (response, handle) =>
                    {
                        updates.Add(response.meta.cursor);
                    },
                    complete = (response) =>
                    {
                        CollectionAssert.AreEqual(new List<int> { 6, 12, 17 }, updates);
                    }
                });
            }
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_exposes_indexes_for_chunked_files()
        {
            byte[] sample = Encoding.UTF8.GetBytes("A,b,c\nd,E,f\nG,h,i");
            using (MemoryStream file = new MemoryStream(sample))
            {
                List<int> updates = new List<int>();
                Papa.parse(file, new Config()
                {
                    chunkSize = 3,
                    step = (response, handle) =>
                    {
                        updates.Add(response.meta.cursor);
                    },
                    complete = (response) =>
                    {
                        CollectionAssert.AreEqual(new List<int> { 6, 12, 17 }, updates);
                    }
                });
            }
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Quoted_line_breaks_near_chunk_boundaries_are_handled()
        {
            byte[] sample = Encoding.UTF8.GetBytes("A,B,C\nX,\"Y\n1\n2\n3\",Z");
            using (MemoryStream file = new MemoryStream(sample))
            {
                List<List<string>> updates = new List<List<string>>();
                Papa.parse(file, new Config()
                {
                    chunkSize = 3,
                    step = (response, handle) =>
                    {
                        updates.Add(response.data[0]);
                    },
                    complete = (response) =>
                    {
                        Assert.IsTrue(updates.SequenceEqual(new List<List<string>>() {
                            new List<string>() {"A", "B", "C"},
                            new List<string>() {"X", "Y\n1\n2\n3", "Z"}
                        }, new Resultdatacomparer()));
                    }
                });
            }
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_functions_can_abort_parsing()
        {
            List<List<string>> updates = new List<List<string>>();
            Papa.parse("A,b,c\nd,E,f\nG,h,i", new Config()
            {
                chunkSize = 6,
                step = (response, handle) =>
                {
                    updates.Add(response.data[0]);
                    handle.abort();
                    Assert.IsTrue(updates.SequenceEqual(new List<List<string>>() {
                            new List<string>() {"A", "b", "c"}
                        }, new Resultdatacomparer()));
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Complete_is_called_after_aborting()
        {
            List<List<string>> updates = new List<List<string>>();
            Papa.parse("A,b,c\nd,E,f\nG,h,i", new Config()
            {
                chunkSize = 6,
                step = (response, handle) =>
                {
                    handle.abort();
                },
                complete = (response) =>
                {
                    Assert.IsTrue(response.meta.aborted);
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_functions_can_pause_parsing()
        {
            List<List<string>> updates = new List<List<string>>();
            Papa.parse("A,b,c\nd,E,f\nG,h,i", new Config()
            {
                step = (response, handle) =>
                {
                    updates.Add(response.data[0]);
                    handle.pause();
                    Assert.IsTrue(updates.SequenceEqual(new List<List<string>>() {
                            new List<string>() {"A", "b", "c"}
                        }, new Resultdatacomparer()));
                },
                complete = (response) =>
                {
                    string callback = "incorrect complete callback";
                }
            });
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void Step_functions_can_resume_parsing()
        {
            List<List<string>> updates = new List<List<string>>();
            ParserHandle handle = null;
            bool first = true;
            Papa.parse("A,b,c\nd,E,f\nG,h,i", new Config()
            {
                step = (response, h) =>
                {
                    updates.Add(response.data[0]);
                    if (!first) return;
                    handle = h;
                    handle.pause();
                    first = false;
                },
                complete = (response) =>
                {
                    Assert.IsTrue(updates.SequenceEqual(new List<List<string>>() {
                            new List<string>() {"A", "b", "c"},
                            new List<string>() {"d", "E", "f"},
                            new List<string>() {"G", "h", "i"}
                        }, new Resultdatacomparer()));
                }
            });
            var t = new System.Threading.Timer((state) => {
                handle.resume();            
            }, null, 500, System.Threading.Timeout.Infinite);

            System.Threading.Thread.Sleep(1000);

            Assert.IsTrue(updates.Count == 3);
        }

        //[TODO]
        //[TestCategory("CUSTOM_TESTS")]
        //[TestMethod]
        //public void Step_functions_can_abort_workers()
        //{
        //    int updates = 0;
        //    Papa.parse("/tests/long-sample.csv", new Config()
        //    {
        //        worker = true,
        //        download = true,
        //        chunkSize = 500,
        //        step = (response, handle) =>
        //        {
        //            updates++;
        //            handle.abort();
        //        },
        //        complete = (response, fileInfo) =>
        //        {
        //            Assert.Equals(1, updates);
        //        }
        //    });
        //}

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void beforeFirstChunk_manipulates_only_first_chunk()
        {
            int updates = 0;
            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                chunkSize = 500,
                beforeFirstChunk = (chunk) =>
                {
                    return (new System.Text.RegularExpressions.Regex(".*?\n")).Replace(chunk, "", 1);
                },
                step = (response, handle) =>
                {
                    updates++;
                },
                complete = (response) =>
                {
                    Assert.Equals(7, updates);
                }
            });

            System.Threading.Thread.Sleep(500);

            Assert.IsTrue(updates == 7);
        }

        [TestCategory("CUSTOM_TESTS")]
        [TestMethod]
        public void First_chunk_not_modified_if_beforeFirstChunk_returns_nothing()
        {
            int updates = 0;
            Papa.parse(new Uri("http://localhost:5588/long-sample.csv"), new Config()
            {
                chunkSize = 500,
                beforeFirstChunk = (chunk) =>
                {
                    return null;
                },
                step = (response, handle) =>
                {
                    updates++;
                },
                complete = (response) =>
                {
                    Assert.Equals(8, updates);
                }
            });

            System.Threading.Thread.Sleep(1000);

            Assert.IsTrue(updates == 8);
        }
        #endregion
    }
}
