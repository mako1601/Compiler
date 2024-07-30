using System.Text;
using System.Collections.Generic;

namespace Compiler
{
    public class Lexer
    {
        private string Input { get; set; }
        private int Position { get; set; }
        private int Line { get; set; }
        public List<string> Errors { get; private set; }

        public Lexer(string input)
        {
            Input    = input;
            Position = 0;
            Line     = 1;
            Errors   = [];
        }

        public List<Token> Tokenize()
        {
            List<Token> tokens = [];
            bool comment       = false;

            while (Position < Input.Length)
            {
                char currentChar = Input[Position];

                if (comment is true)
                {
                    // '*/'
                    if (currentChar is '*' && Position + 1 < Input.Length && Input[Position + 1] is '/')
                    {
                        comment = false;
                        Position += 2;
                        Errors.RemoveAt(Errors.Count - 1);
                    }
                    // 'new line'
                    else if (currentChar is '\n')
                    {
                        Position++;
                        Line++;
                    }
                    else
                    {
                        Position++;
                    }
                }
                else
                {
                    // whitespace and other invisible characters
                    if (char.IsWhiteSpace(currentChar))
                    {
                        if (currentChar == '\n')
                        {
                            Line++;
                        }
                        Position++;
                    }
                    // keywords or TokenType.Identifier
                    else if (IsLetter(currentChar))
                    {
                        string identifier = ReadIdentifier();

                        if (Terminals.TryGetValue(identifier, out TokenType keywordType))
                        {
                            tokens.Add(new Token(keywordType, identifier, Line));
                        }
                        else
                        {
                            tokens.Add(new Token(TokenType.Identifier, identifier, Line));
                        }
                    }
                    // TokenType.Number
                    else if (char.IsDigit(currentChar))
                    {
                        string number = ReadNumber(ref tokens);
                        tokens.Add(new Token(TokenType.Number, number, Line));
                    }
                    // '/*'
                    else if (currentChar is '/' && Position + 1 < Input.Length && Input[Position + 1] is '*')
                    {
                        comment = true;
                        Errors.Add($"Unclosed comment, line {Line}");
                        Position += 2;
                    }
                    // TokenType.LessOrEqual
                    else if (currentChar is '<' && Position + 1 < Input.Length && Input[Position + 1] is '=')
                    {
                        tokens.Add(new Token(TokenType.LessOrEqual, "<=", Line));
                        Position += 2;
                    }
                    // TokenType.GreaterOrEqual
                    else if (currentChar is '>' && Position + 1 < Input.Length && Input[Position + 1] is '=')
                    {
                        tokens.Add(new Token(TokenType.GreaterOrEqual, ">=", Line));
                        Position += 2;
                    }
                    // TokenType.NotEqual
                    else if (currentChar is '!' && Position + 1 < Input.Length && Input[Position + 1] is '=')
                    {
                        tokens.Add(new Token(TokenType.NotEqual, "!=", Line));
                        Position += 2;
                    }
                    // single characters
                    else if (Terminals.TryGetValue(currentChar.ToString(), out TokenType keywordType))
                    {
                        tokens.Add(new Token(keywordType, currentChar.ToString(), Line));
                        Position++;
                    }
                    // unexpected symbol
                    else
                    {
                        if (Errors.Contains($"Unexpected symbol, line: {Line}") is false)
                        {
                            Errors.Add($"Unexpected symbol, line: {Line}");
                        }
                        Position++;
                    }
                }
            }

            return tokens;
        }

        // example: qwerty, q123, q086e, qwe0976
        private string ReadIdentifier()
        {
            var identifier = new StringBuilder();
            identifier.Append(Input[Position++]);

            while (Position < Input.Length && IsLetterOrDigit(Input[Position]))
            {
                identifier.Append(Input[Position++]);
            }

            return identifier.ToString();
        }

        // example: 1, 1000, 0.1233, 239.2, -1, -4.23
        private string ReadNumber(ref List<Token> tokens)
        {
            var number = new StringBuilder();
            number.Append(Input[Position]);

            // remove TokenType.Minus if it was before the number
            if (Position > 0 && Input[Position - 1] is '-')
            {
                tokens.RemoveAt(tokens.Count - 1);
                number.Insert(0, "-");
            }
            Position++;

            while (Position < Input.Length && (char.IsDigit(Input[Position]) || (Input[Position] is '.' && number.ToString().Contains('.') is false)))
            {
                number.Append(Input[Position++]);
            }

            return number.ToString();
        }

        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        private static bool IsLetterOrDigit(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

        private static Dictionary<string, TokenType> Terminals { get; } = new Dictionary<string, TokenType>()
        {
            {"program", TokenType.Program },
            {"var", TokenType.Var },
            {"begin", TokenType.Begin},
            {"end", TokenType.End},
            {"and", TokenType.And},
            {"or", TokenType.Or},
            {"not", TokenType.Not},
            {"true", TokenType.Bool},
            {"false", TokenType.Bool},
            {"dim", TokenType.Dim},
            {"ass", TokenType.Ass},
            {"if", TokenType.If},
            {"then", TokenType.Then},
            {"else", TokenType.Else},
            {"endif", TokenType.Endif},
            {"for", TokenType.For},
            {"until", TokenType.Until},
            {"repeat", TokenType.Repeat},
            {"read", TokenType.Read},
            {"output", TokenType.Output},
            {"+", TokenType.Plus},
            {"-", TokenType.Minus},
            {"*", TokenType.Multiply},
            {"/", TokenType.Divide},
            {"=", TokenType.LogicalEqual},
            {"!=", TokenType.NotEqual},
            {"<", TokenType.Less},
            {"<=", TokenType.LessOrEqual},
            {">", TokenType.Greater},
            {">=", TokenType.GreaterOrEqual},
            {"%", TokenType.Percent},
            {"!", TokenType.Exclamation},
            {"$", TokenType.Dollar},
            {",", TokenType.Comma},
            {".", TokenType.Dot},
            {";", TokenType.Semicolon},
            {"(", TokenType.RoundOpeningBracket},
            {")", TokenType.RoundClosingBracket}
        };
    }
}
