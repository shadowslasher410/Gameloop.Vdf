using System.Runtime.InteropServices;

namespace Gameloop.Vdf.Linq;

/// <summary>
/// Initializes a new instance of the <see cref="VConditional"/> class.
/// </summary>
/// <remarks>
/// VDF conditionals are typically used for platform-specific property inclusion, 
/// such as <c>[$WIN32]</c> or <c>[$POSIX]</c>.
/// </remarks>
public class VConditional() : VToken
{
    /// <summary>Commonly used VDF conditional platform constants.</summary>
    public const string Linux = "LINUX",
                        OsX = "OSX",
                        Posix = "POSIX",
                        Ps3 = "PS3",
                        Ps4 = "PS4",
                        Ps5 = "PS5",
                        SteamDeck = "STEAMDECK",
                        Win32 = "WIN32",
                        Windows = "WINDOWS",
                        X360 = "X360",
                        XboxOne = "XB1",
                        XboxSeriesX = "XBSX";

    /// <summary>
    /// The internal list of <see cref="Token"/> components that make up the conditional expression.
    /// </summary>
    private readonly List<Token> _tokens = [];

    /// <inheritdoc />
    public override VTokenType Type => VTokenType.Conditional;

    /// <summary>Gets the sequence of <see cref="Token"/> items that form this conditional expression.</summary>
    public IReadOnlyList<Token> Tokens => _tokens;

    /// <inheritdoc />
    public override VToken DeepClone()
    {
        VConditional newCond = [];
        foreach (var token in _tokens)
            newCond.Add(token.DeepClone());

        return newCond;
    }

    /// <inheritdoc />
    public override void WriteTo(VdfWriter writer) => writer.WriteConditional(_tokens);

    /// <inheritdoc />
    protected override bool DeepEquals(VToken token)
    {
        if (token is not VConditional other) return false;

        return _tokens.SequenceEqual(other._tokens,
            EqualityComparer<Token>.Create((t1, t2) => Token.DeepEquals(t1, t2)));
    }

    /// <summary>Adds a <see cref="Token"/> to the conditional expression.</summary>
    /// <param name="token">The token to add.</param>
    public void Add(Token token) => _tokens.Add(token);

    /// <summary>
    /// Evaluates the conditional expression against a list of defined platform/environment strings.
    /// </summary>
    /// <param name="definedConditionals">A list of strings representing the currently active conditions (e.g., ["WIN32"]).</param>
    /// <returns><c>true</c> if the expression evaluates to true; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the expression contains an unexpected token sequence.</exception>
    public bool Evaluate(IReadOnlyList<string> definedConditionals)
    {
        int index = 0;
        ReadOnlySpan<Token> tokenSpan = CollectionsMarshal.AsSpan(_tokens);

        if (tokenSpan.IsEmpty) return true;

        bool EvaluateToken(ReadOnlySpan<Token> tokens)
        {
            bool isNot = false;
            if (tokens[index].TokenType == TokenType.Not)
            {
                isNot = true;
                index++;
            }

            if (tokens[index].TokenType != TokenType.Constant)
                throw new InvalidOperationException($"Unexpected conditional token type ({tokens[index].TokenType}).");

            string tokenName = tokens[index++].Name!;
            bool isDefined = definedConditionals.Any(c => string.Equals(c, tokenName, StringComparison.OrdinalIgnoreCase));
            return isNot ^ isDefined;
        }

        bool runningResult = EvaluateToken(tokenSpan);
        while (index < tokenSpan.Length)
        {
            TokenType op = tokenSpan[index++].TokenType;

            runningResult = op switch
            {
                TokenType.Or => runningResult | EvaluateToken(tokenSpan),
                TokenType.And => runningResult & EvaluateToken(tokenSpan),
                _ => throw new InvalidOperationException($"Unexpected conditional operator ({op}).")
            };
        }

        return runningResult;
    }

    /// <summary>
    /// Represents a single unit in a conditional expression, such as a constant, an operator, or a modifier.
    /// </summary>
    /// <param name="tokenType">The type of the token.</param>
    /// <param name="name">The name of the constant (only applicable for <see cref="TokenType.Constant"/>).</param>
    public readonly struct Token(VConditional.TokenType tokenType, string? name = null)
    {
        /// <summary>Gets the type of this token.</summary>
        public TokenType TokenType { get; } = tokenType;

        /// <summary>Gets the name of the constant value, if applicable.</summary>
        public string? Name { get; } = name;

        /// <summary>Creates a deep copy of the current token.</summary>
        public Token DeepClone() => new(TokenType, Name);

        /// <summary>Determines if two tokens are identical in type and name.</summary>
        public static bool DeepEquals(Token t1, Token t2)
            => t1.TokenType == t2.TokenType && t1.Name == t2.Name;
    }

    /// <summary>
    /// Specifies the grammar types allowed within a VDF conditional expression.
    /// </summary>
    public enum TokenType 
    {
        /// <summary>A platform or environment key (e.g., "WIN32").</summary>
        Constant,

        /// <summary>The logical NOT operator (!).</summary>
        Not,

        /// <summary>The logical OR operator (||).</summary>
        Or,

        /// <summary>The logical AND operator (&amp;&amp;).</summary>
        And
    }
}
