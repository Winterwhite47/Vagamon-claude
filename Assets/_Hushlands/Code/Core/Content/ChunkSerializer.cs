using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Hushlands.Core
{
    /// <summary>
    /// Plain-text chunk format (.chunk), one record per line, invariant culture:
    ///   chunk   id biome difficulty length slopeDegrees
    ///   surface start length surface
    ///   feature kind start length height density variant optional requiresUpgrade warningLead
    ///   end
    /// Lines starting with '#' are comments.
    /// </summary>
    public static class ChunkSerializer
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double RadToDeg = 180.0 / Math.PI;

        public static string Write(IEnumerable<Chunk> chunks)
        {
            var sb = new StringBuilder();
            foreach (Chunk c in chunks)
            {
                sb.Append("chunk ").Append(c.Id).Append(' ').Append(c.Biome).Append(' ')
                    .Append(c.Difficulty.ToString(Inv)).Append(' ').Append(c.Length.ToString("R", Inv)).Append(' ')
                    .Append((c.SlopeRadians * RadToDeg).ToString("R", Inv)).Append('\n');
                foreach (SurfaceSpan s in c.Surfaces)
                {
                    sb.Append("surface ").Append(s.start.ToString("R", Inv)).Append(' ')
                        .Append(s.length.ToString("R", Inv)).Append(' ').Append(s.surface).Append('\n');
                }
                foreach (ChunkFeature f in c.Features)
                {
                    sb.Append("feature ").Append(f.kind).Append(' ')
                        .Append(f.start.ToString("R", Inv)).Append(' ')
                        .Append(f.length.ToString("R", Inv)).Append(' ')
                        .Append(f.height.ToString("R", Inv)).Append(' ')
                        .Append(f.density.ToString("R", Inv)).Append(' ')
                        .Append(f.variant.ToString(Inv)).Append(' ')
                        .Append(f.optional ? '1' : '0').Append(' ')
                        .Append(f.requiresUpgrade ? '1' : '0').Append(' ')
                        .Append(f.warningLead.ToString("R", Inv)).Append('\n');
                }
                sb.Append("end\n");
            }
            return sb.ToString();
        }

        public static List<Chunk> Read(string text)
        {
            var result = new List<Chunk>();
            Chunk current = null;
            int lineNo = 0;
            using (var reader = new StringReader(text ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    line = line.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    switch (p[0])
                    {
                        case "chunk":
                            Expect(p, 6, lineNo);
                            if (current != null) throw Error(lineNo, "chunk without end");
                            current = new Chunk(p[1], ParseEnum<BiomeId>(p[2], lineNo), int.Parse(p[3], Inv),
                                double.Parse(p[4], Inv), (float)(double.Parse(p[5], Inv) / RadToDeg));
                            break;
                        case "surface":
                            Expect(p, 4, lineNo);
                            Require(current, lineNo).AddSurface(new SurfaceSpan(double.Parse(p[1], Inv),
                                double.Parse(p[2], Inv), ParseEnum<SurfaceId>(p[3], lineNo)));
                            break;
                        case "feature":
                            Expect(p, 10, lineNo);
                            var f = ChunkFeature.Make(ParseEnum<FeatureKind>(p[1], lineNo), double.Parse(p[2], Inv),
                                double.Parse(p[3], Inv));
                            f.height = float.Parse(p[4], Inv);
                            f.density = float.Parse(p[5], Inv);
                            f.variant = int.Parse(p[6], Inv);
                            f.optional = p[7] == "1";
                            f.requiresUpgrade = p[8] == "1";
                            f.warningLead = double.Parse(p[9], Inv);
                            Require(current, lineNo).AddFeature(f);
                            break;
                        case "end":
                            result.Add(Require(current, lineNo));
                            current = null;
                            break;
                        default:
                            throw Error(lineNo, "unknown record '" + p[0] + "'");
                    }
                }
            }
            if (current != null) throw Error(lineNo, "missing end");
            Chunk.Assemble(result);
            return result;
        }

        private static void Expect(string[] p, int count, int lineNo)
        {
            if (p.Length != count) throw Error(lineNo, p[0] + " expects " + (count - 1) + " fields, got " + (p.Length - 1));
        }

        private static Chunk Require(Chunk c, int lineNo)
        {
            if (c == null) throw Error(lineNo, "record outside chunk");
            return c;
        }

        private static T ParseEnum<T>(string s, int lineNo) where T : struct
        {
            if (Enum.TryParse(s, false, out T v) && Enum.IsDefined(typeof(T), v)) return v;
            throw Error(lineNo, "bad " + typeof(T).Name + " '" + s + "'");
        }

        private static FormatException Error(int lineNo, string msg)
        {
            return new FormatException("chunk line " + lineNo + ": " + msg);
        }
    }
}
