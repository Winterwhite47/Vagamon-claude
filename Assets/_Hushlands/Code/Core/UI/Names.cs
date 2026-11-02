using System.Text;

namespace Hushlands.Core
{
    /// <summary>Display names from enum identifiers ("WoodenHillSled" -> "Wooden hill sled").</summary>
    public static class Names
    {
        public static string Pretty(string id)
        {
            var sb = new StringBuilder(id.Length + 8);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (i > 0 && char.IsUpper(c))
                {
                    sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
