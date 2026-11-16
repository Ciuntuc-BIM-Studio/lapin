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

    /// <summary>CSV split into rows and cells, as <see cref="CsvText.Read"/> reads it: the separator the first line uses most (; , or a tab), quotes honoured.</summary>
    public static List<List<string>> Csv(string text) => CsvText.Read(text);
}
