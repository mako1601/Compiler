using System.Collections.Generic;

namespace Compiler
{
    public class Semantic
    {
        public struct IdData
        {
            public bool IsDefined { get; set; } // not used yet
            public TokenType Type { get; set; }

            public IdData(bool isDefined, TokenType type)
            {
                IsDefined = isDefined;
                Type      = type;
            }
        }

        private List<Token> Tokens {  get; set; }
        public Dictionary<string, IdData> IdTable { get; private set; }
        public List<string> Errors { get; private set; }

        // not used yet
        private List<(int, int)> Expressions { get; set; }
        private List<int> StartOfAss { get; set; }

        public Semantic(List<Token> tokens)
        {
            Tokens  = tokens;
            IdTable = [];
            Errors  = [];

            // not used yet
            Expressions = [];
            StartOfAss  = [];
        }

        /// <summary>
        /// Checks that all variables are declared only once and populates the table of declared variables.
        /// </summary>
        public void CheckDeclaration()
        {
            int index = 3; // 3 index is always an identifier (example: program var dim 'Identifier' ...)
            while (Tokens[index].Type is not TokenType.Begin)
            {
                if (Tokens[index].Type is TokenType.Identifier)
                {
                    if (IdTable.ContainsKey(Tokens[index].Value))
                    {
                        Errors.Add("Multiple variable declaration: " + Tokens[index].Value);
                    }
                    else
                    {
                        IdTable.Add(Tokens[index].Value, new IdData(false, TokenType.Identifier));
                    }
                }
                else if (Tokens[index].IsType())
                {
                    Dictionary<string, IdData> newIdTable = [];

                    foreach (var kvp in IdTable)
                    {
                        string key = kvp.Key;
                        IdData value = kvp.Value;

                        value.Type = Tokens[index].Type;
                        newIdTable.Add(key, value);
                    }

                    IdTable = newIdTable;
                }

                index++;
            }

            while (index < Tokens.Count - 2)
            {
                switch (Tokens[index].Type)
                {
                    // checking for an undeclared variable
                    case TokenType.Identifier when IdTable.ContainsKey(Tokens[index].Value) is false:
                        Errors.Add("Unknown variable: " + Tokens[index].Value + ", line " + Tokens[index].Line);
                        break;

                    // assignment start index
                    case TokenType.Ass:
                        StartOfAss.Add(index - 1);
                        // finding the beginning and end of an expression in an assignment
                        int startAss = index + 1;
                        int endAss   = startAss;
                        while (IsTerminalOfAssignmentEnd(Tokens[endAss + 1].Type) is false) endAss++;
                        Expressions.Add((startAss, endAss));
                        break;

                    // further, search for the beginning and end of expressions
                    case TokenType.If:
                        int startIf = index + 1;
                        int endIf   = startIf;
                        while (Tokens[endIf + 1].Type is not TokenType.Then) endIf++;
                        Expressions.Add((startIf, endIf));
                        break;

                    case TokenType.Until:
                        int startUntil = index + 2;
                        int endUntil   = startUntil;
                        for (int i = 1; i != 0; endUntil++)
                        {
                            if (Tokens[endUntil].Type is TokenType.RoundOpeningBracket) i++;
                            if (Tokens[endUntil].Type is TokenType.RoundClosingBracket) i--;
                        }
                        Expressions.Add((startUntil, endUntil - 2));
                        break;

                    case TokenType.For:
                        int startFor = index + 2;
                        int endFor   = startFor;
                        for (int i = 1; i != 0; endFor++)
                        {
                            if (Tokens[endFor].Type is TokenType.RoundOpeningBracket) i++;
                            if (Tokens[endFor].Type is TokenType.RoundClosingBracket) i--;
                            if (Tokens[endFor].Type is TokenType.Semicolon)
                            {
                                if (startFor <= endFor - 1)
                                {
                                    Expressions.Add((startFor, endFor - 1));
                                    startFor = endFor + 1;
                                }
                                else
                                {
                                    startFor++;
                                }
                            }
                        }
                        if (startFor <= endFor - 2)
                        {
                            Expressions.Add((startFor, endFor - 2));
                        }
                        break;
                }

                index++;
            }
        }

        public void CheckExpression()
        {
            //
        }

        public void CheckAssignment()
        {
            //
        }

        private static bool IsTerminalOfAssignmentEnd(TokenType type) => type switch
        {
            TokenType.Semicolon => true,
            TokenType.End       => true,
            TokenType.Else      => true,
            TokenType.Endif     => true,
            TokenType.Until     => true,

            _ => false
        };
    }
}
