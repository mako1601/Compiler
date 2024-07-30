using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{
    /// <summary>
    /// Reverse Polish notation
    /// </summary>
    public class RPN
    {
        public List<Token> PostfixNotation { get; set; }
        public Dictionary<string, Token> LabelTable { get; private set; }

        public RPN()
        {
            PostfixNotation = [];
            LabelTable      = [];
        }

        public RPN(List<Token> tokens)
        {
            PostfixNotation = ConvertToPostfixNotation(tokens);
            LabelTable = LabelOptimization();
        }

        private static List<Token> ConvertToPostfixNotation(List<Token> tokens)
        {
            List<Token> result = [];
            Stack<Token> stack = [];

            // the beginning of the passage through tokens
            int index = 0;
            while (tokens[index].Type is not TokenType.Begin) index++;

            int labelCount = 0;
            while (index < tokens.Count)
            {
                Token token = tokens[index];

                if (token.Type is TokenType.Comma)
                {
                    // do nothing
                    index++;
                    continue;
                }
                if (token.Type is TokenType.Dot) break;

                switch (token.Type)
                {
                    case TokenType.Identifier:
                    case TokenType.Number:
                    case TokenType.Bool:
                        result.Add(token);
                        break;

                    case TokenType.RoundOpeningBracket:
                    case TokenType.Begin:
                    case TokenType.If:
                    case TokenType.Read:
                    case TokenType.Output:
                        stack.Push(token);
                        break;

                    case TokenType.RoundClosingBracket:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.RoundOpeningBracket)
                        {
                            result.Add(stack.Pop());
                        }
                        stack.Pop();

                        if (stack.Peek().Type is TokenType.LabelInfo)
                        {
                            result.Add(new Token(TokenType.LabelInfo, "true", stack.Pop().Line)); // conditional jump on truth (CJT)
                            stack.Pop(); //delete 'repeat'
                        }
                        else if (stack.Peek().Type is TokenType.Read || stack.Peek().Type is TokenType.Output)
                        {
                            result.Add(stack.Pop()); // delete 'read' or 'output'
                        }
                        break;

                    case TokenType.Semicolon:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.Begin)
                        {
                            result.Add(stack.Pop());
                        }
                        break;

                    case TokenType.End:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.Begin)
                        {
                            result.Add(stack.Pop());
                        }
                        stack.Pop();

                        if (stack.Count > 0 && stack.Peek().Type is TokenType.LabelInfo)
                        {
                            Token tmp1 = stack.Pop();
                            if (stack.Count > 0 && stack.Peek().Type is TokenType.LabelInfo)
                            {
                                Token tmp2 = stack.Pop();
                                if (stack.Count > 0 && stack.Peek().Type is TokenType.For)
                                {
                                    result.Add(new Token(TokenType.LabelInfo, "true", tmp2.Line));
                                    stack.Pop(); // delete 'for'

                                    result[tmp1.Line].Line = result.Count;
                                    result.Add(new Token(TokenType.Label, $"L{labelCount++}"));
                                }
                            }
                        }
                        break;

                    case TokenType.Then:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.If)
                        {
                            result.Add(stack.Pop());
                        }
                        stack.Pop(); // delete 'if'

                        stack.Push(token);
                        stack.Push(new Token(TokenType.LabelInfo, result.Count)); // index result where the jump to the label is stored
                        result.Add(new Token(TokenType.LabelInfo, "false")); // conditional jump on truth (CJT)
                        break;

                    case TokenType.Else:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.LabelInfo)
                        {
                            result.Add(stack.Pop());
                        }
                        result[stack.Pop().Line].Line = result.Count + 1;
                        stack.Pop(); // delete 'then'

                        stack.Push(token);
                        stack.Push(new Token(TokenType.LabelInfo, result.Count));
                        result.Add(new Token(TokenType.LabelInfo)); // unconditional jump (UJ)
                        result.Add(new Token(TokenType.Label, $"L{labelCount++}"));
                        break;

                    case TokenType.Endif:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.LabelInfo)
                        {
                            result.Add(stack.Pop());
                        }
                        result[stack.Pop().Line].Line = result.Count;
                        stack.Pop(); // delete 'else'

                        result.Add(new Token(TokenType.Label, $"L{labelCount++}"));
                        break;

                    case TokenType.Repeat:
                        stack.Push(token);
                        stack.Push(new Token(TokenType.LabelInfo, result.Count));
                        result.Add(new Token(TokenType.Label, $"L{labelCount++}"));
                        break;

                    case TokenType.Until:
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.LabelInfo)
                        {
                            result.Add(stack.Pop());
                        }
                        break;

                    case TokenType.For:
                        stack.Push(token);
                        stack.Push(new Token(TokenType.LabelInfo, result.Count));
                        result.Add(new Token(TokenType.Label, $"L{labelCount++}"));

                        index += 3;
                        token = tokens[index];
                        while (token.Type is not TokenType.Semicolon)
                        {
                            if (token.IsIdentifierOrNumberOrBool())
                            {
                                result.Add(token);
                            }
                            else if (token.Type is TokenType.RoundOpeningBracket)
                            {
                                stack.Push(token);
                            }
                            else if (token.Type is TokenType.RoundClosingBracket)
                            {
                                while (stack.Count > 0 && stack.Peek().Type is not TokenType.RoundOpeningBracket)
                                {
                                    result.Add(stack.Pop());
                                }
                                stack.Pop();
                            }
                            else
                            {
                                while (stack.Count > 0 && Precedence(token.Type) <= Precedence(stack.Peek().Type))
                                {
                                    result.Add(stack.Pop());
                                }
                                stack.Push(token);
                            }

                            token = tokens[++index];
                        }
                        while (stack.Count > 0 && stack.Peek().Type is not TokenType.LabelInfo)
                        {
                            result.Add(stack.Pop());
                        }

                        stack.Push(new Token(TokenType.LabelInfo, result.Count)); // index result where the jump to the label is stored
                        result.Add(new Token(TokenType.LabelInfo, "false")); // conditional jump on truth (CJT)
                        index++;
                        break;

                    default:
                        while (stack.Count > 0 && Precedence(token.Type) <= Precedence(stack.Peek().Type))
                        {
                            result.Add(stack.Pop());
                        }
                        stack.Push(token);
                        break;
                }

                index++;
            }

            while (stack.Count > 0) result.Add(stack.Pop());

            return result;
        }

        private Dictionary<string, Token> LabelOptimization()
        {
            for (int index = 0; index < PostfixNotation.Count - 1; index++)
            {
                if (PostfixNotation[index].Type is TokenType.Label && PostfixNotation[index + 1].Type is TokenType.Label)
                {
                    PostfixNotation.RemoveAt(index);
                    for (int i = 0; i < PostfixNotation.Count; i++)
                    {
                        if (PostfixNotation[i].Type is TokenType.LabelInfo && PostfixNotation[i].Line > index)
                        {
                            PostfixNotation[i].Line--;
                        }
                    }
                }
            }

            Dictionary<string, Token> labelTable = PostfixNotation
                .Select((token, index) => new { token, index })
                .Where(x => x.token.Type is TokenType.Label || x.token.Type is TokenType.LabelInfo)
                .ToDictionary(x => Convert.ToString(x.index), x => x.token);

            return labelTable;
        }

        private static int Precedence(TokenType type) => type switch
        {
            TokenType.Ass => 1,

            TokenType.And => 2,
            TokenType.Or  => 2,

            TokenType.LogicalEqual   => 3,
            TokenType.NotEqual       => 3,
            TokenType.Less           => 3,
            TokenType.Greater        => 3,
            TokenType.LessOrEqual    => 3,
            TokenType.GreaterOrEqual => 3,

            TokenType.Plus  => 4,
            TokenType.Minus => 4,

            TokenType.Multiply => 5,
            TokenType.Divide   => 5,

            TokenType.Not => 6,

            _ => 0
        };
    }
}
