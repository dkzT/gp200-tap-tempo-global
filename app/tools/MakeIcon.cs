// Gera app\icon.ico (16..256 px, entradas PNG) e uma prévia PNG. Uso: MakeIcon.exe <saida.ico> [preview.png]
// Desenho: quadrado escuro arredondado, seta circular azul ("global") em volta de um LED vermelho de TAP.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class MakeIcon
{
    static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static Bitmap Draw(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
            float u = s / 256f;
            RectangleF bg = new RectangleF(8 * u, 8 * u, 240 * u, 240 * u);
            using (GraphicsPath p = Round(bg, 52 * u))
            using (LinearGradientBrush b = new LinearGradientBrush(bg, Color.FromArgb(40, 45, 56), Color.FromArgb(16, 18, 22), 90f))
            {
                g.FillPath(b, p);
                if (s >= 32) using (Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, 3 * u))) g.DrawPath(pen, p);
            }

            float cx = 128 * u, cy = 128 * u;
            // seta circular azul (tempo global)
            float R = 82 * u, w = (s <= 24 ? 30 : 22) * u;
            Color blue = Color.FromArgb(64, 156, 255);
            using (Pen pen = new Pen(blue, w))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Flat;
                g.DrawArc(pen, cx - R, cy - R, 2 * R, 2 * R, -60, 290);
            }
            // ponta da seta no fim do arco (ângulo 230°)
            double a = (-60 + 290) * Math.PI / 180;
            PointF tip = new PointF(cx + (float)(R * Math.Cos(a)), cy + (float)(R * Math.Sin(a)));
            double tang = a + Math.PI / 2; // direção do movimento (sentido horário)
            float L = 34 * u, H = 30 * u;
            PointF fwd = new PointF((float)Math.Cos(tang), (float)Math.Sin(tang));
            PointF nrm = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            PointF p1 = new PointF(tip.X + fwd.X * L, tip.Y + fwd.Y * L);
            PointF p2 = new PointF(tip.X + nrm.X * H - fwd.X * 4 * u, tip.Y + nrm.Y * H - fwd.Y * 4 * u);
            PointF p3 = new PointF(tip.X - nrm.X * H - fwd.X * 4 * u, tip.Y - nrm.Y * H - fwd.Y * 4 * u);
            using (SolidBrush b = new SolidBrush(blue)) g.FillPolygon(b, new PointF[] { p1, p2, p3 });

            // LED vermelho com brilho
            float r = 44 * u;
            if (s >= 32)
                using (GraphicsPath gp = new GraphicsPath())
                {
                    gp.AddEllipse(cx - r * 1.7f, cy - r * 1.7f, r * 3.4f, r * 3.4f);
                    using (PathGradientBrush pb = new PathGradientBrush(gp))
                    { pb.CenterColor = Color.FromArgb(150, 255, 60, 60); pb.SurroundColors = new Color[] { Color.FromArgb(0, 255, 60, 60) }; g.FillPath(pb, gp); }
                }
            RectangleF led = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            using (LinearGradientBrush b = new LinearGradientBrush(led, Color.FromArgb(255, 110, 100), Color.FromArgb(210, 30, 40), 90f)) g.FillEllipse(b, led);
            if (s >= 32) using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 255, 255, 255))) g.FillEllipse(b, cx - r * 0.55f, cy - r * 0.75f, r * 0.7f, r * 0.45f);
        }
        return bmp;
    }

    static void Main(string[] args)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap b = Draw(s)) using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        using (FileStream fs = new FileStream(args[0], FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int off = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32); w.Write(pngs[i].Length); w.Write(off); off += pngs[i].Length;
            }
            foreach (byte[] p in pngs) w.Write(p);
        }
        if (args.Length > 1)
        {
            // prévia: tamanhos lado a lado
            using (Bitmap sheet = new Bitmap(256 + 128 + 64 + 48 + 32 + 16 + 7 * 16, 272))
            using (Graphics g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(240, 240, 240));
                int x = 16;
                foreach (int s in new int[] { 256, 128, 64, 48, 32, 16 })
                { using (Bitmap b = Draw(s)) g.DrawImage(b, x, 8 + (256 - s) / 2); x += s + 16; }
                sheet.Save(args[1], ImageFormat.Png);
            }
        }
    }
}
