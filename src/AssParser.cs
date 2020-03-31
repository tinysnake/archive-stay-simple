using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SnakeUnity.ArchiveStaySimple
{
    public static class AssParser
    {
        private static readonly byte[] END_OF_HEADER_SIGNATURE = {0x1C, 0, 0, 0, 0x1C, 0, 0, 0};

        public static void ToBinary(IList<AssHeader> headers, Stream oldFileStream, Dictionary<string, string> fileRefs, string targetPath)
        {
            var tempFile = Path.GetTempFileName();
            using (var fs = File.Open(tempFile, FileMode.OpenOrCreate))
            {
                ToBinary(headers, oldFileStream, fileRefs, fs);
                var ofs = oldFileStream as FileStream;
                if (ofs != null && Path.GetFullPath(targetPath) == ofs.Name)
                {
                    CopyStream(fs, ofs);
                }
                else
                {
                    using (var tfs = File.Open(targetPath, FileMode.OpenOrCreate))
                    {
                        CopyStream(fs, tfs);
                    }
                }
            }
        }

        public static void ToBinary(IList<AssHeader> headers, Stream oldFileStream, Dictionary<string, string> fileRefs, Stream stream)
        {
            if (!stream.CanWrite)
                throw new IOException("cant write to stream");
            var tempStream = stream;
            if (!stream.CanSeek)
            {
                var tempFile = Path.GetTempFileName();
                stream = File.Open(tempFile, FileMode.OpenOrCreate);
            }

            var piList = new List<AssParseInfo>();
            foreach (var header in headers)
            {
                piList.Add(WriteHeader(stream, header));
            }

            stream.Write(END_OF_HEADER_SIGNATURE, 0, END_OF_HEADER_SIGNATURE.Length);

            foreach (var header in headers)
            {
                WriteContent(stream, header, oldFileStream, fileRefs, piList);
            }

            if (tempStream != stream)
            {
                CopyStream(stream, tempStream, true, false);
                stream.Close();
            }
        }

        public static void ReadHeaderFromBinary(string filePath, IList<AssHeader> headers)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);
            using (var fs = File.OpenRead(filePath))
            {
                ReadHeaderFromBinary(fs, headers);
            }
        }

        public static Stream ReadHeaderFromBinary(Stream fileStream, IList<AssHeader> headers)
        {
            if (!fileStream.CanRead)
                throw new ArgumentException("fileStream has no permission to read");
            if (!fileStream.CanSeek)
            {
                var tempFile = Path.GetTempFileName();
                var stream = File.Open(tempFile, FileMode.OpenOrCreate);
                CopyStream(fileStream, stream, false, false);
                stream.Position = 0;
                fileStream.Close();
                fileStream = stream;
            }

            using (var ms = new MemoryStream())
            {
                var len = END_OF_HEADER_SIGNATURE.Length;
                var eohBuffer = new byte[len];
                while (fileStream.Position < fileStream.Length)
                {
                    var b = (byte) fileStream.ReadByte();
                    ms.WriteByte(b);
                    if (ms.Length < len) continue;
                    ms.Position = ms.Length - len;
                    ms.Read(eohBuffer, 0, len);
                    if (END_OF_HEADER_SIGNATURE.SequenceEqual(eohBuffer))
                    {
                        ms.SetLength(ms.Length - len);
                        ReadHeaders(ms, headers);
                        return fileStream;
                    }
                }

                throw new NotSupportedException("this is not a valid ass file.");
            }
        }

        public static void ExtractBinary(List<AssHeader> headers, Stream stream, string tarDir, bool overwrite)
        {
            ExtractBinaryWithItemComplete(headers, stream, tarDir, overwrite, null);
        }

        private static void ExtractBinaryWithItemComplete(List<AssHeader> headers, Stream stream, string tarDir, bool overwrite, Action onItemComplete)
        {
            if (!Directory.Exists(tarDir))
            {
                Directory.CreateDirectory(tarDir);
            }

            foreach (var h in headers)
            {
                var tarPath = Path.Combine(tarDir, h.fileName);
                if (File.Exists(tarPath) && !overwrite)
                {
                    throw new IOException("file already exists:" + tarPath);
                }

                var fileDir = Path.GetDirectoryName(tarPath);
                if (!Directory.Exists(fileDir))
                    Directory.CreateDirectory(fileDir);

                using (var f = File.Open(tarPath, FileMode.OpenOrCreate))
                {
                    f.Position = 0;
                    f.SetLength(0);
                    stream.Position = h.startPos;
                    var len = h.endPos - h.startPos;
                    WriteToStream(stream, f, len);
                }

                if (onItemComplete != null)
                    onItemComplete();
            }
        }

        public static void ExtractBinaryAsync(List<AssHeader> headers, Stream stream, string tarDir, bool overwrite, Action<float> progress, Action onComplete)
        {
            ThreadPool.QueueUserWorkItem(ExtractBinaryAsyncCallback, new ExtractState
            {
                headers = headers,
                stream = stream,
                tarDir = tarDir,
                overwrite = overwrite,
                onProgress = progress,
                onComplete = onComplete
            });
        }

        private static void ExtractBinaryAsyncCallback(object state)
        {
            var es = state as ExtractState;

            var count = 0;
            Action fn = () =>
            {
                count++;
                if (es.onProgress != null)
                    es.onProgress((float) count / es.headers.Count);
            };

            ExtractBinaryWithItemComplete(es.headers, es.stream, es.tarDir, es.overwrite, fn);
            if (es.onComplete != null)
                es.onComplete();
        }

        private static AssParseInfo WriteHeader(Stream stream, AssHeader header)
        {
            stream.Write(header.fileName);
            var pi = new AssParseInfo
            {
                fileName = header.fileName,
                posIndex = stream.Position,
            };
            stream.Write(0L);
            stream.Write(0L);
            return pi;
        }

        private static void WriteContent(Stream fs, AssHeader header, Stream oldFileStream, Dictionary<string, string> fileRefs, List<AssParseInfo> piList)
        {
            var index = fs.Position;
            var endIndex = 0L;
            if (header.startPos == 0 && header.endPos == 0)
            {
                if (!fileRefs.ContainsKey(header.fileName) || !File.Exists(fileRefs[header.fileName]))
                    throw new ArgumentException("cannot find a source file to be archived, filename:" + header.fileName);
                var filePath = fileRefs[header.fileName];
                try
                {
                    using (var f = File.OpenRead(filePath))
                    {
                        endIndex = WriteToStream(f, fs);
                    }
                }
                catch (Exception e)
                {
                    throw new IOException("cannot read:" + filePath, e);
                }
            }
            else if (header.startPos < header.endPos)
            {
                throw new InvalidOperationException("AssHeader start position is greater than end position");
            }
            else if (oldFileStream.Length < header.endPos)
            {
                throw new ArgumentOutOfRangeException("endPos", "AssHeader end position is out range from the old file stream");
            }
            else if (!oldFileStream.CanSeek || !oldFileStream.CanRead)
            {
                throw new IOException("cant read or seek the olfFileStream");
            }
            else
            {
                oldFileStream.Position = header.startPos;
                var len = header.endPos - header.startPos;
                endIndex = WriteToStream(oldFileStream, fs, len);
            }

            if (endIndex > index)
            {
                var pi = piList.First(_ => _.fileName == header.fileName);
                WriteBackPosition(fs, pi, index, endIndex);
            }
            else throw new IOException("start index is greater than end index");
        }

        private static long WriteToStream(Stream source, Stream target, long length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");
            var buffer = new byte[1 << 20];
            var read = source.Read(buffer, 0, (int) Math.Min(buffer.Length, length));
            while (read > 0)
            {
                length -= read;
                target.Write(buffer, 0, read);
                if (length > 0)
                    read = source.Read(buffer, 0, (int) Math.Min(buffer.Length, length));
                else
                    break;
            }

            return target.Position;
        }

        private static long WriteToStream(Stream source, Stream target)
        {
            var buffer = new byte[1 << 20];

            var read = source.Read(buffer, 0, buffer.Length);
            while (read > 0)
            {
                target.Write(buffer, 0, read);
                read = source.Read(buffer, 0, buffer.Length);
            }

            return target.Position;
        }

        private static void WriteBackPosition(Stream stream, AssParseInfo pi, long start, long end)
        {
            var pos = stream.Position;
            stream.Position = pi.posIndex;
            stream.Write(start);
            stream.Write(end);
            stream.Position = pos;
        }

        private static void Write(this Stream stream, long l)
        {
            var bytes = BitConverter.GetBytes(l);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void Write(this Stream stream, int i)
        {
            var bytes = BitConverter.GetBytes(i);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void Write(this Stream stream, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var lenBytes = BitConverter.GetBytes((short) bytes.Length);
            stream.Write(lenBytes, 0, lenBytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void ReadHeaders(Stream headerStream, IList<AssHeader> headers)
        {
            headerStream.Position = 0;
            while (headerStream.Position < headerStream.Length)
            {
                var fileName = headerStream.ReadString();
                var startPos = headerStream.ReadInt64();
                var endPos = headerStream.ReadInt64();
                headers.Add(new AssHeader
                {
                    fileName = fileName,
                    startPos = startPos,
                    endPos = endPos
                });
            }
        }

        private static long ReadInt64(this Stream stream)
        {
            var bytes = new byte[sizeof(long)];
            var read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
                throw new EndOfStreamException();
            return BitConverter.ToInt64(bytes, 0);
        }

        private static int ReadInt32(this Stream stream)
        {
            var bytes = new byte[sizeof(int)];
            var read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
                throw new EndOfStreamException();
            return BitConverter.ToInt32(bytes, 0);
        }

        private static string ReadString(this Stream stream)
        {
            var lenBytes = new byte[sizeof(short)];
            var read = stream.Read(lenBytes, 0, lenBytes.Length);
            if (read != lenBytes.Length)
                throw new EndOfStreamException();
            var len = BitConverter.ToInt16(lenBytes, 0);
            var bytes = new byte[len];
            read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
                throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        private static void CopyStream(Stream source, Stream target, bool resetSourcePosition = true, bool resetTargetPosition = true)
        {
            if (resetTargetPosition)
                target.Position = 0;
            if (resetSourcePosition)
                source.Position = 0;
            var buffer = new byte[1 << 20];
            var read = source.Read(buffer, 0, buffer.Length);
            while (read > 0)
            {
                target.Write(buffer, 0, read);
                read = source.Read(buffer, 0, buffer.Length);
            }
        }

        private class ExtractState
        {
            public List<AssHeader> headers;
            public Stream stream;
            public string tarDir;
            public bool overwrite;
            public Action<float> onProgress;
            public Action onComplete;
        }
    }
}