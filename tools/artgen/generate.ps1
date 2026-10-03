# STS2 MySts2Mod 全套图标风美术生成器（PowerShell + 内嵌 C#/GDI+，零外部依赖）
# 用法: powershell -File generate.ps1 [-OutRoot <images目录>] [-Only <名称过滤>]
# 配色基调：橙+蓝对比（与用户提供的 HaoStrike/HaoDefend 卡图一致）
param(
    [string]$OutRoot = "C:\Users\39509\Desktop\sts2\MySts2Mod\MySts2Mod\images",
    [string]$Only = ""
)

Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Sts2ArtGen
{
    public static class Gen
    {
        // ---------- 主题 ----------
        class Theme
        {
            public Color BgTop, BgBot, Glow, Ink;
        }
        static Theme Attack = Make(0xa03d12, 0x2e1005, 255, 176, 64, 0xf7eeda);
        static Theme Skill  = Make(0x1c3f66, 0x0a1424, 110, 186, 255, 0xeef4fb);
        static Theme Power  = Make(0x3a2c5e, 0x150e26, 255, 150, 90, 0xf4eefe);

        static Theme Make(int top, int bot, int gr, int gg, int gb, int ink)
        {
            Theme t = new Theme();
            t.BgTop = RGB(top);
            t.BgBot = RGB(bot);
            t.Glow = Color.FromArgb(90, gr, gg, gb);
            t.Ink = RGB(ink);
            return t;
        }

        static Color RGB(int v)
        {
            return Color.FromArgb(255, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
        }

        static Dictionary<string, string> CardType;   // name -> attack/skill/power
        static Dictionary<string, string> CardGlyph;  // name -> glyph key

        static Gen()
        {
            CardType = new Dictionary<string, string>();
            CardGlyph = new Dictionary<string, string>();
            string[] atk = { "hao_strike", "code_offense", "calm", "conflict", "kick_up", "infinite_hao", "better_than_walker", "spark" };
            string[] skl = { "hao_defend", "narcissism", "figure_shadow", "debugging", "exam", "shang_hai_student", "surfing", "sneer", "group_discuss", "fire_in_soul", "unrepentant", "grammar", "magnificent_hao", "student_leader", "code_code_code", "final_commit" };
            string[] pow = { "equal680", "genius", "math_prince", "super_hao_field", "tsinghua_form" };
            foreach (string s in atk) { CardType[s] = "attack"; }
            foreach (string s in skl) { CardType[s] = "skill"; }
            foreach (string s in pow) { CardType[s] = "power"; }

            CardGlyph["hao_strike"] = "sword";
            CardGlyph["hao_defend"] = "shield";
            CardGlyph["narcissism"] = "mirror";
            CardGlyph["figure_shadow"] = "shadow";
            CardGlyph["debugging"] = "bug";
            CardGlyph["code_offense"] = "code";
            CardGlyph["exam"] = "exam";
            CardGlyph["shang_hai_student"] = "sunwave";
            CardGlyph["surfing"] = "surf";
            CardGlyph["sneer"] = "mask";
            CardGlyph["calm"] = "enso";
            CardGlyph["conflict"] = "crossswords";
            CardGlyph["equal680"] = "scales";
            CardGlyph["group_discuss"] = "bubbles";
            CardGlyph["fire_in_soul"] = "innerflame";
            CardGlyph["unrepentant"] = "arrowshield";
            CardGlyph["kick_up"] = "kick";
            CardGlyph["genius"] = "bulb";
            CardGlyph["grammar"] = "book";
            CardGlyph["magnificent_hao"] = "haochar";
            CardGlyph["math_prince"] = "compass";
            CardGlyph["student_leader"] = "rosette";
            CardGlyph["code_code_code"] = "infinity";
            CardGlyph["super_hao_field"] = "dome";
            CardGlyph["tsinghua_form"] = "cap";
            CardGlyph["infinite_hao"] = "spiral";
            CardGlyph["better_than_walker"] = "boot";
            CardGlyph["final_commit"] = "upload";
            CardGlyph["spark"] = "spark4";
        }

        static Random Rng(string seed) { int h = 0; foreach (char c in seed) h = h * 31 + c; return new Random(h); }

        // ---------- 基础 ----------
        static Bitmap NewCanvas(int w, int h) { Bitmap b = new Bitmap(w, h); b.SetResolution(96f, 96f); return b; }

        static Graphics G(Bitmap b)
        {
            Graphics g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            return g;
        }

        static Pen InkPen(Theme t, float w)
        {
            Pen p = new Pen(t.Ink, w);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        static Pen GlowPen(Theme t, float w)
        {
            Pen p = new Pen(t.Glow, w * 2.4f);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        static Font HaoFont(float size) { return new Font("Microsoft YaHei UI", size, FontStyle.Bold, GraphicsUnit.Pixel); }

        // 100x100 设计空间 -> 适配目标矩形并居中
        static void Design(Graphics g, RectangleF box, int design, Action<Graphics> draw)
        {
            GraphicsState s = g.Save();
            float sc = Math.Min(box.Width, box.Height) / design;
            g.TranslateTransform(box.X + box.Width / 2f, box.Y + box.Height / 2f);
            g.ScaleTransform(sc, sc);
            g.TranslateTransform(-design / 2f, -design / 2f);
            draw(g);
            g.Restore(s);
        }

        static void Line(Graphics g, Pen p, float x1, float y1, float x2, float y2) { g.DrawLine(p, x1, y1, x2, y2); }

        static void Poly(Graphics g, Pen p, Brush b, PointF[] pts, bool fill, bool stroke)
        {
            if (fill && b != null) g.FillPolygon(b, pts);
            if (stroke && p != null) g.DrawPolygon(p, pts);
        }

        static void Circle(Graphics g, Pen p, Brush b, float cx, float cy, float rx, float ry, bool fill, bool stroke)
        {
            RectangleF r = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);
            if (fill && b != null) g.FillEllipse(b, r);
            if (stroke && p != null) g.DrawEllipse(p, r);
        }

        static void Arc(Graphics g, Pen p, float cx, float cy, float rad, float start, float sweep)
        {
            g.DrawArc(p, cx - rad, cy - rad, rad * 2f, rad * 2f, start, sweep);
        }

        // 双层描边（辉光 + 墨线）
        static void Stroke2(Graphics g, Action<Pen> draw, Pen glow, Pen ink) { draw(glow); draw(ink); }

        // ---------- 背景 ----------
        static void DrawBackground(Graphics g, int w, int h, Theme t, string seed)
        {
            RectangleF full = new RectangleF(0, 0, w, h);
            LinearGradientBrush bg = new LinearGradientBrush(full, t.BgTop, t.BgBot, 90f);
            g.FillRectangle(bg, full);

            Random rnd = Rng(seed);

            // 斜向光带
            for (int i = 0; i < 3; i++)
            {
                float x = (float)(w * (0.18 + 0.3 * i + 0.08 * rnd.NextDouble()));
                using (LinearGradientBrush streak = new LinearGradientBrush(full, Color.FromArgb(11, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 65f))
                {
                    GraphicsState s = g.Save();
                    g.TranslateTransform(x, 0);
                    g.FillRectangle(streak, -w * 0.05f, -h, w * 0.10f, h * 3f);
                    g.Restore(s);
                }
            }

            // 粒子（attack=火星向上，skill=星点，power=双重色）
            bool isAtk = t == Attack; bool isPow = t == Power;
            for (int i = 0; i < 42; i++)
            {
                float x = (float)(w * rnd.NextDouble());
                float y = (float)(h * rnd.NextDouble());
                float r = (float)(w * (0.004 + 0.010 * rnd.NextDouble()));
                Color c;
                if (isAtk) c = Color.FromArgb((int)(90 + 90 * rnd.NextDouble()), 255, (int)(120 + 90 * rnd.NextDouble()), 40);
                else if (isPow) { if (rnd.NextDouble() < 0.5) c = Color.FromArgb(90, 255, 160, 90); else c = Color.FromArgb(90, 170, 150, 255); }
                else c = Color.FromArgb((int)(60 + 80 * rnd.NextDouble()), 190, 220, 255);
                using (SolidBrush br = new SolidBrush(c)) g.FillEllipse(br, x - r, y - r, r * 2f, r * 2f);
            }

            // 暗角
            using (GraphicsPath vp = new GraphicsPath())
            {
                vp.AddEllipse(-w * 0.35f, -h * 0.45f, w * 1.7f, h * 1.9f);
                using (PathGradientBrush vg = new PathGradientBrush(vp))
                {
                    vg.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    vg.SurroundColors = new Color[] { Color.FromArgb(95, 0, 0, 0) };
                    g.FillRectangle(vg, full);
                }
            }
        }

        // ---------- 字形库（100x100 设计空间） ----------
        static Action<Graphics, Pen, Brush, Pen> Glyph(string key)
        {
            switch (key)
            {
                case "sword":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50, 50); g.RotateTransform(-45); g.TranslateTransform(-50, -50);
                            Line(g, pp, 46, 12, 46, 66); Line(g, pp, 54, 12, 54, 66);
                            g.DrawArc(pp, 46, 4, 8, 16, 180, 180);
                            Line(g, pp, 34, 66, 66, 66);
                            Line(g, pp, 50, 70, 50, 84);
                            g.Restore(s);
                        }, glow, p);
                        GraphicsState s2 = g.Save();
                        g.TranslateTransform(50, 50); g.RotateTransform(-45); g.TranslateTransform(-50, -50);
                        Circle(g, null, b, 50, 88, 4.5f, 4.5f, true, false);
                        g.Restore(s2);
                    };
                case "shield":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsPath path = new GraphicsPath();
                            path.StartFigure();
                            path.AddLine(28f, 20f, 72f, 20f);
                            path.AddBezier(72f, 20f, 74f, 46f, 68f, 66f, 50f, 82f);
                            path.AddBezier(50f, 82f, 32f, 66f, 26f, 46f, 28f, 20f);
                            path.CloseFigure();
                            g.DrawPath(pp, path);
                            path.Dispose();
                        }, glow, p);
                        Stroke2(g, delegate(Pen pp) { Line(g, pp, 50, 34, 50, 58); }, glow, p);
                    };
                case "mirror":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Circle(g, pp, null, 50, 40, 20, 26, false, true);
                            Line(g, pp, 50, 68, 50, 84);
                            Line(g, pp, 40, 88, 60, 88);
                        }, glow, p);
                    };
                case "shadow":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        // 两个交叠人影，前实后虚
                        using (SolidBrush ghost = new SolidBrush(Color.FromArgb(90, p.Color)))
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(20, 8);
                            Circle(g, null, ghost, 40, 28, 8, 8, true, false);
                            Poly(g, null, ghost, new PointF[] { new PointF(28, 76), new PointF(30, 46), new PointF(40, 40), new PointF(50, 46), new PointF(52, 76) }, true, false);
                            g.Restore(s);
                        }
                        GraphicsState s2 = g.Save();
                        g.TranslateTransform(-10, 4);
                        Circle(g, null, b, 40, 28, 9, 9, true, false);
                        Poly(g, null, b, new PointF[] { new PointF(26, 80), new PointF(28, 46), new PointF(40, 40), new PointF(52, 46), new PointF(54, 80) }, true, false);
                        g.Restore(s2);
                    };
                case "bug":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Circle(g, pp, null, 50, 55, 15, 19, false, true);
                            Circle(g, pp, null, 50, 30, 8, 7, false, true);
                            Line(g, pp, 50, 36, 50, 74);
                            Line(g, pp, 35, 45, 20, 36); Line(g, pp, 34, 57, 18, 57); Line(g, pp, 36, 68, 22, 78);
                            Line(g, pp, 65, 45, 80, 36); Line(g, pp, 66, 57, 82, 57); Line(g, pp, 64, 68, 78, 78);
                            Line(g, pp, 45, 24, 40, 14); Line(g, pp, 55, 24, 60, 14);
                        }, glow, p);
                    };
                case "code":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 32, 32, 16, 50); Line(g, pp, 16, 50, 32, 68);
                            Line(g, pp, 68, 32, 84, 50); Line(g, pp, 84, 50, 68, 68);
                            Line(g, pp, 55, 26, 45, 74);
                        }, glow, p);
                    };
                case "exam":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50, 50); g.RotateTransform(-8); g.TranslateTransform(-50, -50);
                            g.DrawRectangle(pp, 30, 20, 40, 58);
                            Line(g, pp, 38, 34, 62, 34); Line(g, pp, 38, 46, 62, 46); Line(g, pp, 38, 58, 54, 58);
                            g.Restore(s);
                        }, glow, p);
                        using (Font f = HaoFont(30f))
                        using (SolidBrush br = new SolidBrush(p.Color))
                        {
                            SizeF sz = g.MeasureString("A", f);
                            g.DrawString("A", f, br, 56 - sz.Width / 2f, 26 - sz.Height / 2f);
                        }
                    };
                case "sunwave":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Arc(g, pp, 50, 52, 16, 180, 180);
                            Line(g, pp, 22, 52, 78, 52);
                            g.DrawBezier(pp, 24, 64, 34, 58, 44, 70, 54, 64);
                            g.DrawBezier(pp, 54, 64, 64, 58, 74, 70, 84, 64);
                            g.DrawBezier(pp, 28, 78, 38, 72, 48, 84, 58, 78);
                            g.DrawBezier(pp, 58, 78, 68, 72, 78, 84, 88, 78);
                        }, glow, p);
                    };
                case "surf":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50, 50); g.RotateTransform(-38); g.TranslateTransform(-50, -50);
                            g.DrawArc(pp, 38, 8, 24, 92, 12, 156);
                            g.Restore(s);
                            g.DrawBezier(pp, 18, 66, 32, 56, 46, 74, 62, 66);
                            g.DrawBezier(pp, 62, 66, 74, 60, 84, 70, 92, 64);
                        }, glow, p);
                    };
                case "mask":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50, 50); g.RotateTransform(-10); g.TranslateTransform(-50, -50);
                            g.DrawBezier(pp, 30, 20, 70, 20, 74, 44, 66, 66);
                            g.DrawBezier(pp, 58, 80, 44, 84, 34, 74, 34, 66);
                            g.DrawBezier(pp, 34, 66, 26, 44, 30, 20, 30, 20);
                            Line(g, pp, 40, 40, 48, 44); Line(g, pp, 62, 40, 54, 44);
                            g.DrawBezier(pp, 42, 58, 50, 64, 58, 60, 62, 52);
                            g.Restore(s);
                        }, glow, p);
                    };
                case "enso":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp) { Arc(g, pp, 50, 50, 30, -70, 300); }, glow, p);
                    };
                case "crossswords":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Action<float> one = delegate(float ang)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50, 50); g.RotateTransform(ang); g.TranslateTransform(-50, -50);
                            Stroke2(g, delegate(Pen pp)
                            {
                                Line(g, pp, 46, 14, 46, 60); Line(g, pp, 54, 14, 54, 60);
                                g.DrawArc(pp, 46, 6, 8, 16, 180, 180);
                                Line(g, pp, 34, 60, 66, 60);
                                Line(g, pp, 50, 64, 50, 76);
                            }, glow, p);
                            g.Restore(s);
                        };
                        one(40); one(-40);
                    };
                case "scales":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 50, 22, 50, 74);
                            Line(g, pp, 38, 78, 62, 78);
                            Line(g, pp, 24, 30, 76, 30);
                            Line(g, pp, 24, 30, 18, 46); Line(g, pp, 24, 30, 30, 46);
                            Arc(g, pp, 24, 46, 10, 0, 180);
                            Line(g, pp, 76, 30, 70, 46); Line(g, pp, 76, 30, 82, 46);
                            Arc(g, pp, 76, 46, 10, 0, 180);
                        }, glow, p);
                    };
                case "bubbles":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsPath b1 = new GraphicsPath();
                            b1.AddArc(20, 20, 18, 18, 90, 180);
                            b1.AddArc(52, 20, 18, 18, 270, 180);
                            b1.AddLine(70f, 38f, 20f, 38f);
                            b1.CloseFigure();
                            g.DrawPath(pp, b1); b1.Dispose();
                            PointF[] t1 = { new PointF(36, 38), new PointF(30, 52), new PointF(46, 38) };
                            g.DrawPolygon(pp, t1);
                            GraphicsPath b2 = new GraphicsPath();
                            b2.AddArc(44, 54, 16, 16, 90, 180);
                            b2.AddArc(72, 54, 16, 16, 270, 180);
                            b2.AddLine(88f, 70f, 44f, 70f);
                            b2.CloseFigure();
                            g.DrawPath(pp, b2); b2.Dispose();
                            PointF[] t2 = { new PointF(58, 70), new PointF(52, 84), new PointF(68, 70) };
                            g.DrawPolygon(pp, t2);
                        }, glow, p);
                        using (SolidBrush br = new SolidBrush(p.Color))
                        {
                            g.FillEllipse(br, 30, 26, 4, 4); g.FillEllipse(br, 38, 26, 4, 4); g.FillEllipse(br, 46, 26, 4, 4);
                            g.FillEllipse(br, 62, 59, 4, 4); g.FillEllipse(br, 70, 59, 4, 4);
                        }
                    };
                case "innerflame":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            // 躯干轮廓
                            g.DrawBezier(pp, 32, 24, 26, 52, 30, 72, 50, 84);
                            g.DrawBezier(pp, 70, 24, 74, 52, 70, 72, 50, 84);
                            g.DrawBezier(pp, 32, 24, 42, 30, 58, 30, 70, 24);
                        }, glow, p);
                        // 内焰（实心）
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(220, 255, 150, 60)))
                        {
                            GraphicsPath fp = new GraphicsPath();
                            fp.AddBezier(50, 34, 38, 50, 42, 62, 50, 70);
                            fp.AddBezier(50, 70, 58, 62, 62, 50, 50, 34);
                            g.FillPath(br, fp); fp.Dispose();
                        }
                        using (SolidBrush br2 = new SolidBrush(Color.FromArgb(230, 255, 230, 160)))
                        {
                            g.FillEllipse(br2, 46, 52, 8, 10);
                        }
                    };
                case "arrowshield":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            GraphicsPath path = new GraphicsPath();
                            path.StartFigure();
                            path.AddLine(56f, 22f, 84f, 22f);
                            path.AddBezier(84f, 22f, 84f, 50f, 76f, 68f, 66f, 80f);
                            path.AddBezier(66f, 80f, 58f, 66f, 56f, 48f, 56f, 22f);
                            path.CloseFigure();
                            g.DrawPath(pp, path); path.Dispose();
                        }, glow, p);
                        // 穿过盾的箭
                        GraphicsState s = g.Save();
                        g.TranslateTransform(50, 50); g.RotateTransform(-40); g.TranslateTransform(-50, -50);
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 50, 16, 50, 84);
                            Line(g, pp, 40, 28, 50, 14); Line(g, pp, 50, 14, 60, 28);
                            Line(g, pp, 42, 38, 58, 38);
                        }, glow, p);
                        g.Restore(s);
                    };
                case "kick":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 26, 24, 46, 50); Line(g, pp, 46, 50, 62, 62);
                            Line(g, pp, 20, 32, 30, 22);
                        }, glow, p);
                        Circle(g, p, b, 74, 68, 11, 11, false, true);
                        using (SolidBrush br = new SolidBrush(p.Color))
                        {
                            g.FillEllipse(br, 71, 65, 3, 3); g.FillEllipse(br, 77, 70, 3, 3);
                        }
                    };
                case "bulb":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Circle(g, pp, null, 50, 42, 17, 17, false, true);
                            Line(g, pp, 44, 59, 44, 68); Line(g, pp, 56, 59, 56, 68);
                            Line(g, pp, 44, 68, 56, 68);
                            Line(g, pp, 46, 73, 54, 73);
                            Line(g, pp, 50, 8, 50, 16); Line(g, pp, 22, 20, 28, 26); Line(g, pp, 78, 20, 72, 26);
                            Line(g, pp, 14, 44, 24, 44); Line(g, pp, 86, 44, 76, 44);
                        }, glow, p);
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 255, 210, 120)))
                        {
                            g.FillEllipse(br, 44, 38, 12, 12);
                        }
                    };
                case "book":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Poly(g, pp, null, new PointF[] { new PointF(50, 32), new PointF(22, 26), new PointF(22, 68), new PointF(50, 74) }, false, true);
                            Poly(g, pp, null, new PointF[] { new PointF(50, 32), new PointF(78, 26), new PointF(78, 68), new PointF(50, 74) }, false, true);
                            Line(g, pp, 30, 38, 42, 40); Line(g, pp, 30, 48, 42, 50); Line(g, pp, 58, 40, 70, 38); Line(g, pp, 58, 50, 70, 48);
                        }, glow, p);
                    };
                case "haochar":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        using (Font f = HaoFont(64f))
                        using (SolidBrush gl = new SolidBrush(glow.Color))
                        using (SolidBrush br = new SolidBrush(p.Color))
                        {
                            SizeF sz = g.MeasureString("豪", f);
                            g.DrawString("豪", f, gl, 50 - sz.Width / 2f, 50 - sz.Height / 2f + 2);
                            g.DrawString("豪", f, br, 50 - sz.Width / 2f, 50 - sz.Height / 2f);
                        }
                        // 四角星光
                        using (SolidBrush br2 = new SolidBrush(Color.FromArgb(180, 255, 210, 120)))
                        {
                            PointF[][] stars = {
                                new PointF[] { new PointF(20,16), new PointF(23,23), new PointF(30,26), new PointF(23,29), new PointF(20,36), new PointF(17,29), new PointF(10,26), new PointF(17,23) },
                                new PointF[] { new PointF(82,60), new PointF(85,67), new PointF(92,70), new PointF(85,73), new PointF(82,80), new PointF(79,73), new PointF(72,70), new PointF(79,67) }
                            };
                            foreach (PointF[] st in stars) g.FillPolygon(br2, st);
                        }
                    };
                case "compass":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Circle(g, pp, null, 50, 22, 7, 7, false, true);
                            Line(g, pp, 46, 28, 32, 78);
                            Line(g, pp, 54, 28, 68, 78);
                            Arc(g, pp, 50, 78, 18, 200, 140);
                        }, glow, p);
                    };
                case "rosette":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            double a = i * Math.PI / 5.0;
                            float x = 50 + (float)(20 * Math.Cos(a));
                            float y = 40 + (float)(20 * Math.Sin(a));
                            Circle(g, p, null, x, y, 6.5f, 6.5f, false, true);
                        }
                        Circle(g, p, null, 50, 40, 15, 15, false, true);
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 42, 56, 38, 82); Line(g, pp, 58, 56, 62, 82);
                        }, glow, p);
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 255, 190, 90)))
                        {
                            g.FillEllipse(br, 45, 35, 10, 10);
                        }
                    };
                case "infinity":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            g.DrawArc(pp, 22, 34, 34, 32, 60, 300);
                            g.DrawArc(pp, 44, 34, 34, 32, 240, 300);
                        }, glow, p);
                        Stroke2(g, delegate(Pen pp)
                        {
                            Line(g, pp, 76, 24, 84, 16); Line(g, pp, 84, 16, 84, 26);
                        }, glow, p);
                    };
                case "dome":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Arc(g, pp, 50, 68, 30, 180, 180);
                            Line(g, pp, 16, 68, 84, 68);
                            Arc(g, pp, 50, 68, 20, 180, 180);
                            Line(g, pp, 50, 14, 50, 38);
                        }, glow, p);
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(170, 255, 170, 90)))
                        {
                            g.FillEllipse(br, 30, 46, 6, 6);
                            g.FillEllipse(br, 64, 40, 6, 6);
                        }
                    };
                case "cap":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Poly(g, pp, null, new PointF[] { new PointF(50, 26), new PointF(84, 42), new PointF(50, 58), new PointF(16, 42) }, false, true);
                            g.DrawBezier(pp, 34, 50, 34, 62, 40, 68, 50, 70);
                            g.DrawBezier(pp, 50, 70, 60, 68, 66, 62, 66, 50);
                            Line(g, pp, 84, 42, 84, 62);
                            Circle(g, pp, null, 84, 66, 3.5f, 3.5f, false, true);
                        }, glow, p);
                    };
                case "spiral":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            PointF[] pts = new PointF[80];
                            for (int i = 0; i < 80; i++)
                            {
                                double tt = i * 0.16;
                                double rr = 2.2 * tt;
                                pts[i] = new PointF(50 + (float)(rr * Math.Cos(tt)), 50 + (float)(rr * Math.Sin(tt)));
                            }
                            g.DrawLines(pp, pts);
                        }, glow, p);
                    };
                case "boot":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        GraphicsState s = g.Save();
                        g.TranslateTransform(50, 50); g.RotateTransform(-14); g.TranslateTransform(-50, -50);
                        g.FillPath(b, BootPath());
                        g.DrawPath(p, BootPath());
                        g.Restore(s);
                    };
                case "upload":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            Circle(g, pp, null, 50, 50, 32, 32, false, true);
                            Line(g, pp, 50, 68, 50, 36);
                            Line(g, pp, 38, 48, 50, 34); Line(g, pp, 50, 34, 62, 48);
                        }, glow, p);
                    };
                case "spark4":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        PointF[] star = {
                            new PointF(50,10), new PointF(59,41), new PointF(90,50), new PointF(59,59),
                            new PointF(50,90), new PointF(41,59), new PointF(10,50), new PointF(41,41)
                        };
                        g.FillPolygon(b, star);
                        g.DrawPolygon(p, star);
                    };
                case "flexarm":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Stroke2(g, delegate(Pen pp)
                        {
                            // 弯曲手臂
                            g.DrawBezier(pp, 26, 74, 24, 46, 38, 26, 58, 28);
                            g.DrawBezier(pp, 58, 28, 76, 30, 80, 44, 72, 52);
                            g.DrawBezier(pp, 72, 52, 60, 62, 44, 58, 40, 74);
                            // 拳头
                            Circle(g, pp, null, 68, 38, 11, 10, false, true);
                        }, glow, p);
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 255, 190, 90)))
                        {
                            PointF[] up = { new PointF(86, 20), new PointF(80, 32), new PointF(84, 32), new PointF(84, 44), new PointF(88, 44), new PointF(88, 32), new PointF(92, 32) };
                            g.FillPolygon(br, up);
                        }
                    };
                case "spikeheart":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            double a = i * Math.PI / 5.0 - Math.PI / 2.0;
                            float x1 = 50 + (float)(24 * Math.Cos(a));
                            float y1 = 50 + (float)(24 * Math.Sin(a));
                            float x2 = 50 + (float)(34 * Math.Cos(a));
                            float y2 = 50 + (float)(34 * Math.Sin(a));
                            Stroke2(g, delegate(Pen pp) { Line(g, pp, x1, y1, x2, y2); }, glow, p);
                        }
                        Stroke2(g, delegate(Pen pp)
                        {
                            g.DrawBezier(pp, 50, 68, 30, 54, 30, 36, 42, 34);
                            g.DrawBezier(pp, 42, 34, 48, 33, 50, 38, 50, 42);
                            g.DrawBezier(pp, 50, 42, 50, 38, 52, 33, 58, 34);
                            g.DrawBezier(pp, 58, 34, 70, 36, 70, 54, 50, 68);
                        }, glow, p);
                    };
                case "cards3":
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Action<Pen, float, float> card = delegate(Pen pc, float ang, float dx)
                        {
                            GraphicsState s = g.Save();
                            g.TranslateTransform(50 + dx, 52); g.RotateTransform(ang); g.TranslateTransform(-50, -52);
                            g.DrawRectangle(pc, 36, 26, 28, 40);
                            g.Restore(s);
                        };
                        Stroke2(g, delegate(Pen pp) { card(pp, -22, -8); card(pp, 22, 8); }, glow, p);
                        GraphicsState s0 = g.Save();
                        g.TranslateTransform(50, 52); g.TranslateTransform(-50, -52);
                        g.FillRectangle(b, 37, 27, 26, 38);
                        g.DrawRectangle(p, 36, 26, 28, 40);
                        g.Restore(s0);
                        using (SolidBrush br = new SolidBrush(Color.FromArgb(200, 255, 210, 120)))
                        {
                            PointF[] st = { new PointF(82,14), new PointF(84,20), new PointF(90,22), new PointF(84,24), new PointF(82,30), new PointF(80,24), new PointF(74,22), new PointF(80,20) };
                            g.FillPolygon(br, st);
                        }
                    };
                default:
                    return delegate(Graphics g, Pen p, Brush b, Pen glow)
                    {
                        Circle(g, p, b, 50, 50, 30, 30, false, true);
                    };
            }
        }

        static GraphicsPath BootPath()
        {
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddLine(34f, 26f, 50f, 26f);
            path.AddLine(50f, 26f, 52f, 48f);
            path.AddLine(52f, 48f, 72f, 56f);
            path.AddLine(72f, 56f, 72f, 66f);
            path.AddLine(72f, 66f, 30f, 66f);
            path.AddLine(30f, 66f, 30f, 44f);
            path.CloseFigure();
            return path;
        }

        // ---------- 卡图 ----------
        static void GenCard(string root, string name)
        {
            string type;
            if (!CardType.TryGetValue(name, out type)) type = "skill";
            string glyphKey;
            if (!CardGlyph.TryGetValue(name, out glyphKey)) glyphKey = "spark4";
            Theme t = type == "attack" ? Attack : (type == "power" ? Power : Skill);

            int bigW = 1000, bigH = 760;
            Bitmap big = NewCanvas(bigW, bigH);
            using (Graphics g = G(big))
            {
                DrawBackground(g, bigW, bigH, t, name);
                RectangleF box = new RectangleF(bigW * 0.16f, bigH * 0.10f, bigW * 0.68f, bigH * 0.80f);
                Design(g, box, 100, delegate(Graphics gg) { Glyph(glyphKey)(gg, InkPen(t, 6.5f), new SolidBrush(t.Ink), GlowPen(t, 6.5f)); });
            }
            string dirBig = System.IO.Path.Combine(root, "card_portraits", "big");
            System.IO.Directory.CreateDirectory(dirBig);
            big.Save(System.IO.Path.Combine(dirBig, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);

            Bitmap small = NewCanvas(250, 190);
            using (Graphics g2 = G(small))
            {
                g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g2.DrawImage(big, new Rectangle(0, 0, 250, 190), 0, 0, bigW, bigH, GraphicsUnit.Pixel);
            }
            string dirSmall = System.IO.Path.Combine(root, "card_portraits");
            System.IO.Directory.CreateDirectory(dirSmall);
            small.Save(System.IO.Path.Combine(dirSmall, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);

            big.Dispose(); small.Dispose();
        }

        // 用户卡图裁剪（竖版 -> 卡牌横版比例）
        static void CropCard(string src, string outSmall, string outBig, double yFrac)
        {
            using (Bitmap srcBmp = new Bitmap(src))
            {
                int sw = srcBmp.Width, sh = srcBmp.Height;
                int bandH = (int)Math.Round(sw * 760.0 / 1000.0);
                if (bandH > sh) bandH = sh;
                int y = (int)Math.Round((sh - bandH) * yFrac);
                Rectangle crop = new Rectangle(0, y, sw, bandH);

                Bitmap big = NewCanvas(1000, 760);
                using (Graphics g = G(big))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(srcBmp, new Rectangle(0, 0, 1000, 760), crop, GraphicsUnit.Pixel);
                }
                big.Save(outBig, System.Drawing.Imaging.ImageFormat.Png);

                Bitmap small = NewCanvas(250, 190);
                using (Graphics g2 = G(small))
                {
                    g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g2.DrawImage(big, new Rectangle(0, 0, 250, 190), 0, 0, 1000, 760, GraphicsUnit.Pixel);
                }
                small.Save(outSmall, System.Drawing.Imaging.ImageFormat.Png);
                big.Dispose(); small.Dispose();
            }
        }

        // ---------- 能力图标 ----------
        static void GenPower(string root, string name, string glyphKey)
        {
            Theme t = Power;
            int bigS = 256;
            Bitmap big = NewCanvas(bigS, bigS);
            using (Graphics g = G(big))
            {
                g.Clear(Color.Transparent);
                // 圆形徽章底
                using (GraphicsPath disc = new GraphicsPath())
                {
                    disc.AddEllipse(20, 20, 216, 216);
                    using (PathGradientBrush pgb = new PathGradientBrush(disc))
                    {
                        pgb.CenterColor = Lighten(t.BgTop, 0.25f);
                        pgb.SurroundColors = new Color[] { t.BgBot };
                        g.FillPath(pgb, disc);
                    }
                    using (Pen ring = new Pen(t.Glow, 8f)) g.DrawEllipse(ring, 24, 24, 208, 208);
                    using (Pen ring2 = new Pen(Color.FromArgb(200, t.Ink), 5f)) g.DrawEllipse(ring2, 32, 32, 192, 192);
                }
                RectangleF box = new RectangleF(52, 52, 152, 152);
                Design(g, box, 100, delegate(Graphics gg) { Glyph(glyphKey)(gg, InkPen(t, 7f), new SolidBrush(t.Ink), GlowPen(t, 7f)); });
            }
            string dirBig = System.IO.Path.Combine(root, "powers", "big");
            System.IO.Directory.CreateDirectory(dirBig);
            big.Save(System.IO.Path.Combine(dirBig, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);

            Bitmap small = NewCanvas(64, 64);
            using (Graphics g2 = G(small))
            {
                g2.Clear(Color.Transparent);
                g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g2.DrawImage(big, new Rectangle(0, 0, 64, 64), 0, 0, bigS, bigS, GraphicsUnit.Pixel);
            }
            string dirSmall = System.IO.Path.Combine(root, "powers");
            System.IO.Directory.CreateDirectory(dirSmall);
            small.Save(System.IO.Path.Combine(dirSmall, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            big.Dispose(); small.Dispose();
        }

        static Color Lighten(Color c, float f)
        {
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * f), (int)(c.G + (255 - c.G) * f), (int)(c.B + (255 - c.B) * f));
        }

        // ---------- 遗物 ----------
        static void GenRelic(string root, string name, string glyphKey, bool isMedallion)
        {
            int bigS = 256, smallS = 94;

            Bitmap big = NewCanvas(bigS, bigS);
            using (Graphics g = G(big))
            {
                g.Clear(Color.Transparent);
                DrawRelicBody(g, glyphKey, isMedallion, false);
            }
            string dirBig = System.IO.Path.Combine(root, "relics", "big");
            System.IO.Directory.CreateDirectory(dirBig);
            big.Save(System.IO.Path.Combine(dirBig, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);

            Bitmap small = NewCanvas(smallS, smallS);
            using (Graphics g2 = G(small))
            {
                g2.Clear(Color.Transparent);
                g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g2.DrawImage(big, new Rectangle(0, 0, smallS, smallS), 0, 0, bigS, bigS, GraphicsUnit.Pixel);
            }
            string dirSmall = System.IO.Path.Combine(root, "relics");
            System.IO.Directory.CreateDirectory(dirSmall);
            small.Save(System.IO.Path.Combine(dirSmall, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);

            // 白色剪影 outline
            Bitmap ob = NewCanvas(bigS, bigS);
            using (Graphics g3 = G(ob))
            {
                g3.Clear(Color.Transparent);
                DrawRelicBody(g3, glyphKey, isMedallion, true);
            }
            Bitmap oSmall = NewCanvas(smallS, smallS);
            using (Graphics g4 = G(oSmall))
            {
                g4.Clear(Color.Transparent);
                g4.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g4.DrawImage(ob, new Rectangle(0, 0, smallS, smallS), 0, 0, bigS, bigS, GraphicsUnit.Pixel);
            }
            oSmall.Save(System.IO.Path.Combine(dirSmall, name + "_outline.png"), System.Drawing.Imaging.ImageFormat.Png);
            ob.Save(System.IO.Path.Combine(dirBig, name + "_outline.png"), System.Drawing.Imaging.ImageFormat.Png);

            big.Dispose(); small.Dispose(); ob.Dispose(); oSmall.Dispose();
        }

        static void DrawRelicBody(Graphics g, string glyphKey, bool isMedallion, bool outlineOnly)
        {
            Color body = outlineOnly ? Color.White : Color.FromArgb(0xd8, 0x8a, 0x2a);
            Color bodyDark = outlineOnly ? Color.White : Color.FromArgb(0x8a, 0x4a, 0x10);
            Color inkCol = outlineOnly ? Color.White : Color.FromArgb(0xf7, 0xee, 0xda);

            if (isMedallion)
            {
                using (SolidBrush br = new SolidBrush(body)) g.FillEllipse(br, 24, 24, 208, 208);
                using (SolidBrush br2 = new SolidBrush(bodyDark)) g.FillEllipse(br2, 40, 40, 176, 176);
                if (!outlineOnly)
                {
                    using (Font f = HaoFont(120f))
                    using (SolidBrush br3 = new SolidBrush(inkCol))
                    {
                        SizeF sz = g.MeasureString("豪", f);
                        g.DrawString("豪", f, br3, 128 - sz.Width / 2f, 126 - sz.Height / 2f);
                    }
                }
            }
            else
            {
                using (SolidBrush br = new SolidBrush(body)) g.FillEllipse(br, 32, 32, 192, 192);
                if (!outlineOnly)
                {
                    using (Pen rim = new Pen(bodyDark, 14f)) g.DrawEllipse(rim, 39, 39, 178, 178);
                    using (Font f = HaoFont(96f))
                    using (SolidBrush br3 = new SolidBrush(inkCol))
                    {
                        SizeF sz = g.MeasureString("W", f);
                        g.DrawString("W", f, br3, 128 - sz.Width / 2f, 124 - sz.Height / 2f);
                    }
                }
            }
        }

        // ---------- 能量表盘 ----------
        static void GenEnergy(string root, string bigPath, string smallPath)
        {
            int bigS = 148; // 74*2 超采样
            Bitmap big = NewCanvas(bigS, bigS);
            using (Graphics g = G(big))
            {
                g.Clear(Color.Transparent);
                using (GraphicsPath orb = new GraphicsPath())
                {
                    orb.AddEllipse(8, 8, bigS - 16, bigS - 16);
                    using (PathGradientBrush pgb = new PathGradientBrush(orb))
                    {
                        pgb.CenterColor = Color.FromArgb(0xff, 0xff, 0xc8, 0x60);
                        pgb.SurroundColors = new Color[] { Color.FromArgb(0xff, 0xc0, 0x50, 0x10) };
                        g.FillPath(pgb, orb);
                    }
                }
                using (Pen ring = new Pen(Color.FromArgb(0xff, 0x2a, 0x55, 0x90), 10f)) g.DrawEllipse(ring, 12, 12, bigS - 24, bigS - 24);
                using (Pen shine = new Pen(Color.FromArgb(150, 255, 255, 230), 7f)) g.DrawArc(shine, 24, 20, bigS - 48, bigS - 44, 130, 80);
                using (Font f = HaoFont(64f))
                using (SolidBrush br = new SolidBrush(Color.FromArgb(0xff, 0x3a, 0x14, 0x04)))
                {
                    SizeF sz = g.MeasureString("豪", f);
                    g.DrawString("豪", f, br, bigS / 2f - sz.Width / 2f, bigS / 2f - sz.Height / 2f - 4);
                }
            }
            string dBig = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(root, bigPath));
            System.IO.Directory.CreateDirectory(dBig);
            big.Save(System.IO.Path.Combine(root, bigPath), System.Drawing.Imaging.ImageFormat.Png);

            Bitmap small = NewCanvas(24, 24);
            using (Graphics g2 = G(small))
            {
                g2.Clear(Color.Transparent);
                g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g2.DrawImage(big, new Rectangle(0, 0, 24, 24), 0, 0, bigS, bigS, GraphicsUnit.Pixel);
            }
            string dSmall = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(root, smallPath));
            System.IO.Directory.CreateDirectory(dSmall);
            small.Save(System.IO.Path.Combine(root, smallPath), System.Drawing.Imaging.ImageFormat.Png);
            big.Dispose(); small.Dispose();
        }

        // ---------- 总入口 ----------
        public static string GenerateAll(string root)
        {
            int count = 0;
            System.Text.StringBuilder log = new System.Text.StringBuilder();

            // 卡图（跳过用户已提供的两张，由裁剪流程处理）
            List<string> cards = new List<string>(CardType.Keys);
            foreach (string c in cards)
            {
                if (c == "hao_strike" || c == "hao_defend") continue;
                GenCard(root, c); count++;
                log.Append(c).Append(' ');
            }

            // 用户两张卡图裁剪
            string cardDir = System.IO.Path.Combine(root, "card_portraits");
            CropCard(System.IO.Path.Combine(cardDir, "HaoStrike.png"),
                     System.IO.Path.Combine(cardDir, "hao_strike.png"),
                     System.IO.Path.Combine(cardDir, "big", "hao_strike.png"), 0.30);
            CropCard(System.IO.Path.Combine(cardDir, "HaoDefend.png"),
                     System.IO.Path.Combine(cardDir, "hao_defend.png"),
                     System.IO.Path.Combine(cardDir, "big", "hao_defend.png"), 0.34);
            count += 2;

            // 能力图标
            GenPower(root, "hao_power", "haochar");
            GenPower(root, "shang_hai_student_power", "sunwave");
            GenPower(root, "sneer_power", "mask");
            GenPower(root, "equal680_power", "scales");
            GenPower(root, "unrepentant_guard_power", "arrowshield");
            GenPower(root, "unrepentant_pain_power", "spikeheart");
            GenPower(root, "genius_power", "bulb");
            GenPower(root, "magnificent_draw_power", "cards3");
            GenPower(root, "magnificent_hao_power", "haochar");
            GenPower(root, "math_prince_power", "compass");
            GenPower(root, "math_prince_temp_strength_power", "flexarm");
            GenPower(root, "super_hao_field_power", "dome");
            GenPower(root, "tsinghua_form_power", "cap");
            GenPower(root, "final_commit_power", "upload");
            count += 14;

            // 遗物
            GenRelic(root, "hao", "medallion", true);
            GenRelic(root, "lucky_coin", "coin", false);
            count += 2;

            // 能量表盘
            GenEnergy(root, "energy_hao_big.png", "energy_hao.png");
            count += 1;

            return string.Format("done {0} assets: {1}", count, log.ToString());
        }
    }
}
"@

$result = [Sts2ArtGen.Gen]::GenerateAll($OutRoot)
Write-Output $result
