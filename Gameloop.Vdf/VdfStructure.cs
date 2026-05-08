namespace Gameloop.Vdf
{
    public static class VdfStructure
    {
        public const char ArrayStart = '[', ArrayEnd = ']', Comma = ',';
        public const char CarriageReturn = '\r', NewLine = '\n';
        public const char Quote = '"', Escape = '\\', Comment = '/', Assign = ' ', Indent = '\t';
        public const char ConditionalStart = '[', ConditionalEnd = ']', ConditionalConstant = '$', ConditionalNot = '!', ConditionalAnd = '&', ConditionalOr = '|';
        public const char ObjectStart = '{', ObjectEnd = '}';
    }

    public static class VdfCharExtensions
    {
        public static bool IsVdfEscapable(this char ch)
            => ch is '\n' or '\t' or '\v' or '\b' or '\r' or '\f' or '\a' or '\\' or '?' or '\'' or '\"';

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

    public enum KeyValuesFormat
    {
        Kv1,
        Kv2,
        Kv3,
        Auto
    }
}
