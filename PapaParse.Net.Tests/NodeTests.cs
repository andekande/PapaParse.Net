using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace PapaParse.Net.Tests
{
    [TestClass]
    public class NodeTests
    {
        string longSampleRawCsv = String.Empty;

        [TestInitialize]
        public void loadTestFile()
        {
            longSampleRawCsv = File.ReadAllText("long-sample.csv");
        }

        private void assertLongSampleParsedCorrectly(Result parsedCsv)
        {
            Assert.AreEqual(8, parsedCsv.data.Count);

            CollectionAssert.AreEqual(parsedCsv.data[0], new List<string>() { 
                "Grant",
			    "Dyer",
			    "Donec.elementum@orciluctuset.example",
			    "2013-11-23T02:30:31-08:00",
			    "2014-05-31T01:06:56-07:00",
			    "Magna Ut Associates",
			    "ljenkins"
            });

            CollectionAssert.AreEqual(parsedCsv.data[7], new List<string>() { 
			    "Talon",
			    "Salinas",
			    "posuere.vulputate.lacus@Donecsollicitudin.example",
			    "2015-01-31T09:19:02-08:00",
			    "2014-12-17T04:59:18-08:00",
			    "Aliquam Iaculis Incorporate",
			    "Phasellus@Quisquetincidunt.example"
            });

            Assert.AreEqual(parsedCsv.meta, new Meta() { 
			    delimiter = ",",
			    linebreak = "\n",
			    aborted = false,
			    truncated = false,
			    cursor = 1209
            });

            Assert.AreEqual(parsedCsv.errors.Count, 0);
        }

        [TestMethod]
	    public void synchronouslyParsedCsvShouldBeCorrectlyParsed()
        {
		    assertLongSampleParsedCorrectly(Papa.parse(longSampleRawCsv, null));
	    }

        [TestMethod]
	    public void asynchronouslyParsedCsvShouldBeCorrectlyParsed()
        {
            Papa.parse(longSampleRawCsv, new Config()
            {
                complete = (parsedCsv) =>
                {
                    assertLongSampleParsedCorrectly(parsedCsv);
                }
            });
	    }

        [TestMethod]
        public void asynchronouslyParsedFileShouldBeCorrectlyParsed()
        {
            Papa.parse(File.OpenRead("long-sample.csv"), new Config()
            {
                chunkSize= 22,
                complete = (parsedCsv) =>
                {
                    assertLongSampleParsedCorrectly(parsedCsv);
                }
            });
        }
    }
}
