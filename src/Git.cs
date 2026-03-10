using System.IO.Compression;
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

    public static void CatFile(Span<string> args)
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

}