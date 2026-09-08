using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceViewer.Core.Analysis;

namespace TraceViewer.Core
{
    public class Project
    {
        public int Version { get; set; } = 2;
        public TraceData TraceData { get; set; } = new();
        public List<(int Id, string Text)> Comments { get; set; } = [];
        public HashSet<int> HiddenRows { get; set; } = [];
        public HashSet<int> DeObHiddenRows { get; set; } = [];
        public List<(int Id, string Name)> Blocks { get; set; } = [];
        public List<int> Bookmarks { get; set; } = [];
        public string Notes { get; set; } = "";

        // Dumpulator integration
        public string? DumpulatorScript { get; set; }
        public string? DumpFilePath { get; set; }
        public string? DumpFileName { get; set; }
        public bool HasDump => !string.IsNullOrWhiteSpace(DumpFilePath) && File.Exists(DumpFilePath);

        // Extensible metadata slot for future plugins and analyses
        public Dictionary<string, object> ExtraMetadata { get; set; } = new();
    }

    #region Manifest Data Transfer Objects

    public class ProjectManifest
    {
        public int Version { get; set; } = 2;
        public string AppVersion { get; set; } = "2.0";
        public string CreatedAt { get; set; } = "";
        public string LastModifiedAt { get; set; } = "";
        public string Notes { get; set; } = "";
        public TraceManifest Trace { get; set; } = new();
        public DumpulatorManifest Dumpulator { get; set; } = new();
        public List<CommentEntry> Comments { get; set; } = [];
        public List<int> HiddenRows { get; set; } = [];
        public List<int> DeObHiddenRows { get; set; } = [];
        public List<BlockEntry> Blocks { get; set; } = [];
        public List<int> Bookmarks { get; set; } = [];

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extensions { get; set; }
    }

    public class TraceManifest
    {
        public string OriginalFileName { get; set; } = "";
        public string EntryPath { get; set; } = "trace.trace64";
        public int TotalRows { get; set; }
        public string Arch { get; set; } = "x64";
    }

    public class DumpulatorManifest
    {
        public bool HasDump { get; set; }
        public string DumpOriginalFileName { get; set; } = "";
        public string DumpEntryPath { get; set; } = "dump.dmp";
        public long DumpSizeBytes { get; set; }
        public string ScriptEntryPath { get; set; } = "dumpulator/script.py";
    }

