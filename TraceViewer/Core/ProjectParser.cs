using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using TraceViewer.Core.Analysis;

namespace TraceViewer.Core
{
    public class Project
    {
        public TraceData TraceData { get; set; } = new();
        public List<(int Id, string Text)> Comments { get; set; } = [];
        public HashSet<int> HiddenRows { get; set; } = [];
        public HashSet<int> DeObHiddenRows { get; set; } = [];
        public List<(int Id, string Name)> Blocks { get; set; } = [];
        public string Notes { get; set; } = "";
    }

    internal readonly record struct FileHeader(char[] Magic, int Version, int BlockSize);
    internal readonly record struct BlockDescriptor(char[] SubMagic, int TraceLength);

    public class ProjectLoader
    {
        public static Project OpenProject(string filename)
        {
            var project = new Project();

            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fs);

            var header = ReadHeader(reader);

            if (new string(header.Magic) != "TRVI")
                throw new InvalidDataException("Invalid Magic!");
            if (header.Version != 1)
                throw new InvalidDataException("Invalid Version!");

            byte[] compressedBlock = reader.ReadBytes(header.BlockSize);

            byte[] decompressedBlock;
            using (var compressedStream = new MemoryStream(compressedBlock))
            using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            using (var decompressedStream = new MemoryStream())
            {
                deflateStream.CopyTo(decompressedStream);
                decompressedBlock = decompressedStream.ToArray();
            }

            BlockDescriptor descriptor;
            byte[] traceDataBlock;
            using (var decompressedMs = new MemoryStream(decompressedBlock))
            using (var decompressedReader = new BinaryReader(decompressedMs))
            {
                descriptor = ReadDescriptor(decompressedReader);
                if (new string(descriptor.SubMagic) != "DESC")
                    throw new InvalidDataException("Invalid SubMagic in descriptor.");

                traceDataBlock = decompressedReader.ReadBytes(descriptor.TraceLength);
            }

            string tempTraceFilename = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempTraceFilename, traceDataBlock);
                TraceHandler.OpenAndLoad(tempTraceFilename);
                project.TraceData = TraceHandler.Trace!;
            }
            finally
            {
                try { File.Delete(tempTraceFilename); } catch { /* best-effort cleanup */ }
            }

            using (var decompressedMs = new MemoryStream(decompressedBlock))
            using (var decompressedReader = new BinaryReader(decompressedMs))
            {
                decompressedMs.Position = 0;
                ReadDescriptor(decompressedReader); // skip descriptor
                decompressedReader.BaseStream.Seek(descriptor.TraceLength, SeekOrigin.Current);

                project.Comments = ReadComments(decompressedReader);
                project.HiddenRows = ReadHiddenRows(decompressedReader);
                project.DeObHiddenRows = ReadHiddenRows(decompressedReader);
                project.Blocks = ReadBlocks(decompressedReader);
                project.Notes = ReadNotes(decompressedReader);
            }

            return project;
        }

        private static FileHeader ReadHeader(BinaryReader reader) =>
            new(reader.ReadChars(4), reader.ReadInt32(), reader.ReadInt32());

        private static BlockDescriptor ReadDescriptor(BinaryReader reader) =>
            new(reader.ReadChars(4), reader.ReadInt32());

        private static List<(int Id, string Text)> ReadComments(BinaryReader reader)
        {
            int commentCount = reader.ReadInt32();
            var comments = new List<(int, string)>(commentCount);
            for (int i = 0; i < commentCount; i++)
            {
                int id = reader.ReadInt32();
                short len = reader.ReadInt16();
                string comment = new string(reader.ReadChars(len));
                comments.Add((id, comment));
            }
            return comments;
        }

        private static HashSet<int> ReadHiddenRows(BinaryReader reader)
        {
            int hiddenRowCount = reader.ReadInt32();
            var hiddenRows = new HashSet<int>(hiddenRowCount);
            for (int i = 0; i < hiddenRowCount; i++)
                hiddenRows.Add(reader.ReadInt32());
            return hiddenRows;
        }

        private static List<(int Id, string Name)> ReadBlocks(BinaryReader reader)
        {
            int blockCount = reader.ReadInt32();
            var blocks = new List<(int, string)>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                int id = reader.ReadInt32();
                short len = reader.ReadInt16();
                string block = new string(reader.ReadChars(len));
                blocks.Add((id, block));
            }
            return blocks;
        }

        private static string ReadNotes(BinaryReader reader) =>
            Encoding.UTF8.GetString(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
    }

    public class ProjectWriter
    {
        public static void SaveProject(Project project, string filename)
        {
            using var fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);

            byte[] traceData = File.ReadAllBytes(project.TraceData.Filename);
            byte[] commentsData = WriteComments(project.Comments);
            byte[] hiddenRowsData = WriteHiddenRows(project.HiddenRows);
            byte[] deObHiddenRowsData = WriteHiddenRows(project.DeObHiddenRows);
            byte[] blocks = WriteBlocks(project.TraceData.Trace);
            byte[] notesData = Encoding.UTF8.GetBytes(project.Notes);

            using var decompressedMs = new MemoryStream();
            using (var decompressedWriter = new BinaryWriter(decompressedMs, Encoding.UTF8, leaveOpen: true))
            {
                WriteDescriptor(decompressedWriter, traceData.Length);
                decompressedWriter.Write(traceData);
                decompressedWriter.Write(commentsData);
                decompressedWriter.Write(hiddenRowsData);
                decompressedWriter.Write(deObHiddenRowsData);
                decompressedWriter.Write(blocks);
                decompressedWriter.Write(notesData);
            }

            byte[] decompressedBlock = decompressedMs.ToArray();

            byte[] compressedBlock;
            using (var compressedStream = new MemoryStream())
            {
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress, leaveOpen: true))
                    deflateStream.Write(decompressedBlock, 0, decompressedBlock.Length);
                compressedBlock = compressedStream.ToArray();
            }

            WriteHeader(writer, compressedBlock.Length);
            writer.Write(compressedBlock);
        }

        private static void WriteHeader(BinaryWriter writer, int blockSize)
        {
            writer.Write("TRVI".ToCharArray());
            writer.Write(1); // Version
            writer.Write(blockSize);
        }

        private static void WriteDescriptor(BinaryWriter writer, int traceLength)
        {
            writer.Write("DESC".ToCharArray());
            writer.Write(traceLength);
        }

        private static byte[] WriteComments(List<(int Id, string Text)> comments)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(comments.Count);
            foreach (var (id, text) in comments)
            {
                writer.Write(id);
                writer.Write((short)text.Length);
                writer.Write(text.ToCharArray());
            }
            return ms.ToArray();
        }

        private static byte[] WriteHiddenRows(HashSet<int> hiddenRows)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(hiddenRows.Count);
            foreach (var hiddenRow in hiddenRows)
                writer.Write(hiddenRow);
            return ms.ToArray();
        }

        private static byte[] WriteBlocks(List<TraceRow> traceRows)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            var blockRows = new List<TraceRow>();
            foreach (var row in traceRows)
            {
                if (row.isBlockStart)
                    blockRows.Add(row);
            }

            writer.Write(blockRows.Count);
            foreach (var row in blockRows)
            {
                writer.Write(row.Id);
                writer.Write((short)row.block.Length);
                writer.Write(row.block.ToCharArray());
            }
            return ms.ToArray();
        }
    }
}
