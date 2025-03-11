using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TraceViewer.Core
{
    public class Project
    {
        public TraceData TraceData;

        public List<Tuple<int, string>> Comments;

        public HashSet<int> HiddenRows;

        public string Name;

        public string Notes;

    }

    internal class Header
    {
        public char[] Magic; // "TRVI"
        public int Version; // 1
        public short NameLength;
        public string TraceName;
        public int BlockSize;
    }

    internal class Descriptor
    {
        public char[] SubMagic; // "DESC"
        public int TraceLength;
    }


    public class ProjectLoader
    {

        public static Project OpenProject(string filename)
        {
            Project project = new Project();
            string tempTraceFilename = null;

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                Header header = ReadHeader(reader);

                if (new string(header.Magic) != "TRVI")
                {
                    throw new InvalidDataException("Invalid Magic!");
                }
                if (header.Version != 1)
                {
                    throw new InvalidDataException("Invalid Version!");
                }
                project.Name = header.TraceName;

                byte[] compressedBlock = reader.ReadBytes(header.BlockSize);

                byte[] decompressedBlock;
                using (MemoryStream compressedStream = new MemoryStream(compressedBlock))
                using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    deflateStream.CopyTo(decompressedStream);
                    decompressedBlock = decompressedStream.ToArray();
                }

                Descriptor descriptor;
                byte[] traceDataBlock;
                using (MemoryStream decompressedMs = new MemoryStream(decompressedBlock))
                using (BinaryReader decompressedReader = new BinaryReader(decompressedMs))
                {
                    descriptor = ReadDescriptor(decompressedReader);

                    if (new string(descriptor.SubMagic) != "DESC")
                    {
                        throw new InvalidDataException("Ungültige SubMagic Number im Descriptor.");
                    }

                    traceDataBlock = decompressedReader.ReadBytes(descriptor.TraceLength);
                }

                tempTraceFilename = Path.GetTempFileName();
                File.WriteAllBytes(tempTraceFilename, traceDataBlock);

                TraceHandler.OpenAndLoad(tempTraceFilename);

                project.TraceData = TraceHandler.Trace;

                tempTraceFilename = null;

                using (MemoryStream decompressedMs = new MemoryStream(decompressedBlock))
                using (BinaryReader decompressedReader = new BinaryReader(decompressedMs))
                {
                    decompressedMs.Position = 0;

                    ReadDescriptor(decompressedReader);

                    decompressedReader.BaseStream.Seek(descriptor.TraceLength, SeekOrigin.Current);

                    project.Comments = ReadComments(decompressedReader);

                    project.HiddenRows = ReadHiddenRows(decompressedReader);

                    project.Notes = ReadNotes(decompressedReader);
                }
                
            }
            return project;
        }

        private static Header ReadHeader(BinaryReader reader)
        {
            Header header = new Header();
            header.Magic = reader.ReadChars(4);
            header.Version = reader.ReadInt32();
            header.NameLength = reader.ReadInt16();
            header.TraceName = new string(reader.ReadChars(header.NameLength));
            header.BlockSize = reader.ReadInt32();
            return header;
        }

        private static Descriptor ReadDescriptor(BinaryReader reader)
        {
            Descriptor descriptor = new Descriptor();
            descriptor.SubMagic = reader.ReadChars(4);
            descriptor.TraceLength = reader.ReadInt32();
            return descriptor;
        }


        private static List<Tuple<int, string>> ReadComments(BinaryReader reader)
        {
            List<Tuple<int, string>> comments = new List<Tuple<int, string>>();
            int commentCount = reader.ReadInt32();
            for (int i = 0; i < commentCount; i++)
            {
                int id = reader.ReadInt32();
                short len = reader.ReadInt16();
                string comment = new string(reader.ReadChars(len));
                comments.Add(new Tuple<int, string>(id, comment));
            }
            return comments;
        }

        private static HashSet<int> ReadHiddenRows(BinaryReader reader)
        {
            HashSet<int> hiddenRows = new HashSet<int>();
            int hiddenRowCount = reader.ReadInt32();
            for (int i = 0; i < hiddenRowCount; i++)
            {
                hiddenRows.Add(reader.ReadInt32());
            }
            return hiddenRows;
        }

        private static string ReadNotes(BinaryReader reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
        }

        public static string ReadToEnd(BinaryReader reader)
        {
            StringBuilder sb = new StringBuilder();
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                sb.Append(reader.ReadChar()); 
            }
            return sb.ToString();
        }
    }

    public class ProjectWriter
    {
        public static void SaveProject(Project project, string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                byte[] traceData = File.ReadAllBytes(project.TraceData.Filename);

                byte[] commentsData = WriteComments(project.Comments);

                byte[] hiddenRowsData = WriteHiddenRows(project.HiddenRows);

                byte[] notesData = Encoding.UTF8.GetBytes(project.Notes);

                using (MemoryStream decompressedMs = new MemoryStream())
                using (BinaryWriter decompressedWriter = new BinaryWriter(decompressedMs))
                {
                    WriteDescriptor(decompressedWriter, traceData.Length);
                    decompressedWriter.Write(traceData);
                    decompressedWriter.Write(commentsData);
                    decompressedWriter.Write(hiddenRowsData);
                    decompressedWriter.Write(notesData);

                    byte[] decompressedBlock = decompressedMs.ToArray();

                    // Komprimieren
                    byte[] compressedBlock;
                    using (MemoryStream compressedStream = new MemoryStream())
                    using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                    {
                        deflateStream.Write(decompressedBlock, 0, decompressedBlock.Length);
                        deflateStream.Close();
                        compressedBlock = compressedStream.ToArray();
                    }

                    WriteHeader(writer, project.Name, compressedBlock.Length);

                    writer.Write(compressedBlock);
                }
            }
        }

        private static void WriteHeader(BinaryWriter writer, string traceName, int blockSize)
        {
            writer.Write("TRVI".ToCharArray());
            writer.Write(1); // Version
            writer.Write((short)traceName.Length);
            writer.Write(traceName.ToCharArray());
            writer.Write(blockSize);
        }

        private static void WriteDescriptor(BinaryWriter writer, int traceLength)
        {
            writer.Write("DESC".ToCharArray());
            writer.Write(traceLength);
        }

        private static byte[] WriteComments(List<Tuple<int, string>> comments)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(comments.Count);
                foreach (var comment in comments)
                {
                    writer.Write(comment.Item1);
                    writer.Write((short)comment.Item2.Length);
                    writer.Write(comment.Item2.ToCharArray()); 
                }
                return ms.ToArray();
            }
        }

        private static byte[] WriteHiddenRows(HashSet<int> hiddenRows)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(hiddenRows.Count);
                foreach (var hiddenRow in hiddenRows)
                {
                    writer.Write(hiddenRow);
                }
                return ms.ToArray();
            }
        }
    }
}
