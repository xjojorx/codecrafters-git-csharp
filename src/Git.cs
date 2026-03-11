using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CodeCrafters.Git;

public static class Git
{
#if DEBUG
    const bool DOWRITE = false;
#else
    const bool DOWRITE = true;
#endif

    public static void Init()
    {
        Directory.CreateDirectory(".git");
        Directory.CreateDirectory(".git/objects");
        Directory.CreateDirectory(".git/refs");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
        Console.WriteLine("Initialized git directory");
    }

    public static void CatFile(ReadOnlySpan<string> args)
    {
        string blobHash = args[^1];
        string subpath = blobHash.Insert(2, "/");
        string path = Path.Join(GetGitDir(), "objects", subpath);

        Span<byte> content = ReadBlobFile(path);

        int nullIdx = content.IndexOf((byte)0);
        int len = int.Parse(Encoding.UTF8.GetString(content.Slice(5, nullIdx - 5)));
        Span<byte> blob = content.Slice(nullIdx + 1, len);
        Console.Write(Encoding.UTF8.GetString(blob));
    }

    public static string HashObject(ReadOnlySpan<string> args)
    {
        bool writeObject = args.Contains<string>("-w");
        string filePath = args[^1];
        var hash = Convert.ToHexStringLower(HashObject(filePath, writeObject));
        Console.WriteLine(hash);
        return hash;
    }
    public static byte[] HashObject(string filePath, bool writeObject)
    {
        var fileContent = File.ReadAllBytes(filePath);
        byte[] blob;
        using (var ms = new MemoryStream())
        {
            ms.Write(Encoding.UTF8.GetBytes(String.Concat("blob ", fileContent.Length, "\0")));
            ms.Write(fileContent);
            blob = ms.ToArray();
        }
        var hash = SHA1.HashData(blob);
        var hashStr = Convert.ToHexStringLower(hash);

        if (writeObject)
        {
            WriteObject(hashStr, blob);
        }
        return hash;
    }

    public static void LsTree(ReadOnlySpan<string> args)
    {
        bool nameOnly = args.Contains("--name-only");
        string treeHash = args[^1];

        string subpath = treeHash.Insert(2, "/");
        string path = Path.Join(GetGitDir(), "objects", subpath);
        Span<byte> content = ReadBlobFile(path);

        int nullIdx = content.IndexOf((byte)0);
        // Console.WriteLine(Encoding.UTF8.GetString(content));
        int len = int.Parse(Encoding.UTF8.GetString(content.Slice(5, nullIdx - 5)));
        Span<byte> blob = content.Slice(nullIdx + 1, len);

        List<TreeEntry> entries = new();
        var rem = blob;
        while (rem.Length > 0)
        {
            var spaceIdx = rem.IndexOf((byte)' ');
            var mode = int.Parse(Encoding.UTF8.GetString(rem.Slice(0, spaceIdx)));
            nullIdx = rem.IndexOf((byte)0);
            var name = Encoding.UTF8.GetString(rem.Slice(spaceIdx + 1, nullIdx - (spaceIdx + 1)));
            var hash = rem.Slice(nullIdx + 1, 20).ToArray();
            var entry = new TreeEntry
            {
                Hash = hash,
                Name = name,
                Mode = mode
            };
            entries.Add(entry);

            //iterate
            rem = rem.Slice(nullIdx + 1 + 20);
        }

        //print
        foreach (var treeEntry in entries)
        {
            if (nameOnly)
            {
                Console.WriteLine(treeEntry.Name);
            }
            else
            {
                string kind = treeEntry.Mode == 40000 ? "tree" : "blob";
                Console.WriteLine($"{treeEntry.Mode:D6} {kind} {Convert.ToHexStringLower(treeEntry.Hash)}    {treeEntry.Name}");
            }
        }
    }

    public static void WriteTree()
    {
        // var entries = GetStagingArea();
        // NOTE: should check files in the staging area and build a tree with the result of writing those entries
        // for now and as required for the challenge, just write the tree for "."
        var hash = WriteTree(Directory.GetCurrentDirectory());
        Console.WriteLine(Convert.ToHexStringLower(hash));
    }
    private static byte[] WriteTree(string path)
    {
        FileInfo fi = new(path);
        if (!IsDirectory(fi))
        {
            // is a file
            return HashObject(path, true);
        }

        //is a dir
        var entries = new List<TreeEntry>(capacity: 64);
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            if (Path.GetFileName(entry) == ".git")
            {
                continue;
            }
            if (Path.GetFileName(entry) == "bin")
            {
                continue;
            }
            if (Path.GetFileName(entry) == "obj")
            {
                continue;
            }
            if (Path.GetFileName(entry) == ".codecrafters")
            {
                continue;
            }
            if (Path.GetFileName(entry) == ".idea")
            {
                continue;
            }
            string name = entry;
            var file = new FileInfo(entry);
            int mode = GetFileMode(file);
            byte[] hash = WriteTree(entry);

            entries.Add(new TreeEntry
            {
                Hash = hash,
                Name = name,
                Mode = mode,
            });
        }

