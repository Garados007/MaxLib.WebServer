using System;
using System.Collections.Generic;

namespace MaxLib.WebServer.Benchmark.Rendering
{
    public class Table
    {
        public class TableHeader
        {
            private readonly Memory<Memory<TableHeaderCell>> cells;

            public int Columns { get; }

            public int Rows { get; }

            internal TableHeader(int columns, int rows)
            {
                Columns = columns;
                Rows = rows;
                cells = new Memory<TableHeaderCell>[rows];
                for (int y = 0; y < rows; ++y)
                {
                    cells.Span[y] = new TableHeaderCell[columns];
                    for (int x = 0; x < columns; ++x)
                        cells.Span[y].Span[x] = new TableHeaderCell();
                }
            }

            public TableHeaderCell this[int column, int row]
            {
                get
                {
                    if (column < 0 || column >= Columns)
                        throw new ArgumentOutOfRangeException(nameof(column));
                    if (row < 0 || row >= Rows)
                        throw new ArgumentOutOfRangeException(nameof(row));
                    var col = RealColumn(column, row);
                    return cells.Span[row].Span[col];
                }
                set
                {
                    if (column < 0 || column >= Columns)
                        throw new ArgumentOutOfRangeException(nameof(column));
                    if (row < 0 || row >= Rows)
                        throw new ArgumentOutOfRangeException(nameof(row));
                    var col = RealColumn(column, row);
                    cells.Span[row].Span[col] = value;
                }
            }

            public TableHeaderCell this[int column]
            {
                get => this[column, 0];
                set => this[column, 0] = value;
            }

            public void SetRowAlignment(int row, Alignment alignment)
            {
                if (row < 0 || row >= Rows)
                    throw new ArgumentOutOfRangeException(nameof(row));
                for (int x = 0; x < Columns; ++x)
                    cells.Span[row].Span[x].Alignment = alignment;
            }

            private int RealColumn(int column, int row)
            {
                int x = 0;
                int col = 0;
                while (x <= column)
                    x += cells.Span[row].Span[col = x].ColSpan;
                return col;
            }
        }

        public class TableBody<T>
        {
            private readonly Memory<Memory<T>> cells;
            private readonly T init;

            public int Columns { get; }

            public int Rows { get; }

            internal TableBody(int columns, int rows, T init)
            {
                this.init = init;
                Columns = columns;
                Rows = rows;
                cells = new Memory<T>[rows];
                for (int y = 0; y < rows; ++y)
                {
                    cells.Span[y] = new T[columns];
                    for (int x = 0; x < columns; ++x)
                        cells.Span[y].Span[x] = init;
                }
            }

            public T this[int column, int row]
            {
                get
                {
                    if (column < 0 || column >= Columns)
                        throw new ArgumentOutOfRangeException(nameof(column));
                    if (row < 0 || row >= Rows)
                        throw new ArgumentOutOfRangeException(nameof(row));
                    return cells.Span[row].Span[column];
                }
                set
                {
                    if (column < 0 || column >= Columns)
                        throw new ArgumentOutOfRangeException(nameof(column));
                    if (row < 0 || row >= Rows)
                        throw new ArgumentOutOfRangeException(nameof(row));
                    cells.Span[row].Span[column] = value ?? init;
                }
            }

            public void SetColumn(int column, T value)
            {
                if (column < 0 || column >= Columns)
                    throw new ArgumentOutOfRangeException(nameof(column));
                for (int y = 0; y < Rows; ++y)
                    cells.Span[y].Span[column] = value;
            }

            public void SetRow(int row, T value)
            {
                if (row < 0 || row >= Rows)
                    throw new ArgumentOutOfRangeException(nameof(row));
                cells.Span[row].Span.Fill(value);
            }

            public void Set(T value)
            {
                for (int y = 0; y < Rows; ++y)
                    cells.Span[y].Span.Fill(value);
            }

        }
    
        public TableHeader Header { get; }

        public TableBody<string> Body { get; }

        public TableBody<Alignment> Alignment { get; }

        public TableHeader Footer { get; }

        public Table(int columns, int rows, int headerRows = 1, int footerRows = 0)
        {
            if (columns < 0)
                throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows < 0)
                throw new ArgumentOutOfRangeException(nameof(rows));
            if (headerRows < 0)
                throw new ArgumentOutOfRangeException(nameof(headerRows));
            if (footerRows < 0)
                throw new ArgumentOutOfRangeException(nameof(footerRows));
            Header = new TableHeader(columns, headerRows);
            Body = new TableBody<string>(columns, rows, "");
            Alignment = new TableBody<Alignment>(columns, rows, Rendering.Alignment.Left);
            Footer = new TableHeader(columns, footerRows);
        }
    }
}