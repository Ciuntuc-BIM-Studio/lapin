using System.Text;
using Lapin.Drawing.Export;

namespace Lapin.Platform;

/// <summary>A table a user gives as a file: a workbook's first sheet, or CSV (semicolons, commas or tabs, quotes honoured), as rows of cells.</summary>
public static class Tables
{
    public static List<List<string>> Read(byte[] bytes)
    {
        if (XlsxReader.IsZip(bytes))
        {
            try { return XlsxReader.Read(bytes); }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException) { throw new CommandException("The workbook could not be read: " + ex.Message); }
        }
        return Csv(Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
    }

    /// <summary>CSV split into rows and cells: the separator the first line uses most (; , or a tab), quotes honoured.</summary>
    public static List<List<string>> Csv(string text)
    {
        var first = text.Split('\n').FirstOrDefault() ?? "";
        var sep = new[] { ';', '\t', ',' }.OrderByDescending(c => first.Count(x => x == c)).First();
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch is '\n' or '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
