using System;
using System.IO;

namespace CodeCrafters.Git;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Please provide a command.");
            return;
        }

        // You can use print statements as follows for debugging, they'll be visible when running tests.
        // Console.Error.WriteLine("Logs from your program will appear here!");

        string command = args[0];
        switch (args)
        {
            case ["init", ..]:
                {
                    Git.Init();
                }
                break;
            case ["cat-file", ..]:
                {
                    Git.CatFile(args.Slice(1));
                }
                break;
            case ["hash-object", ..]:
                {
                    Git.HashObject(args.Slice(1));
                }
                break;
            case ["ls-tree", ..]:
                {
                    Git.LsTree(args.Slice(1));
                }
                break;
            case ["write-tree", ..]:
                {
                    Git.WriteTree();
                }
                break;
            case ["commit-tree", ..]:
                {
                    Git.CommitTree(args.Slice(1));
                }
                break;
            default:
                {
                    throw new ArgumentException($"Unknown command {command}");
                }

        }
    }
}

public static class Extensions
{
    public static Span<T> Slice<T>(this T[] array, int start)
    => new Span<T>(array, start, array.Length - start);
    public static Span<T> Slice<T>(this T[] array, int start, int length)
        => new Span<T>(array, start, length);

    public static ReadOnlySpan<char> Slice(this string str, int start)
        => str.AsSpan(start);
    public static ReadOnlySpan<char> Slice(this string str, int start, int length)
        => str.AsSpan(start, length);
}
