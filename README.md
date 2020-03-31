# ArchiveStaySimple

short for ASS, yes I chose this TROLLING name because there're so many archiving library and softwares out there but in my case it's too complicated and too big of size to use them, so I had to write a simple one to solve this.

But as you stumble upon this, I'll tell you what it's capable of, anyway.

**if you're using C#3.5 and under, and sick of dealing zip filename encoding issue, this is for you!**

* Super small
* Simple to use
* Stores only relative path and file content, no permissions and what not, **simple**!
* No compressing or decompressing, only stitching files together and prevserve their relative path, **simple**!
* Won't use a lot of memory.
* Uses utf-8 encoding for saving the filename, no encoding issue, **bonus 1**!
* Supports long filename, **bonus 2**!

OMG, that's it, I wish it has more features, but no, that's it.

## To Install

Just clone or download this repo and put the files in the `src` folder to your project, then done!

## To Use

Creating a ASS file:

```c#
// remember to do this at the top the your .cs file.
using SnakeUnity.ArchiveStaySimple;

var assFileName = "test.ass";
var assFile = new AssFile();
// add a single file
assFile.AddFile("test.txt", "c:\\test.txt"); // the first argument is the relative path of the archive, the second argument is the actual file path for reading for archiving.
// add a directory relative to root
assFile.AddDirectory("c:\\test dir", new[]{@"^\..+$", @"^.+\.meta$"}); // note that second argument is a list of RegExps, not the usal matching pattern.
// remove a file
assFile.RemoveFile("test.txt"); //removing file using the relative path not the actual path.
// unfortunately, there's no 'RemoveDirectory' method
assFile.WriteTo(assFileName);
```

Reading and Extracting a ASS file:

```c#
// remember to do this at the top the your .cs file.
using SnakeUnity.ArchiveStaySimple;
using System;

var assFileName = "test.ass";
var targetPath = "c:\\test";
// using the "using" enclosure will automatically call "Dispose" to the ass file which is crucial, because it's dealing with the file system, not closing a file is always bad!
using(var assFile = new AssFile())
{
    // reading a ass file is just reading the "header"s from it, this won't suddenly read all the things and take much of your memory. 
    assFile.ReadFrom(assFileName);
    // you can loop through assFile.Headers to check what's in the ass file.
    foreach(var header in assFile.Headers)
    {
        Console.WriteLine(header.fileName);
    }
    // finally the "Extract" method will do a unarchiving action, this will directory extract ass file to disk, the memory usage is just a buffer like 100k
    assFile.Extract(targetPath, true);
}
```

Compressing and Decompressing the ass file: 

Like I said, ASS doesn't include (de)compressing, but you can compress it your self easily:

```c#
using SnakeUnity.ArchiveStaySimple;
using System.IO;
using System.IO.Compression;

// to compress
var tarfile = "test.ass.gz";
using(var fs = File.Open(tarfile, FileMode.OpenOrCreate))
using(var deflateStream = new DeflateStream(fs, CompressionMode.Compress))
{
    var assFile = new AssFile();
    assFile.AddDirectory("c:\\my_test_dir");
    assFile.WriteTo(deflateStream);
}

// to decompress
var tempFile = Path.GetTempFileName();
using(var tfs = File.Open(tempFile, FileMode.Create))
using(var deflateStream = new DeflateStream(tfs, CompressMode.Decompress))
{
    using(var fs = File.Open(tempFile, FileMode.Open))
    {
        fs.CopyTo(deflateStream);
        deflateStream.Flush();
    }
    // this step is crucial, we must read the filestream from the start.
    tfs.Position = 0;
    var assFile = new AssFile();
    assFile.ReadFrom(tfs);
    assFile.Extract("c:\\my_test_decompress_dir", true);
    //here we don't have to call assFile.Close() or assFile.Dispose(), because the using block will dispose the file for us.
}
```

## Command Line Support

Why not try the good old `tar` command? You're using Windows? Well, I guess you have to support it by yourself!



## License

License? Forget about the license, just take it, no thanks!