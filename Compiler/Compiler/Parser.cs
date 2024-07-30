using System;
using System.Collections.Generic;

namespace Compiler
{
    public static class Parser
    {
        private static char[,] OperatorPrecedenceMatrix { get; set; } = new char[40, 40];

        public static void InitializeParser()
        {
            OperatorPrecedenceMatrix = FillMatrix();
        }

        /// <summary>
        /// Fills out the operator precedence matrix according to the given grammar.
        /// </summary>
        /// <returns>Two dimensional array of characters.</returns>
        private static char[,] FillMatrix()
        {
            char[,] matrix = new char[(int)TokenType.EndOfLine, (int)TokenType.EndOfLine];
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] = ' ';
                }
            }

            matrix[matrix.GetLength(0) - 1, (int)TokenType.Program] = '<'; // start symbol
            matrix[(int)TokenType.Dot, matrix.GetLength(0) - 1] = '>';     // end symbol

            Dictionary<string, List<HashSet<TokenType>>> extremeLeftAndExtremeRightTerminals = GetExtremeLeftAndExtremeRightTerminals();

            foreach (var kvp in _grammar)
            {
                List<string> words = kvp.Value;

                for (int i = 0; i < words.Count; i++)
                {
                    if (_terminals.ContainsKey(words[i]) is false) continue;

                    if (_terminals.ContainsKey(words[i]) && i - 1 >= 0 && !_terminals.ContainsKey(words[i - 1]) && !words[i - 1].Equals("|"))
                    {
                        extremeLeftAndExtremeRightTerminals.TryGetValue(words[i - 1], out var lists);
                        if (lists is not null)
                        {
                            foreach (TokenType token in lists[1])
                            {
                                matrix[(int)token, (int)_terminals[words[i]]] = '>';
                            }
                        }
                    }

                    if (_terminals.ContainsKey(words[i]) && i + 1 < words.Count && !words[i + 1].Equals("|"))
                    {
                        if (!_terminals.ContainsKey(words[i + 1]))
                        {
                            extremeLeftAndExtremeRightTerminals.TryGetValue(words[i + 1], out var lists);
                            if (lists is not null)
                            {
                                foreach (TokenType token in lists[0])
                                {
                                    matrix[(int)_terminals[words[i]], (int)token] = '<';
                                }
                            }
                        }
                        else
                        {
                            matrix[(int)_terminals[words[i]], (int)_terminals[words[i + 1]]] = '=';
                            continue;
                        }

                        if (i + 2 < words.Count && _terminals.ContainsKey(words[i + 2]))
                        {
                            matrix[(int)_terminals[words[i]], (int)_terminals[words[i + 2]]] = '=';
                            continue;
                        }
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Constructs sets of leftmost and rightmost terminal symbols.
        /// </summary>
        /// <returns>A dictionary, where the key is a non-terminal symbol and the value is a list of unique terminal symbols (tokens).</returns>
        private static Dictionary<string, List<HashSet<TokenType>>> GetExtremeLeftAndExtremeRightTerminals()
        {
            // First, we create a set of leftmost and rightmost symbols
            Dictionary<string, List<HashSet<string>>> extremeLeftAndExtremeRightSymbols = GetExtremeLeftAndExtremeRightSymbols();

            // Then we complete the set of leftmost and rightmost characters
            ComplementSet(ref extremeLeftAndExtremeRightSymbols);

            // And finally, we create a set of leftmost and rightmost terminal symbol
            var extremeLeftAndExtremeRightTerminals = new Dictionary<string, List<HashSet<TokenType>>>();
            (Dictionary<string, HashSet<TokenType>> extremeLeftTerminal, Dictionary<string, HashSet<TokenType>> extremeRightTerminal) = GetExtremeLeftAndExtremeRightTerminal();

            foreach (var kvp in extremeLeftAndExtremeRightSymbols)
            {
                string nonterminalSymbol = kvp.Key;
                List<HashSet<string>> sets = kvp.Value;

                List<HashSet<TokenType>> newSets = [];

                for (int i = 0; i < 2; i++)
                {
                    HashSet<TokenType> tokens = [];

                    foreach (string str in sets[i])
                    {
                        if (_terminals.TryGetValue(str, out TokenType token))
                        {
                            tokens.Add(token);
                            continue;
                        }
                        if (i is 0 && extremeLeftTerminal.TryGetValue(str, out var set))
                        {
                            tokens.UnionWith(set);
                            continue;
                        }
                        if (i is 1 && extremeRightTerminal.TryGetValue(str, out set))
                        {
                            tokens.UnionWith(set);
                            continue;
                        }
                    }

                    newSets.Add(tokens);
                }

                extremeLeftAndExtremeRightTerminals.Add(nonterminalSymbol, newSets);
            }

            return extremeLeftAndExtremeRightTerminals;
        }

        /// <summary>
        /// Returns the set of the leftmost and rightmost characters.
        /// </summary>
        /// <returns>A dictionary, where the key is a non-terminal symbol and the value is a list of unique symbols (terminal and/or non-terminal symbols).</returns>
        private static Dictionary<string, List<HashSet<string>>> GetExtremeLeftAndExtremeRightSymbols()
        {
            Dictionary<string, List<HashSet<string>>> extremeSymbols = [];

            foreach (var kvp in _grammar)
            {
                string nonterminalSymbol = kvp.Key;
                List<string> words       = kvp.Value;

                HashSet<string> extremeLeftSymbols  = [];
                HashSet<string> extremeRightSymbols = [];

                for (int wordIndex = 0; wordIndex < words.Count; wordIndex++)
                {
                    // Checking the delimiter '|'
                    if (words[wordIndex].Equals("|"))
                    {
                        extremeRightSymbols.Add(words[wordIndex - 1]);
                        extremeLeftSymbols.Add(words[wordIndex + 1]);

                        if (wordIndex + 2 < words.Count)
                        {
                            wordIndex++;
                        }
                        continue;
                    }
                    // Checking the first word on the right side
                    if (wordIndex is 0)
                    {
                        extremeLeftSymbols.Add(words[wordIndex]);
                        if ((wordIndex + 1 < words.Count && words[wordIndex + 1].Equals("|")) || wordIndex + 1 == words.Count)
                        {
                            extremeRightSymbols.Add(words[wordIndex]);
                        }
                        continue;
                    }
                    // Checking the last word on the right side
                    if (wordIndex == words.Count - 1)
                    {
                        extremeRightSymbols.Add(words[wordIndex]);
                    }
                }

                // Adding many leftmost and rightmost characters to the dictionary
                extremeSymbols.Add(nonterminalSymbol, [extremeLeftSymbols, extremeRightSymbols]);
            }

            return extremeSymbols;
        }

        /// <summary>
        /// Complete with many leftmost and rightmost symbols.
        /// </summary>
        private static void ComplementSet(ref Dictionary<string, List<HashSet<string>>> extremeLeftAndExtremeRightSymbols)
        {
            while (true)
            {
                Dictionary<string, List<HashSet<string>>> newExtremeLeftAndExtremeRightSymbols = [];

                foreach (var kvp in extremeLeftAndExtremeRightSymbols)
                {
                    string nonterminalSymbol   = kvp.Key;
                    List<HashSet<string>> sets = kvp.Value;

                    List<HashSet<string>> newSets = [];

                    for (int i = 0; i < 2; i++)
                    {
                        HashSet<string> strings = [];

                        foreach (string str in sets[i])
                        {
                            strings.Add(str);

                            if (extremeLeftAndExtremeRightSymbols.TryGetValue(str, out var newStr))
                            {
                                for (int j = i; j < i + 1; j++)
                                {
                                    foreach (string s in newStr[j])
                                    {
                                        strings.Add(s);
                                    }
                                }
                            }
                        }

                        newSets.Add(strings);
                    }

                    newExtremeLeftAndExtremeRightSymbols.Add(nonterminalSymbol, newSets);
                }

                if (Equals(extremeLeftAndExtremeRightSymbols, newExtremeLeftAndExtremeRightSymbols))
                {
                    break;
                }
                else
                {
                    extremeLeftAndExtremeRightSymbols = newExtremeLeftAndExtremeRightSymbols;
                }
            }
        }

        /// <summary>
        /// Returns the leftmost or rightmost terminal character from each rule.
        /// </summary>
        /// <returns>A cartage of two dictionaries, where the key is a non-terminal symbol, and the value is unique tokens, those terminal symbols.</returns>
        private static (Dictionary<string, HashSet<TokenType>>, Dictionary<string, HashSet<TokenType>>) GetExtremeLeftAndExtremeRightTerminal()
        {
            Dictionary<string, HashSet<TokenType>> extremeLeftTerminals  = [];
            Dictionary<string, HashSet<TokenType>> extremeRightTerminals = [];

            foreach (var grammarRule in _grammar)
            {
                HashSet<TokenType> firstTerminals = [];
                HashSet<TokenType> lastTerminals  = [];

                string nonterminalSymbol = grammarRule.Key;
                List<string> words       = grammarRule.Value;

                bool isFirstTerminal = true;
                bool isLastTerminal  = true;

                for (int i = 0, j = words.Count - 1; i < words.Count && j > -1; i++, j--)
                {
                    if (isFirstTerminal && _terminals.TryGetValue(words[i], out var value))
                    {
                        firstTerminals.Add(value);
                        isFirstTerminal = false;
                    }
                    if (words[i].Equals("|") is true)
                    {
                        isFirstTerminal = true;
                    }

                    if (isLastTerminal && _terminals.TryGetValue(words[j], out var _value))
                    {
                        lastTerminals.Add(_value);
                        isLastTerminal = false;
                    }
                    if (words[j].Equals("|") is true)
                    {
                        isLastTerminal = true;
                    }
                }

                extremeLeftTerminals.Add(nonterminalSymbol, firstTerminals);
                extremeRightTerminals.Add(nonterminalSymbol, lastTerminals);
            }

            return (extremeLeftTerminals, extremeRightTerminals);
        }

        /// <summary>
        /// Определяет, равны ли два словаря типа Dictionary<string, List<HashSet<string>>>.
        /// </summary>
        /// <returns>Returns true if the value of the first dictionary matches the value of the second dictionary; otherwise, false.</returns>
        private static bool Equals(Dictionary<string, List<HashSet<string>>> first, Dictionary<string, List<HashSet<string>>> second)
        {
            foreach (var kvp in first)
            {
                string key = kvp.Key;
                List<HashSet<string>> valueOfFirst = kvp.Value;
                second.TryGetValue(key, out var valueOfSecond);

                if (valueOfSecond is not null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        if (valueOfFirst[i].Count != valueOfSecond[i].Count)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Performs syntactic analysis of the input sequence of characters (tokens) using the shift-convolution algorithm.
        /// </summary>
        public static void ShiftConvolution(List<Token> tokens)
        {
            List<Token> characterChain = [.. tokens];
            characterChain.Add(new Token(TokenType.EndOfLine));
            List<string> rules = [
                "program var E begin E end .",
                "dim E %", "dim E !", "dim E $",
                "id", "E , id",
                "E ;", "E ; E",
                "begin E end", "E ; begin E end",
                "id ass E",
                "if E then E else E endif", "E ; if E then E else E endif",
                "for E ; E ) begin E end", "for E ; ) begin E end", "E ; for E ; ) begin E end", "E ; for E ; E ) begin E end",
                "repeat E until ( E )", "E ; repeat E until ( E )",
                "read ( E )", "E ; read ( E )",
                "output ( E )", "E ; output ( E )",
                "( ;", "( E ;", "( ; E", "( E ; E",
                "not E",
                "E + E", "E - E",
                "E * E", "E / E",
                "E = E", "E != E", "E < E", "E > E", "E <= E", "E >= E",
                "E and E", "E or E",
                "id", "num", "bool", "( E )"
            ];

            // Step 1: Create a stack and add the "beginning of line" character to it
            Stack<Token> stack = [];
            stack.Push(new Token(TokenType.StartOfLine));

            int currentIndex = 0;

            while ((stack.Peek().Type is TokenType.Nonterminal && stack.Count is 2) is false)
            {
                Token topOfStack = stack.Peek();
                Token currentToken = characterChain[currentIndex];

                if (topOfStack.Type is not TokenType.Nonterminal)
                {
                    // If the start character
                    if (topOfStack.Type > TokenType.StartOfLine)
                    {
                        int row = (int)TokenType.StartOfLine;
                        if (OperatorPrecedenceMatrix[row, (int)currentToken.Type].Equals('<')
                            || OperatorPrecedenceMatrix[row, (int)currentToken.Type].Equals('='))
                        {
                            stack.Push(currentToken);
                            currentIndex++;
                            continue;
                        }
                    }
                    // If the end character
                    if (currentToken.Type > TokenType.StartOfLine)
                    {
                        int column = (int)TokenType.StartOfLine;
                        if (OperatorPrecedenceMatrix[(int)topOfStack.Type, column].Equals('>'))
                        {
                            string str = string.Empty;
                            currentToken = SkipNonterminals(stack, ref str);
                            topOfStack = SkipNonterminals(stack);

                            while (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('='))
                            {
                                currentToken = SkipNonterminals(stack, ref str);
                                topOfStack = SkipNonterminals(stack);
                            }

                            // rule search
                            for (int i = 0; i < rules.Count; i++)
                            {
                                if (str.Equals(rules[i]) is true)
                                {
                                    stack.Push(new Token(TokenType.Nonterminal));
                                    break;
                                }
                                if (i + 1 == rules.Count)
                                {
                                    throw new SyntaxException("Convolution is impossible, there is no grammar rule");
                                }
                            }
                            continue;
                        }
                        else
                        {
                            throw new SyntaxException($"There is no precedence relationship between tokens \'{topOfStack.Value}\' and \'{currentToken.Value}\'!");
                        }
                    }
                    // shift
                    if (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('<')
                        || OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('='))
                    {
                        stack.Push(currentToken);
                        currentIndex++;
                        continue;
                    }
                    // convolution
                    else if (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('>'))
                    {
                        string str = string.Empty;
                        currentToken = SkipNonterminals(stack, ref str);
                        topOfStack = SkipNonterminals(stack);

                        while (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('='))
                        {
                            currentToken = SkipNonterminals(stack, ref str);
                            topOfStack = SkipNonterminals(stack);
                        }

                        // rule search
                        for (int i = 0; i < rules.Count; i++)
                        {
                            if (str.Equals(rules[i]))
                            {
                                stack.Push(new Token(TokenType.Nonterminal));
                                break;
                            }
                            if (i + 1 == rules.Count)
                            {
                                throw new SyntaxException("Convolution is impossible, there is no grammar rule");
                            }
                        }
                        continue;
                    }
                    else
                    {
                        throw new SyntaxException($"There is no precedence relationship between tokens \'{topOfStack.Value}\' and \'{currentToken.Value}\'!");
                    }
                }
                // If there is a nonterminal at the top of the stack
                else
                {
                    topOfStack = SkipNonterminals(stack);

                    // shift
                    if (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('<')
                        || OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('='))
                    {
                        stack.Push(currentToken);
                        currentIndex++;
                        continue;
                    }
                    // convolution
                    else if (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('>'))
                    {
                        string str = string.Empty;
                        currentToken = SkipNonterminals(stack, ref str);
                        topOfStack = SkipNonterminals(stack);

                        while (OperatorPrecedenceMatrix[(int)topOfStack.Type, (int)currentToken.Type].Equals('='))
                        {
                            currentToken = SkipNonterminals(stack, ref str);
                            topOfStack = SkipNonterminals(stack);
                        }

                        // rule search
                        for (int i = 0; i < rules.Count; i++)
                        {
                            if (str.Equals(rules[i]))
                            {
                                stack.Push(new Token(TokenType.Nonterminal));
                                break;
                            }
                            if (i + 1 == rules.Count)
                            {
                                throw new SyntaxException("Convolution is impossible, there is no grammar rule");
                            }
                        }
                        continue;
                    }
                    else
                    {
                        throw new SyntaxException($"There is no precedence relationship between tokens \'{topOfStack.Value}\' и \'{currentToken.Value}\'!");
                    }
                }
            }
        }

        /// <summary>
        /// Forms a rule that should be collapsed, passing nonterminals from the stack to the terminal.
        /// </summary>
        /// <returns>Removes and returns the token at the top of the stack.</returns>
        private static Token SkipNonterminals(Stack<Token> stack, ref string str)
        {
            Token token = stack.Pop();

            if (str.Equals(string.Empty))
            {
                str = ConvertToString(token);
            }
            else
            {
                str = ConvertToString(token) + " " + str;
            }

            if (token.Type is TokenType.Nonterminal)
            {
                token = stack.Pop();
                str = ConvertToString(token) + " " + str;
            }

            while (stack.Peek().Type is TokenType.Nonterminal)
            {
                str = ConvertToString(stack.Pop()) + " " + str;
            }

            return token;
        }

        /// <summary>
        /// Looks for the first terminal at the top of the stack.
        /// </summary>
        /// <returns>Returns the terminal token at the top of the stack.</returns>
        private static Token SkipNonterminals(Stack<Token> stack)
        {
            int count = 0;
            while (stack.Peek().Type is TokenType.Nonterminal)
            {
                count++;
                stack.Pop();
            }
            Token topOfStack = stack.Peek();
            for (int i = 0; i < count; i++)
            {
                stack.Push(new Token(TokenType.Nonterminal));
            }
            return topOfStack;
        }

        /// <summary>
        /// Converts the token type into a string, which will be needed to find the required grammar rule.
        /// </summary>
        /// <returns>Returns a string corresponding to the token type.</returns>
        private static string ConvertToString(Token token) => token.Type switch
        {
            TokenType.Nonterminal => "E",
            TokenType.Identifier  => "id",
            TokenType.Number      => "num",
            TokenType.Bool        => "bool",

            _ => token.Value
        };

        private static readonly Dictionary<string, TokenType> _terminals = new()
        {
            {"program", TokenType.Program},
            {"var", TokenType.Var},
            {"begin", TokenType.Begin},
            {"end", TokenType.End},
            {"and", TokenType.And},
            {"or", TokenType.Or},
            {"not", TokenType.Not},
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
            {"id", TokenType.Identifier},
            {"num", TokenType.Number},
            {"bool", TokenType.Bool},
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

        private readonly static Dictionary<string, List<string>> _grammar = new()
        {
            { "S", new List<string> { "program", "var", "O", "begin", "L", "end", "." } },
            { "O", new List<string> { "dim", "I", "%", "|", "dim", "I", "!", "|", "dim", "I", "$" } },
            { "I", new List<string> { "id", "|", "I", ",", "id" } },
            { "L", new List<string> { "L1", "|", "L", ";", "L1", "|", "L1", ";", "|", "begin",
                "L", "end", "|", "L", ";", "begin", "L", "end", "|", "if",
                "E", "then", "L", "else", "L", "endif", "|", "L", ";", "if",
                "E", "then", "L", "else", "L", "endif", "|", "for", "F", ";",
                "E", ")", "begin", "L", "end", "|", "L", ";", "for", "F", ";",
                "E", ")", "begin", "L", "end", "|", "repeat", "L", "until", "(",
                "E", ")", "|", "L", ";", "repeat", "L", "until", "(", "E", ")",
                "|", "read", "(", "I", ")", "|", "L", ";", "read", "(", "I", ")",
                "|", "output", "(", "I", ")", "|", "L", ";", "output", "(", "I", ")" } },
            { "L1", new List<string> { "id", "ass", "E" } },
            { "F", new List<string> { "(", "E", ";", "E" } },
            { "E", new List<string> { "X" } },
            { "X", new List<string> { "Y", "and", "Y", "|", "Y", "or", "Y" } },
            { "Y", new List<string> { "Z", "=", "Z", "|", "Z", "!=", "Z", "|", "Z", "<", "Z", "|",
                "Z", ">", "Z", "|", "Z", "<=", "Z", "|", "Z", ">=", "Z" } },
            { "Z", new List<string> { "W", "|", "Z", "+", "W", "|", "Z", "-", "W" } },
            { "W", new List<string> { "V", "|", "W", "*", "V", "|", "W", "/", "V" } },
            { "V", new List<string> { "id", "|", "num", "|", "bool", "|", "(", "E", ")", "|", "not", "V" } }
        };
    }

    public class SyntaxException : Exception
    {
        public SyntaxException() { }

        public SyntaxException(string message) : base(message) { }

        public SyntaxException(string message, Exception inner) : base(message, inner) { }
    }
}
