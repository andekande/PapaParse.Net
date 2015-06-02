PapaParse.Net
=========

Fast and reliable CSV parser based on [Papa Parse](http://papaparse.com).

This PCL has all the functionality of Papa Parse except for JSON to CSV parsing and asynchronous operations, which might be added in the Future. You can pass a String, a Stream or a Uri to PapaParse.Net and use any of the other config options as described in the [Papa Parse documentation](http://papaparse.com/docs).
By now the focus is on feature parity and reusability, that's why the API might look quite strange. Feel free to open an Issue to discuss how to make the API surface look familiar to what a .Net developer expects.

Features that got ported:

- Easy to use
- Parse CSV files directly (local or over the network)
- Fast mode
- Stream large files (even via HTTP)
- Auto-detect delimiter
- Header row support
- Pause, resume, abort

Papa Parse is a **Portable Class Library** and as such can be used in:

- .Net beginning with 4.0
- Windows 8
- Windows Phone 8
- Windows Phone Silverlight 8
- Silverlight 5

Basic Usage
-----

```csharp
// pass in the contents of a csv file
Result parsed = Papa.parse(csv);

// voila
List<List<string>> rows = parsed.data;
```


Parse File(s)
-----

Due to files being handled differently in the supported environments, PapaParse.Net will just accept Streams. By default streams are read in 10MB chunks.

```csharp
// Parse single file
using (FileStream stream = File.OpenRead(filename))
{
   Papa.parse(stream, new Config()
   {
      complete = (parsed) =>
      {
         List<List<string>> rows = parsed.data;
      }
    });
}
```

Download and Parse from Url
-----

By pointing to an Uri, PapaParse.Net requests the resource in a network friendly manner. By default requests are split in 5MB chunks.

```csharp
// Get a file from the Web
Papa.parse(new Uri("http://webserver.com/remotefile.csv"), new Config()
{
   chunkSize = 500 * 1024, //download in 500KB blocks
   complete = (parsed) =>
   {
      List<List<string>> rows = parsed.data;
   }
});
```


For a complete understanding of the power of this library, please refer to the [Papa Parse web site](http://papaparse.com).


Tests
-----

For PapaParse.Net all Tests of the JavaScript Library have been ported as well. There are 107 testcases in the project that can be run via MSTest.


License
-------

The original PapaParse is MIT licensed. So is PapaParse.Net.

For Ideas and Improvements reach out to Twitter @clientjs

![PapaParse.Net Logo](PapaParseNetLogo.jpg) 