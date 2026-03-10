using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CodeCrafters.Git;

public static class Git {
    public static void Init() {
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
        int len = int.Parse(Encoding.UTF8.GetString(content.Slice(5,nullIdx-5)));
        Span<byte> blob = content.Slice(nullIdx + 1, len);
        Console.Write(Encoding.UTF8.GetString(blob));
    }

    public static void HashObject(ReadOnlySpan<string> args)
    {
        bool writeObject = args.Contains<string>("-w");
        string filePath = args[^1];

        var fileContent = File.ReadAllBytes(filePath);
        byte[] blob;
        using (var ms = new MemoryStream())
        {
            ms.Write(Encoding.UTF8.GetBytes(String.Concat("blob ", fileContent.Length, "\0")));
            ms.Write(fileContent);
            blob = ms.ToArray();
        }
        var hash = Convert.ToHexStringLower(SHA1.HashData(blob));
        Console.WriteLine(hash);

        if (writeObject)
        {
            WriteObject(hash, blob);
        }
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
        int len = int.Parse(Encoding.UTF8.GetString(content.Slice(5,nullIdx-5)));
        Span<byte> blob = content.Slice(nullIdx + 1, len);

        List<TreeEntry> entries = new();
        var rem = blob;
        while (rem.Length > 0)
        {
            var spaceIdx = rem.IndexOf((byte)' ');
            var mode = int.Parse(Encoding.UTF8.GetString(rem.Slice(0, spaceIdx)));
            nullIdx = rem.IndexOf((byte)0);
            var name = Encoding.UTF8.GetString(rem.Slice(spaceIdx + 1, nullIdx - (spaceIdx+1)));
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
        string subpath = hash.Insert(2, "/");
        string dirPath = Path.Join(GetGitDir(), "objects", subpath.Slice(0, 2));
        Directory.CreateDirectory(dirPath);
        string path = Path.Join(dirPath, subpath.Slice(2));
        using var sink = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zlib = new ZLibStream(sink, CompressionMode.Compress);
        zlib.Write(fileContent);
    }
}

public struct TreeEntry
{
    public byte[] Hash { get; set; }
    public string Name{ get; set; }
    public int Mode{ get; set; }
}