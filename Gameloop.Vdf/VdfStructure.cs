namespace Gameloop.Vdf
{
    /// <summary>
    /// Defines the standard character constants used in the structure of VDF (Valve Data Format) files.
    /// </summary>
    public static class VdfStructure
    {
        /// <summary>Characters used to define arrays (primarily in KeyValues2/3).</summary>
        public const char ArrayStart = '[', ArrayEnd = ']', Comma = ',';

        /// <summary>Standard line ending characters.</summary>
        public const char CarriageReturn = '\r', NewLine = '\n';

        /// <summary>Characters for string delimiters, escaping, comments, and layout.</summary>
        public const char Quote = '"', Escape = '\\', Comment = '/', Assign = ' ', Indent = '\t';

        /// <summary>Characters used to define and parse conditional blocks (e.g., [$WIN32]).</summary>
        public const char ConditionalStart = '[', ConditionalEnd = ']', ConditionalConstant = '$', ConditionalNot = '!', ConditionalAnd = '&', ConditionalOr = '|';

        /// <summary>Characters used to define the boundaries of a VDF object.</summary>
        public const char ObjectStart = '{', ObjectEnd = '}';
    }

    /// <summary>
    /// Provides extension methods for <see cref="char"/> to handle VDF-specific escaping logic.
    /// </summary>
    public static class VdfCharExtensions
    {
        /// <summary>
        /// Determines whether a character is a standard VDF escapable control character or delimiter.
        /// </summary>
        /// <param name="ch">The character to check.</param>
        /// <returns><c>true</c> if the character can be escaped; otherwise, <c>false</c>.</returns>
        public static bool IsVdfEscapable(this char ch)
            => ch is '\n' or '\t' or '\v' or '\b' or '\r' or '\f' or '\a' or '\\' or '?' or '\'' or '\"';

        /// <summary>
        /// Converts a control character to its VDF escape literal (e.g., '\n' becomes 'n').
        /// </summary>
        /// <param name="ch">The control character.</param>
        /// <returns>The literal character representing the escape sequence.</returns>
        public static char ToVdfEscape(this char ch) => ch switch
        {
            '\n' => 'n',
            '\t' => 't',
            '\v' => 'v',
            '\b' => 'b',
            '\r' => 'r',
            '\f' => 'f',
            '\a' => 'a',
            '\\' => '\\',
            '?' => '?',
            '\'' => '\'',
            '\"' => '\"',
            _ => ch
        };

        /// <summary>
        /// Converts a VDF escape literal back to its actual control character (e.g., 'n' becomes '\n').
        /// </summary>
        /// <param name="ch">The escape literal.</param>
        /// <returns>The actual control character.</returns>
        public static char FromVdfEscape(this char ch) => ch switch
        {
            'n' => '\n',
            't' => '\t',
            'v' => '\v',
            'b' => '\b',
            'r' => '\r',
            'f' => '\f',
            'a' => '\a',
            '\\' => '\\',
            '?' => '?',
            '\'' => '\'',
            '\"' => '\"',
            _ => ch
        };
    }

    /// <summary>
    /// Specifies the version or style of the KeyValues format to be used during parsing or serialization.
    /// </summary>
    public enum KeyValuesFormat
    {
        /// <summary>The original Valve KeyValues format used in Source 1.</summary>
        Kv1,

        /// <summary>The updated KeyValues format used in newer Valve tools and Source 2.</summary>
        Kv2,

        /// <summary>The JSON-like format used in Dota 2 and other Source 2 applications.</summary>
        Kv3,

        /// <summary>Automatically detect the format based on the input content.</summary>
        Auto
    }
}