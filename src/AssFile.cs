using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SnakeUnity.ArchiveStaySimple
{
    public class AssFile : IDisposable
    {
        public AssFile()
        {
        }

        private readonly List<AssHeader> _headers = new List<AssHeader>();

        private readonly Dictionary<string, string> _fileReference = new Dictionary<string, string>();

        private Stream _oldFileStream;

        public IList<AssHeader> Headers
        {
            get { return _headers.AsReadOnly(); }
        }

        public void AddFile(string targetPath, string actualFilePath)
        {
            if (_headers.Any(_ => _.fileName == targetPath))
                throw new IOException("target file path already exists:" + targetPath);
            if (!File.Exists(actualFilePath))
                throw new IOException("file not found:" + actualFilePath);
            var s = new AssHeader
            {
                fileName = targetPath
            };
            _headers.Add(s);
            _fileReference.Add(targetPath, actualFilePath);
        }

        public void AddDirectory(string dirPath, IEnumerable<string> excludes)
        {
            var dir = dirPath;
            var subStrLength = dir.Length;
            LoopDir(dir, excludes, subStrLength);
        }

        private void LoopDir(string dir, IEnumerable<string> excludes, int subStrLength)
        {
            var files = Directory.GetFiles(dir);
            foreach (var f in files)
            {
                var ff = f.Substring(subStrLength + 1);
                if (excludes != null)
                {
                    bool excluded = false;
                    foreach (var exc in excludes)
                    {
                        if (Regex.IsMatch(ff, exc))
                        {
                            excluded = true;
                            break;
                        }
                    }

                    if (excluded)
                        continue;
                }

                AddFile(ff, f);
            }

            var dirs = Directory.GetDirectories(dir);
            foreach (var subDir in dirs)
            {
                LoopDir(subDir, excludes, subStrLength);
            }
        }

        public void RemoveFile(string targetPath)
        {
            var item = _headers.FirstOrDefault(_ => _.fileName == targetPath);
            if (item != null)
                _headers.Remove(item);
        }

        public void ReadFrom(string path)
        {
            if (!File.Exists(path))
            {
                throw new IOException("file not found:" + path);
            }

            var stream = File.OpenRead(path);
            ReadFrom(stream);
        }

        public void ReadFrom(Stream stream)
        {
            _headers.Clear();
            if (_oldFileStream != null)
                _oldFileStream.Close();
            try
            {
                _oldFileStream = AssParser.ReadHeaderFromBinary(stream, _headers);
            }
            catch
            {
                stream.Close();
                throw;
            }
        }

        public void WriteTo(string path)
        {
            AssParser.ToBinary(_headers, _oldFileStream, _fileReference, path);
        }

        public void WriteTo(Stream stream)
        {
            AssParser.ToBinary(_headers, _oldFileStream, _fileReference, stream);
        }

        public void Extract(string tarDir, bool overwrite)
        {
            AssParser.ExtractBinary(_headers, _oldFileStream, tarDir, overwrite);
        }

        public void Close()
        {
            if (_oldFileStream != null)
                _oldFileStream.Close();
        }

        public void Dispose()
        {
            Close();
        }
    }
}