using System.IO.Compression;
using System.Text;

namespace Lapin.Package;

/// <summary>
/// Lapin for the browser (docs/html-export.md, decision 482): one .html file that is a page any browser opens by
/// double-click and, after it, the project's .lapin package unchanged:
/// <code>
/// &lt;!DOCTYPE html&gt; … &lt;meta name="lapin-package" content="lapin-project 3"&gt; … the viewer and its data … &lt;/html&gt;
/// &lt;plaintext hidden&gt;          the browser reads everything after this tag as text it does not show
/// [the package: a zip]
/// </code>
/// A zip is read from its end (its central directory), and the package is written with its offsets counted from the
/// file's start, so the whole file is a zip as it stands: Lapin, unzip and Python read it as one. A page without the
/// tail is a viewer only.
/// </summary>
public static class HtmlPackage
{
    public const string Suffix = ".html";
    public const string MetaName = "lapin-package";
    /// <summary>The tag that marks the page as Lapin's, first in its head.</summary>
    public static readonly string Meta = $"<meta name=\"{MetaName}\" content=\"{LapinPackage.Format} {LapinPackage.Version}\">";
    /// <summary>The tag after which the browser stops reading markup: the package follows it.</summary>
    public const string Tail = "<plaintext hidden>";
    /// <summary>How far into the file the marker is looked for: it is in the head, before anything large.</summary>
    const int HeadBytes = 8192;

    /// <summary>The page (UTF-8), then, given <paramref name="package"/>, the tail tag and the package it writes into the same stream.</summary>
    public static void Write(Stream output, string page, Action<Stream>? package)
    {
        output.Write(Encoding.UTF8.GetBytes(page));
        if (package == null) return;
        output.Write(Encoding.UTF8.GetBytes("\n" + Tail + "\n"));
        // ZipArchive counts an entry's offset from the stream's start: written after the page, they are the file's.
        package(output);
    }

    /// <summary>Whether the bytes are a web page (a byte-order mark, spaces, then a doctype or an html tag).</summary>
    public static bool IsPage(ReadOnlySpan<byte> bytes)
    {
        var i = bytes.StartsWith("﻿"u8) ? 3 : 0;
        while (i < bytes.Length && bytes[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++;
        var rest = bytes[i..];
        return StartsWithIgnoringCase(rest, "<!doctype html"u8) || StartsWithIgnoringCase(rest, "<html"u8);
    }

    /// <summary>Whether the file at <paramref name="path"/> is a web page.</summary>
    public static bool IsPage(string path)
    {
        using var f = File.OpenRead(path);
        var head = new byte[64];
        var n = f.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        return IsPage(head.AsSpan(0, n));
    }

    /// <summary>Whether the bytes are a page Lapin wrote: a page with Lapin's marker in its head.</summary>
    public static bool IsLapinPage(ReadOnlySpan<byte> bytes) =>
        IsPage(bytes) && bytes[..Math.Min(bytes.Length, HeadBytes)].IndexOf(Encoding.UTF8.GetBytes($"name=\"{MetaName}\"")) >= 0;

    /// <summary>Whether a page of Lapin's carries its project: a zip read from its end with a Lapin manifest.</summary>
    public static bool HasPackage(byte[] bytes)
    {
        if (!IsLapinPage(bytes)) return false;
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            return zip.GetEntry("manifest.json") != null;
        }
        catch (InvalidDataException) { return false; }
    }

    /// <summary>The project a page carries, or why it does not open.</summary>
    public static void Check(byte[] bytes)
    {
        if (!IsLapinPage(bytes))
            throw new InvalidDataException("This web page was not written by Lapin: only a page Lapin exported (File → Lapin for the browser) opens as a project.");
        if (!HasPackage(bytes))
            throw new InvalidDataException("This Lapin page was exported without its project: it shows the model in a browser but cannot be opened. Export it again with the project included.");
    }

    static bool StartsWithIgnoringCase(ReadOnlySpan<byte> s, ReadOnlySpan<byte> prefix)
    {
        if (s.Length < prefix.Length) return false;
        for (var i = 0; i < prefix.Length; i++)
            if (char.ToLowerInvariant((char)s[i]) != (char)prefix[i]) return false;
        return true;
    }
}
