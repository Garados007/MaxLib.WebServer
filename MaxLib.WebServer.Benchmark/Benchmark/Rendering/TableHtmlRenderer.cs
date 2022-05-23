using System;
using System.IO;

namespace MaxLib.WebServer.Benchmark.Rendering
{
    public class TableHtmlRenderer
    {

        public void Render(TextWriter writer, Table table)
        {
            writer.Write("<table>");
            if (table.Header.Rows > 0)
            {
                writer.Write("<thead>");
                for (int i = 0; i < table.Header.Rows; ++i)
                    Render(writer, i, table.Header, true);
                writer.Write("</thead>");
            }
            writer.Write("<tbody>");
            for (int i = 0; i < table.Body.Rows; ++i)
                Render(writer, i, table);
            writer.Write("</tbody>");
            if (table.Footer.Rows > 0)
            {
                writer.Write("<tfoot>");
                for (int i = 0; i < table.Footer.Rows; ++i)
                    Render(writer, i, table.Footer, false);
                writer.Write("</tfoot>");
            }
            writer.Write("</table>");
        }

        private void Render(TextWriter writer, int y, Table.TableHeader table, bool header)
        {
            int x = 0;
            var tag = header ? "th" : "td";
            writer.Write("<tr>");
            while (x < table.Columns)
            {
                var cell = table[x, y];
                writer.Write("<{0} colspan=\"{1}\" style=\"text-align:{2}\">",
                    tag, cell.ColSpan, GetAlignment(cell.Alignment)
                );
                System.Web.HttpUtility.HtmlEncode(cell.Text, writer);
                writer.Write("</{0}>", tag);
                x += cell.ColSpan;
            }
            writer.Write("</tr>");
        }

        private void Render(TextWriter writer, int y, Table table)
        {
            writer.Write("<tr>");
            for (int x = 0; x < table.Body.Columns; ++x)
            {
                writer.Write("<td style=\"text-align:{0}\">", GetAlignment(table.Alignment[x, y]));
                System.Web.HttpUtility.HtmlEncode(table.Body[x, y], writer);
                writer.Write("</td>");
            }
            writer.Write("</tr>");
        }

        private string GetAlignment(Alignment alignment)
        {
            return alignment switch
            {
                Alignment.Left => "left",
                Alignment.Center => "center",
                Alignment.Right => "right",
                _ => "unset"
            };
        }
    }
}