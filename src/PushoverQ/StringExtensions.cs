using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// The string extensions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Takes the specified number of characters from a string from the right.
        /// </summary>
        /// <param name="str"> The <see cref="string"/>. </param>
        /// <param name="length"> The length. </param>
        /// <returns> The left-truncated <see cref="string"/>. </returns>
        public static string Right(this string str, int length)
        {
            if (string.IsNullOrEmpty(str))
                str = string.Empty;
            else if (str.Length > length)
                str = str.Substring(str.Length - length, length);
            
            return str;
        }
    }
}