    public class CommentEntry
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }

    public class BlockEntry
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    public static class ProjectSessionManager
    {
        private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "TraceViewer");
        private static readonly string SessionsRoot = Path.Combine(TempRoot, "Sessions");

        public static string CreateSessionDirectory()
        {
            string sessionDir = Path.Combine(SessionsRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionDir);
            return sessionDir;
        }

        public static void PurgeTempOnStartup()
        {
            try
            {
                if (!Directory.Exists(TempRoot)) return;
                var di = new DirectoryInfo(TempRoot);
                foreach (var dir in di.GetDirectories())
                {
                    try
                    {
                        dir.Delete(true);
                    }
                    catch { }
                }
                foreach (var file in di.GetFiles())
                {
                    try
                    {
                        file.Delete();
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    public static class ProjectLoader
    {
        public static Project OpenProject(string filename)
        {
            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] magic = new byte[4];
            int read = fs.Read(magic, 0, 4);
            fs.Position = 0;

            // Check if legacy binary TRVI format
            if (read == 4 && magic[0] == 'T' && magic[1] == 'R' && magic[2] == 'V' && magic[3] == 'I')
            {
                return LegacyProjectLoader.OpenProject(fs);
            }

            // Otherwise load as standard extensible ZIP package
            return PackageProjectLoader.OpenProject(fs, filename);
        }
    }

    public static class ProjectWriter
    {
        public static void SaveProject(Project project, string filename)
        {
            PackageProjectWriter.SaveProject(project, filename);
        }
    }

    #region Package Project Loader & Writer (v2 ZIP-based)

    internal static class PackageProjectWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void SaveProject(Project project, string filename)
        {
            string targetDir = Path.GetDirectoryName(filename) ?? "";
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string tempFile = filename + ".tmp." + Guid.NewGuid().ToString("N");

            try
            {
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    // 1. Build Manifest
                    string dumpOriginalName = "";
                    long dumpSize = 0;
                    if (project.HasDump && !string.IsNullOrEmpty(project.DumpFilePath) && File.Exists(project.DumpFilePath))
                    {
                        dumpOriginalName = !string.IsNullOrEmpty(project.DumpFileName)
                            ? project.DumpFileName
                            : Path.GetFileName(project.DumpFilePath);
                        dumpSize = new FileInfo(project.DumpFilePath).Length;
                    }

                    var manifest = new ProjectManifest
                    {
                        Version = 2,
                        AppVersion = "2.0",
                        CreatedAt = DateTime.UtcNow.ToString("o"),
                        LastModifiedAt = DateTime.UtcNow.ToString("o"),
                        Notes = project.Notes,
                        HiddenRows = project.HiddenRows.ToList(),
                        DeObHiddenRows = project.DeObHiddenRows.ToList(),
                        Bookmarks = project.Bookmarks.ToList(),
                        Comments = project.Comments.Select(c => new CommentEntry { Id = c.Id, Text = c.Text }).ToList(),
                        Blocks = project.Blocks.Select(b => new BlockEntry { Id = b.Id, Name = b.Name }).ToList(),
                        Trace = new TraceManifest
                        {
                            OriginalFileName = !string.IsNullOrEmpty(project.TraceData?.Filename) ? Path.GetFileName(project.TraceData.Filename) : "trace.trace64",
                            EntryPath = "trace.trace64",
                            TotalRows = project.TraceData?.Trace.Count ?? 0,
                            Arch = project.TraceData?.Arch ?? "x64"
                        },
                        Dumpulator = new DumpulatorManifest
                        {
                            HasDump = project.HasDump,
                            DumpOriginalFileName = dumpOriginalName,
                            DumpEntryPath = "dump.dmp",
                            DumpSizeBytes = dumpSize,
                            ScriptEntryPath = "dumpulator/script.py"
                        }
                    };

                    // 2. Write project.json manifest
                    var manifestEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
                    using (var entryStream = manifestEntry.Open())
                    {
                        JsonSerializer.Serialize(entryStream, manifest, JsonOptions);
                    }

                    // 3. Write trace.trace64 payload
                    if (!string.IsNullOrEmpty(project.TraceData?.Filename) && File.Exists(project.TraceData.Filename))
                    {
                        var traceEntry = archive.CreateEntry("trace.trace64", CompressionLevel.Fastest);
                        using (var src = new FileStream(project.TraceData.Filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
                        using (var dest = traceEntry.Open())
                        {
                            src.CopyTo(dest);
                        }
                    }

                    // 4. Write dump.dmp if present
                    if (project.HasDump && !string.IsNullOrEmpty(project.DumpFilePath) && File.Exists(project.DumpFilePath))
                    {
                        var dumpEntry = archive.CreateEntry("dump.dmp", CompressionLevel.Fastest);
                        using (var src = new FileStream(project.DumpFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
                        using (var dest = dumpEntry.Open())
                        {
                            src.CopyTo(dest);
                        }
                    }

                    // 5. Write dumpulator/script.py
                    if (!string.IsNullOrWhiteSpace(project.DumpulatorScript))
                    {
                        var scriptEntry = archive.CreateEntry("dumpulator/script.py", CompressionLevel.Optimal);
                        using (var dest = scriptEntry.Open())
                        using (var writer = new StreamWriter(dest, Encoding.UTF8))
                        {
                            writer.Write(project.DumpulatorScript);
                        }
                    }
                }

                // Atomic move/replace
                if (File.Exists(filename))
                {
                    File.Move(tempFile, filename, overwrite: true);
                }
                else
                {
                    File.Move(tempFile, filename);
                }
            }
            catch
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                throw;
            }
        }
    }

    internal static class PackageProjectLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static Project OpenProject(Stream stream, string filename)
        {
            var project = new Project();
            string sessionDir = ProjectSessionManager.CreateSessionDirectory();

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            // 1. Read project.json
            var manifestEntry = archive.GetEntry("project.json");
            if (manifestEntry == null)
                throw new InvalidDataException("Invalid .tvproj package: 'project.json' manifest is missing.");

            ProjectManifest? manifest;
            using (var manifestStream = manifestEntry.Open())
            {
                manifest = JsonSerializer.Deserialize<ProjectManifest>(manifestStream, JsonOptions);
            }

            if (manifest == null)
                throw new InvalidDataException("Failed to deserialize 'project.json' manifest.");

            project.Version = manifest.Version;
            project.Notes = manifest.Notes ?? "";
            project.HiddenRows = manifest.HiddenRows != null ? new HashSet<int>(manifest.HiddenRows) : [];
            project.DeObHiddenRows = manifest.DeObHiddenRows != null ? new HashSet<int>(manifest.DeObHiddenRows) : [];
            project.Bookmarks = manifest.Bookmarks ?? [];

            if (manifest.Comments != null)
            {
                project.Comments = manifest.Comments.Select(c => (c.Id, c.Text)).ToList();
            }

            if (manifest.Blocks != null)
            {
                project.Blocks = manifest.Blocks.Select(b => (b.Id, b.Name)).ToList();
            }

            // 2. Extract and Load Trace
            string traceEntryName = manifest.Trace?.EntryPath ?? "trace.trace64";
            var traceEntry = archive.GetEntry(traceEntryName)
                             ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".trace64", StringComparison.OrdinalIgnoreCase));

            if (traceEntry == null)
                throw new InvalidDataException("Invalid .tvproj package: trace payload not found.");

            string traceTargetName = !string.IsNullOrWhiteSpace(manifest.Trace?.OriginalFileName)
                ? manifest.Trace.OriginalFileName
                : "trace.trace64";

            string extractedTracePath = Path.Combine(sessionDir, traceTargetName);
            using (var src = traceEntry.Open())
            using (var dst = new FileStream(extractedTracePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            {
                src.CopyTo(dst);
            }

            TraceHandler.OpenAndLoad(extractedTracePath);
            project.TraceData = TraceHandler.Trace!;

            // 3. Extract Dump (if present)
            if (manifest.Dumpulator != null && manifest.Dumpulator.HasDump)
            {
                string dumpEntryName = !string.IsNullOrWhiteSpace(manifest.Dumpulator.DumpEntryPath)
                    ? manifest.Dumpulator.DumpEntryPath
                    : "dump.dmp";

                var dumpEntry = archive.GetEntry(dumpEntryName)
                                ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase));

                if (dumpEntry != null)
                {
                    string dumpTargetName = !string.IsNullOrWhiteSpace(manifest.Dumpulator.DumpOriginalFileName)
                        ? manifest.Dumpulator.DumpOriginalFileName
                        : "dump.dmp";

                    string extractedDumpPath = Path.Combine(sessionDir, dumpTargetName);
                    using (var src = dumpEntry.Open())
                    using (var dst = new FileStream(extractedDumpPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                    {
                        src.CopyTo(dst);
                    }

                    project.DumpFilePath = extractedDumpPath;
                    project.DumpFileName = manifest.Dumpulator.DumpOriginalFileName;
                }
            }

            // 4. Read Dumpulator script
            var scriptEntry = archive.GetEntry("dumpulator/script.py")
                              ?? archive.GetEntry("script.py");
            if (scriptEntry != null)
            {
                using var src = scriptEntry.Open();
                using var reader = new StreamReader(src, Encoding.UTF8);
                project.DumpulatorScript = reader.ReadToEnd();
            }

            return project;
        }
    }

    #endregion

    #region Legacy Project Loader (v1 TRVI binary)

    internal readonly record struct FileHeader(char[] Magic, int Version, int BlockSize);
    internal readonly record struct BlockDescriptor(char[] SubMagic, int TraceLength);

    internal static class LegacyProjectLoader
    {
        public static Project OpenProject(Stream stream)
        {
            var project = new Project();
            string sessionDir = ProjectSessionManager.CreateSessionDirectory();

            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
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

            string sessionTraceFilename = Path.Combine(sessionDir, "legacy_trace.trace64");
            File.WriteAllBytes(sessionTraceFilename, traceDataBlock);
            TraceHandler.OpenAndLoad(sessionTraceFilename);
            project.TraceData = TraceHandler.Trace!;

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

    #endregion
}
