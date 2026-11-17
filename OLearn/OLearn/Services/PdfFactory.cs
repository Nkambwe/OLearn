using System.Text;

namespace OLearn.Services;

/// <summary>Builds small valid PDF files for the sample resources, so the app can be tested without course material.</summary>
public static class PdfFactory
{
    public static void Write(Stream s, string title, string note, long paddingBytes = 0)
    {
        var offsets = new List<long>();
        long pos = 0;
        void W(string t) { var b = Encoding.ASCII.GetBytes(t); s.Write(b, 0, b.Length); pos += b.Length; }

        W("%PDF-1.4\n");
        offsets.Add(pos); W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        offsets.Add(pos); W("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        offsets.Add(pos); W("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");
        var content = $"BT /F1 24 Tf 60 760 Td ({Esc(title)}) Tj ET\nBT /F1 12 Tf 60 720 Td ({Esc(note)}) Tj ET\n";
        offsets.Add(pos); W($"4 0 obj\n<< /Length {content.Length} >>\nstream\n{content}endstream\nendobj\n");
        offsets.Add(pos); W("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        var objects = 5;
        if (paddingBytes > 0)
        {
            offsets.Add(pos);
            W($"6 0 obj\n<< /Length {paddingBytes} >>\nstream\n");
            var chunk = new byte[1 << 20];
            var remaining = paddingBytes;
            while (remaining > 0)
            {
                var n = (int)Math.Min(chunk.Length, remaining);
                s.Write(chunk, 0, n); remaining -= n; pos += n;
            }
            W("\nendstream\nendobj\n");
            objects = 6;
        }
        var xref = pos;
        W($"xref\n0 {objects + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }

    private static string Esc(string t)
    {
        var sb = new StringBuilder();
        foreach (var c in t) sb.Append(c is '(' or ')' or '\\' || c > 126 ? '?' : c);
        return sb.ToString();
    }
}
