using System.Runtime.InteropServices;

namespace Gameloop.Vdf.Linq;

public class VConditional() : VToken
{
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

    private readonly List<Token> _tokens = [];

    public override VTokenType Type => VTokenType.Conditional;
    public IReadOnlyList<Token> Tokens => _tokens;

    public override VToken DeepClone()
    {
        VConditional newCond = [];
        foreach (var token in _tokens)
            newCond.Add(token.DeepClone());

        return newCond;
    }

    public override void WriteTo(VdfWriter writer) => writer.WriteConditional(_tokens);

    protected override bool DeepEquals(VToken token)
    {
        if (token is not VConditional other) return false;

        return _tokens.SequenceEqual(other._tokens,
            EqualityComparer<Token>.Create((t1, t2) => Token.DeepEquals(t1, t2)));
    }

    public void Add(Token token) => _tokens.Add(token);

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

    public readonly struct Token(VConditional.TokenType tokenType, string? name = null)
    {
        public TokenType TokenType { get; } = tokenType;
        public string? Name { get; } = name;

        public Token DeepClone() => new(TokenType, Name);

        public static bool DeepEquals(Token t1, Token t2)
            => t1.TokenType == t2.TokenType && t1.Name == t2.Name;
    }

    public enum TokenType { Constant, Not, Or, And }
}
