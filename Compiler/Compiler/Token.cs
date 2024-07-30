namespace Compiler
{
    public enum TokenType
    {
        Program, Var, Begin, End, // program var begin end
        Dim, // dim
        Identifier,
        Number, // 0-9
        Bool, // true false
        Ass, // ass
        If, Then, Else, Endif, // if then else endif
        For, Repeat, Until, // for return until
        Read, Output, // read output
        Plus, Minus, Multiply, Divide, // + - * /
        Percent, Exclamation, Dollar, // % ! $ (int, float, bool)
        RoundOpeningBracket, RoundClosingBracket, // ( )
        LogicalEqual, NotEqual, Less, Greater, LessOrEqual, GreaterOrEqual, // = != < > <= >=
        And, Or, Not, // and or not
        Comma, Dot, Semicolon, // , . ;
        StartOfLine, EndOfLine, Nonterminal, // For parser
        Label, LabelInfo // For RPN
    }

    public class Token
    {
        public TokenType Type { get; private set; }
        public string Value { get; private set; }
        public int Line { get; set; }

        public Token(TokenType type)
        {
            Type = type;
            Value = string.Empty;
            Line = 0;
        }

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
            Line = 0;
        }

        public Token(TokenType type, int line)
        {
            Type = type;
            Value = string.Empty;
            Line = line;
        }

        public Token(TokenType type, string value, int line)
        {
            Type = type;
            Value = value;
            Line = line;
        }

        public Token(Token token)
        {
            Type = token.Type;
            Value = token.Value;
            Line = token.Line;
        }

        public bool IsType() => Type switch
        {
            TokenType.Percent     => true,
            TokenType.Exclamation => true,
            TokenType.Dollar      => true,

            _ => false
        };

        public bool IsIdentifierOrNumberOrBool() => Type switch
        {
            TokenType.Identifier => true,
            TokenType.Number     => true,
            TokenType.Bool       => true,

            _ => false
        };

        public bool IsArithmeticOperation() => Type switch
        {
            TokenType.Plus     => true,
            TokenType.Minus    => true,
            TokenType.Multiply => true,
            TokenType.Divide   => true,

            _ => false
        };

        public bool IsLogicalOperation() => Type switch
        {
            TokenType.LogicalEqual   => true,
            TokenType.NotEqual       => true,
            TokenType.Less           => true,
            TokenType.Greater        => true,
            TokenType.LessOrEqual    => true,
            TokenType.GreaterOrEqual => true,

            _ => false
        };

        public override bool Equals(object obj)
        {
            if (GetType() != obj.GetType()) return false;

            Token other = (Token)obj;
            return Type.Equals(other.Type) && Value.Equals(other.Value);
        }
        public override int GetHashCode() => Type.GetHashCode();
        public override string ToString() => $"{Line} {Type} {Value}";
    }
}
