using System;

namespace MaxLib.WebServer.Benchmark.Rendering
{
    public class TableHeaderCell
    {
        public string Text { get; set; } = "";

        private int colSpan;

        public TableHeaderCell()
        {

        }

        public TableHeaderCell(string text, int colSpan = 1, Alignment alignment = Alignment.Left)
        {
            Text = text;
            this.colSpan = colSpan;
            Alignment = alignment;
        }

        public Alignment Alignment { get; set; }

        public int ColSpan
        {
            get => Math.Max(1, colSpan);
            set => colSpan = Math.Max(1, value);
        }
    }
}