        byte[] treeHash = HashTree(entries);
        return treeHash;
    }

    public static void CommitTree(ReadOnlySpan<string> args)
    {
        //NOTE: this almost requires a better argument parser
        string treeHash = args[0];
        string parentHash = args[2];
        string message = args[4];

        string gitName = "John Doe";
        string gitEmail = "john@example.com";
        string dateStr = "1234567890 +0000";

        var sb = new StringBuilder();
        sb.AppendLine($"tree {treeHash}");
        sb.AppendLine($"parent {parentHash}");
        sb.AppendLine($"author {gitName} <{gitEmail}> {dateStr}");
        sb.AppendLine($"committer {gitName} <{gitEmail}> {dateStr}");
        sb.AppendLine();
        sb.AppendLine(message);
        var bodyStr = sb.ToString();

        byte[] content;
        using (var sink = new MemoryStream())
        {
            var body = Encoding.UTF8.GetBytes(bodyStr);
            sink.Write(Encoding.UTF8.GetBytes($"commit {body.Length}"));
            sink.WriteByte(0);
            sink.Write(body);
            content = sink.ToArray();
        }

        byte[] hash = SHA1.HashData(content);
        string hashStr = Convert.ToHexStringLower(hash);
        WriteObject(hashStr, content);

        Console.WriteLine(hashStr);
    }


    private static string GetGitDir()
    {
        //NOTE: should recursively search up the tree
        return ".git";
    }

    private static byte[] ReadBlobFile(string path)
    {
        byte[] content;
        using (var source = new FileStream(path, FileMode.Open, FileAccess.Read))
        {

            using var zstream = new ZLibStream(source, CompressionMode.Decompress);
            using var sink = new MemoryStream();
            zstream.CopyTo(sink);

            content = sink.ToArray();
        }

        return content;
    }

    private static void WriteObject(string hash, byte[] fileContent)
    {
        //debug constant
        if (!DOWRITE) return;
        string subpath = hash.Insert(2, "/");
        string dirPath = Path.Join(GetGitDir(), "objects", subpath.Slice(0, 2));
        Directory.CreateDirectory(dirPath);
        string path = Path.Join(dirPath, subpath.Slice(2));
        using var sink = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zlib = new ZLibStream(sink, CompressionMode.Compress);
        zlib.Write(fileContent);
    }

    private static List<FileInfo> GetStagingArea()
    {
        //NOTE: for challenge purposes staging area is not implemented, working with the current directory
        List<FileInfo> res = new();
        var currentPath = Directory.GetCurrentDirectory();
        foreach (var dir in Directory.GetDirectories(currentPath))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName != ".git") continue;

            res.Add(new FileInfo(dir));
        }
        foreach (var file in Directory.GetFiles(currentPath))
        {
            res.Add(new FileInfo(file));
        }
        return res;
    }

    private static bool IsDirectory(FileInfo fileInfo) => (fileInfo.Attributes & FileAttributes.Directory) > 0;
    private static int GetFileMode(FileInfo fileInfo) => IsDirectory(fileInfo) ? 40000 : 100644;

    private static byte[] HashTree(List<TreeEntry> entries)
    {
        byte[] entriesPart;
        using (var sink = new MemoryStream())
        {
            foreach (var entry in entries.OrderBy(e => Path.GetFileName(e.Name)))
            {
                // Console.WriteLine($"{treeEntry.Mode:D6} {kind} {Convert.ToHexStringLower(treeEntry.Hash)}    {treeEntry.Name}");
                sink.Write(Encoding.UTF8.GetBytes($"{entry.Mode} {Path.GetFileName(entry.Name)}"));
                sink.WriteByte(0);
                sink.Write(entry.Hash);
            }
            entriesPart = sink.ToArray();
        }
        int size = entriesPart.Length;
        byte[] header = Encoding.UTF8.GetBytes($"tree {size}");
        byte[] content = new byte[size + header.Length + 1];
        header.CopyTo(content, 0);
        content[header.Length] = 0;
        entriesPart.CopyTo(content, header.Length + 1);
        var treeHash = SHA1.HashData(content);

        WriteObject(Convert.ToHexStringLower(treeHash), content);

        return treeHash;
    }

}

public struct TreeEntry
{
    public byte[] Hash { get; set; }
    public string Name { get; set; }
    public int Mode { get; set; }
}

public record struct FileEntry(string FileName, FileType FileType);
public enum FileType
{
    File,
    Directory,
    Link
}
