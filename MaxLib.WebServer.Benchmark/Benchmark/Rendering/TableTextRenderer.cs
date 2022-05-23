using System;
using System.IO;

namespace MaxLib.WebServer.Benchmark.Rendering
{
    public class TableTextRenderer
    {
        public void Render(TextWriter writer, Table table)
        {
            // measure sizes
            Span<int> colSizes = stackalloc int[table.Body.Columns];
            // measure body size
            for (int y = 0; y < table.Body.Rows; ++y)
                for (int x = 0; x < table.Body.Columns; ++x)
                {
                    var size = table.Body[x, y].Length;
                    if (colSizes[x] < size)
                        colSizes[x] = size;
                }
            // measure column size
            for (int y = 0; y < table.Header.Rows; ++y)
            {
                int x = 0; 
                while (x < table.Header.Columns)
                {
                    var span = table.Header[x, y].ColSpan;
                    var length = table.Header[x, y].Text.Length;
                    var refLength = (span - 1) * 3;
                    for (int i = 0; i < span; ++i)
                        refLength += colSizes[x + i];
                    if (refLength < length)
                    {
                        var extra = (length - refLength) / span;
                        var mod = (length - refLength) - extra * span;
                        for (int i = 0; i < span; ++i)
                        {
                            colSizes[x + i] += extra + (i < mod ? 1 : 0);
                        }
                    }
                    x += span;
                }
            }
            for (int y = 0; y < table.Footer.Rows; ++y)
            {
                int x = 0; 
                while (x < table.Footer.Columns)
                {
                    var span = table.Footer[x, y].ColSpan;
                    var length = table.Footer[x, y].Text.Length;
                    var refLength = (span - 1) * 3;
                    for (int i = 0; i < span; ++i)
                        refLength += colSizes[x + i];
                    if (refLength < length)
                    {
                        var extra = (length - refLength) / span;
                        var mod = (length - refLength) - extra * span;
                        for (int i = 0; i < span; ++i)
                        {
                            colSizes[x + i] += extra + (i < mod ? 1 : 0);
                        }
                    }
                    x += span;
                }
            }
            // rendering grid line
            var width = colSizes.Length * 3 + 1;
            for (int x = 0; x < colSizes.Length; ++x)
                width += colSizes[x];
            Span<char> line = stackalloc char[width];
            Span<char> delimiter = stackalloc char[width];
            line[0] = '+';
            delimiter[0] = '+';
            int left = 1;
            for (int x = 0; x < colSizes.Length; ++x)
            {
                line[left .. (left + colSizes[x] + 2)].Fill('-');
                line[left + colSizes[x] + 2] = '+';
                delimiter[left .. (left + colSizes[x] + 2)].Fill('=');
                delimiter[left + colSizes[x] + 2] = '+';
                left += colSizes[x] + 3;
            }
            // output header lines
            for (int y = 0; y < table.Header.Rows; ++y)
            {
                writer.WriteLine(line);
                writer.Write('|');
                int x = 0; 
                while (x < table.Header.Columns)
                {
                    var cell = table.Header[x, y];
                    var span = cell.ColSpan;
                    var refLength = (span - 1) * 3;
                    for (int i = 0; i < span; ++i)
                        refLength += colSizes[x + i];
                    WriteAligned(writer, cell.Text, refLength, cell.Alignment);
                    writer.Write('|');
                    x += span;
                }
                writer.WriteLine();
            }
            writer.WriteLine(table.Header.Rows == 0 ? line : delimiter);
            // output body lines
            for (int y = 0; y < table.Body.Rows; ++y)
            {
                if (y > 0)
                    writer.WriteLine(line);
                writer.Write('|');
                for (int x = 0; x < table.Body.Columns; ++x)
                {
                    WriteAligned(writer, table.Body[x, y], colSizes[x], table.Alignment[x, y]);
                    writer.Write('|');
                }
                writer.WriteLine();
            }
            // output footer lines
            writer.WriteLine(table.Footer.Rows == 0 ? line : delimiter);
            for (int y = 0; y < table.Footer.Rows; ++y)
            {
                writer.Write('|');
                int x = 0; 
                while (x < table.Footer.Columns)
                {
                    var cell = table.Footer[x, y];
                    var span = cell.ColSpan;
                    var refLength = (span - 1) * 3;
                    for (int i = 0; i < span; ++i)
                        refLength += colSizes[x + i];
                    WriteAligned(writer, cell.Text, refLength, cell.Alignment);
                    writer.Write('|');
                    x += span;
                }
                writer.WriteLine();
                writer.WriteLine(line);
            }
        }

        private void WriteAligned(TextWriter writer, string value, int length, Alignment alignment)
        {
            int offset = length - value.Length;
            switch (alignment)
            {
                case Alignment.Left: writer.Write(' '); break;
                case Alignment.Center:
                    for (int i = 0; i <= offset / 2; ++i)
                        writer.Write(' ');
                    break;
                case Alignment.Right:
                    for (int i = 0; i <= offset; ++i)
                        writer.Write(' ');
                    break;
            }
            writer.Write(value);
            switch (alignment)
            {
                case Alignment.Left:
                    for (int i = 0; i <= offset; ++i)
                        writer.Write(' ');
                    break;
                case Alignment.Center:
                    for (int i = offset / 2; i <= offset; ++i)
                        writer.Write(' ');
                    break;
                case Alignment.Right: writer.Write(' '); break;
            }
        }
    }
}