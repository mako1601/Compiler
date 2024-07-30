using System;
using System.Text;
using System.Collections.Generic;

using static Compiler.Semantic;

namespace Compiler
{
    // NASM and GCC
    public static class Assembly
    {
        private static RPN _rpn = new();

        public static string GetAssemblyCode(RPN rpn, Semantic semantic)
        {
            _rpn = rpn;

            // entry and data
            int dataType = 0;
            var asmCode = new StringBuilder("global main\n\nsection .data\n");

            foreach (var id in semantic.IdTable)
            {
                string key = id.Key;
                IdData data = id.Value;

                asmCode.Append(key);
                if (data.Type is TokenType.Percent)
                {
                    asmCode.AppendLine(" dq ?");
                    continue;
                }
                else if (data.Type is TokenType.Exclamation)
                {
                    asmCode.AppendLine(" dq ?");
                    dataType = 1;
                    continue;
                }
                else
                {
                    asmCode.AppendLine(" db ?");
                    dataType = -1;
                    continue;
                }
            }

            // code
            asmCode.AppendLine("\nsection .text\nmain:");

            Dictionary<string, string> inputFormats  = [];
            Dictionary<string, string> outputFormats = [];
            bool insertPrintf = false;
            bool insertScanf  = false;
            int outputCount = 0;
            int inputCount  = 0;

            if (dataType < 1)
            {
                for (int index = 0; index < _rpn.PostfixNotation.Count; index++)
                {
                    while (_rpn.PostfixNotation[index].Type is not TokenType.Label
                        && _rpn.PostfixNotation[index].Type is not TokenType.LabelInfo
                        && _rpn.PostfixNotation[index].Type is not TokenType.Ass
                        && _rpn.PostfixNotation[index].Type is not TokenType.Read
                        && _rpn.PostfixNotation[index].Type is not TokenType.Output
                        && _rpn.PostfixNotation[index].IsLogicalOperation() is false
                        && _rpn.PostfixNotation[index].IsArithmeticOperation() is false)
                    index++;

                    if (_rpn.PostfixNotation[index].Type == TokenType.Label)
                    {
                        asmCode.AppendLine($"{_rpn.PostfixNotation[index].Value}:");
                        _rpn.PostfixNotation.RemoveAt(index--);
                    }
                    else if (_rpn.PostfixNotation[index].IsArithmeticOperation())
                    {
                        if (_rpn.PostfixNotation[index - 2].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov rax, qword [rel {_rpn.PostfixNotation[index - 2].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmov rax, {_rpn.PostfixNotation[index - 2].Value}");
                        }

                        string command = ConvertToAssemblyCommand(_rpn.PostfixNotation[index]);
                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            if (command.Equals("idiv"))
                            {
                                asmCode.AppendLine($"\tcqo");
                                asmCode.AppendLine($"\tmov rcx, qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                                asmCode.AppendLine($"\t{command} rcx");
                            }
                            else
                            {
                                asmCode.AppendLine($"\t{command} rax, qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                            }
                        }
                        else
                        {
                            if (command.Equals("idiv"))
                            {
                                asmCode.AppendLine($"\tcqo");
                                asmCode.AppendLine($"\tmov rcx, {_rpn.PostfixNotation[index - 1].Value}");
                                asmCode.AppendLine($"\t{command} rcx");
                            }
                            else
                            {
                                asmCode.AppendLine($"\t{command} rax, {_rpn.PostfixNotation[index - 1].Value}");
                            }
                        }

                        _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                        index -= 3;
                        WhatNext(ref asmCode, ref index);
                    }
                    else if (_rpn.PostfixNotation[index].IsLogicalOperation())
                    {
                        if (_rpn.PostfixNotation[index - 2].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov rax, qword [rel {_rpn.PostfixNotation[index - 2].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmov rax, {_rpn.PostfixNotation[index - 2].Value}");
                        }

                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tcmp rax, qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tcmp rax, {_rpn.PostfixNotation[index - 1].Value}");
                        }

                        if (_rpn.PostfixNotation[index + 1].Type is TokenType.LabelInfo)
                        {
                            _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[index + 1].Line), out var label);
                            if (_rpn.PostfixNotation[index + 1].Value.Equals("false"))
                            {
                                string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], false);
                                asmCode.AppendLine($"\t{command} {label?.Value}");
                                _rpn.PostfixNotation.RemoveRange(index - 2, 4);
                                index -= 3;
                            }
                            else
                            {
                                string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                asmCode.AppendLine($"\t{command} {label?.Value}");
                                _rpn.PostfixNotation.RemoveRange(index - 2, 4);
                                index -= 3;
                            }
                        }
                        else if (_rpn.PostfixNotation[index + 1].IsIdentifierOrNumberOrBool())
                        {
                            int AndOrPos = index + 2;
                            while (_rpn.PostfixNotation[AndOrPos].Type is not TokenType.And
                                && _rpn.PostfixNotation[AndOrPos].Type is not TokenType.Or)
                            {
                                AndOrPos++;
                            }

                            int LabelInfoPos = index + 2;
                            while (_rpn.PostfixNotation[LabelInfoPos].Type is not TokenType.LabelInfo)
                            {
                                LabelInfoPos++;
                            }

                            if (_rpn.PostfixNotation[AndOrPos].Type is TokenType.And)
                            {
                                if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("false"))
                                {
                                    _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[LabelInfoPos].Line), out var label);
                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], false);
                                    asmCode.AppendLine($"\t{command} {label?.Value}");
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("true"))
                                {
                                    int labelCount = 0;
                                    foreach (var item in _rpn.LabelTable)
                                    {
                                        Token value = item.Value;
                                        if (value.Type is TokenType.Label && value.Value.Equals($"L{labelCount}"))
                                        {
                                            labelCount++;
                                        }
                                    }

                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} L{labelCount}");
                                    _rpn.PostfixNotation.Insert(LabelInfoPos + 1, new Token(TokenType.Label, $"L{labelCount}"));
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    ; // ???
                                }
                            }
                            else if (_rpn.PostfixNotation[AndOrPos].Type is TokenType.Or)
                            {
                                if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("true"))
                                {
                                    _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[LabelInfoPos].Line), out var label);
                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} {label?.Value}");
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("false"))
                                {
                                    int labelCount = 0;
                                    foreach (var item in _rpn.LabelTable)
                                    {
                                        Token value = item.Value;
                                        if (value.Type is TokenType.Label && value.Value.Equals($"L{labelCount}"))
                                        {
                                            labelCount++;
                                        }
                                    }

                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} L{labelCount}");
                                    _rpn.PostfixNotation.Insert(LabelInfoPos + 1, new Token(TokenType.Label, $"L{labelCount}"));
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    ; // ???
                                }
                            }
                            else
                            {
                                ; // ???
                            }
                        }
                        else
                        {
                            ; // ???
                        }
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.LabelInfo)
                    {
                        _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[index].Line), out var label);

                        // только если условие без какого-либо знака сравнения
                        if (_rpn.PostfixNotation[index].Value.Equals("false"))
                        {
                            if (index - 1 > -1)
                            {
                                if (_rpn.PostfixNotation[index - 1].Type is TokenType.Not)
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 2].Value}], 0");
                                    asmCode.AppendLine($"\tjne {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 1].Value}], 0");
                                    asmCode.AppendLine($"\tje {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 1, 2);
                                    index -= 2;
                                }
                            }
                            else
                            {
                                _rpn.PostfixNotation.RemoveAt(index--);
                            }
                        }
                        // только если БП
                        else if (_rpn.PostfixNotation[index].Value.Equals(string.Empty))
                        {
                            asmCode.AppendLine($"\tjmp {label?.Value}");
                            _rpn.PostfixNotation.RemoveAt(index--);
                        }
                        // только если условие без какого-либо знака сравнения
                        else
                        {
                            if (index - 1 > -1)
                            {
                                if (_rpn.PostfixNotation[index - 1].Type is TokenType.Not)
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 2].Value}], 0");
                                    asmCode.AppendLine($"\tje {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 1].Value}], 0");
                                    asmCode.AppendLine($"\tjne {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 1, 2);
                                    index -= 2;
                                }
                            }
                            else
                            {
                                asmCode.AppendLine($"\tjmp {label?.Value}");

                                int matches = 0;
                                foreach (var item in _rpn.LabelTable)
                                {
                                    string key = item.Key;
                                    Token value = item.Value;

                                    if (_rpn.PostfixNotation[index + 1].Value != value.Value)
                                    {
                                        continue;
                                    }

                                    foreach (var _item in _rpn.LabelTable)
                                    {
                                        Token _value = _item.Value;

                                        if (Convert.ToInt32(key) == _value.Line)
                                        {
                                            matches++;
                                        }

                                        if (matches > 2)
                                        {
                                            break;
                                        }
                                    }
                                    break;
                                }

                                if (matches < 2)
                                {
                                    _rpn.PostfixNotation.RemoveRange(index, 2);
                                    index--;
                                }
                                else
                                {
                                    _rpn.PostfixNotation.RemoveAt(index--);
                                }
                            }
                        }
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Ass)
                    {
                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov qword [rel {_rpn.PostfixNotation[index - 2].Value}], qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmov qword [rel {_rpn.PostfixNotation[index - 2].Value}], {_rpn.PostfixNotation[index - 1].Value}");
                        }
                        _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                        index -= 3;
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Read)
                    {
                        insertScanf = true;

                        // input format creation
                        string format = string.Empty;
                        for (int j = 0; _rpn.PostfixNotation[j].Type is not TokenType.Read; j++)
                        {
                            format += "%d ";
                        }
                        format = format.Remove(format.Length - 1, 1);

                        if (inputFormats.TryGetValue(format, out var value))
                        {
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel {value}]");
                        }
                        else
                        {
                            inputFormats.Add(format, $"formatin{inputCount}");
                            asmCode.Replace("section .data\n", $"section .data\nformatin{inputCount} db \"{format}\", 0\n");
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel formatin{inputCount++}]");
                        }

                        int idCount = 0;
                        asmCode.AppendLine($"\tlea rdx, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tlea r8, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 2);
                            asmCode.AppendLine("\tcall scanf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tlea r9, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 3);
                            asmCode.AppendLine("\tcall sacnf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        int shift = 32;
                        while (_rpn.PostfixNotation[idCount].Type is not TokenType.Read)
                        {
                            asmCode.AppendLine($"\tlea r10, [rel {_rpn.PostfixNotation[idCount++].Value}]\n\tmov qword [rsp+{shift}], r10");
                            shift += 8;
                        }
                        asmCode.AppendLine("\tcall scanf\n\tadd rsp, 40");
                        _rpn.PostfixNotation.RemoveRange(0, 4 + (shift - 32) / 8);
                        index = -1;

                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Output)
                    {
                        insertPrintf = true;

                        string format = string.Empty;
                        for (int i = 0; _rpn.PostfixNotation[i].Type is not TokenType.Output; i++)
                        {
                            format += "%d ";
                        }
                        format = format.Remove(format.Length - 1, 1);

                        if (outputFormats.TryGetValue(format, out var value))
                        {
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel {value}]");
                        }
                        else
                        {
                            outputFormats.Add(format, $"formatin{inputCount}");
                            asmCode.Replace("section .data\n", $"section .data\nformatout{outputCount} db \"{format}\", 10, 0\n");
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel formatout{outputCount++}]");
                        }

                        int idCount = 0;
                        asmCode.AppendLine($"\tmov rdx, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov r8, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 2);
                            asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov r9, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 3);
                            asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        int shift = 32;
                        while (_rpn.PostfixNotation[idCount].Type is not TokenType.Output)
                        {
                            asmCode.AppendLine($"\tmov r10, [rel {_rpn.PostfixNotation[idCount++].Value}]\n\tmov qword [rsp+{shift}], r10");
                            shift += 8;
                        }
                        asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                        _rpn.PostfixNotation.RemoveRange(0, 4 + (shift - 32) / 8);
                        index = -1;
                    }
                    else
                    {
                        ; // ???
                    }
                }

                if (insertScanf is true)
                {
                    asmCode.Insert(13, "extern scanf\n");
                }
                if (insertPrintf is true)
                {
                    asmCode.Insert(13, "extern printf\n");
                }
                if (insertScanf is true || insertPrintf is true)
                {
                    asmCode.Replace("section .data\n", "\nsection .data\n");
                }

                asmCode.AppendLine("\tret");

                return asmCode.ToString();
            }
            else
            {
                for (int index = 0; index < _rpn.PostfixNotation.Count; index++)
                {
                    while (_rpn.PostfixNotation[index].Type is not TokenType.Label
                        && _rpn.PostfixNotation[index].Type is not TokenType.LabelInfo
                        && _rpn.PostfixNotation[index].Type is not TokenType.Ass
                        && _rpn.PostfixNotation[index].Type is not TokenType.Read
                        && _rpn.PostfixNotation[index].Type is not TokenType.Output
                        && _rpn.PostfixNotation[index].IsLogicalOperation() is false
                        && _rpn.PostfixNotation[index].IsArithmeticOperation() is false)
                    index++;

                    if (_rpn.PostfixNotation[index].Type is TokenType.Label)
                    {
                        asmCode.AppendLine($"{_rpn.PostfixNotation[index].Value}:");
                        _rpn.PostfixNotation.RemoveAt(index--);
                    }
                    else if (_rpn.PostfixNotation[index].IsArithmeticOperation())
                    {
                        if (_rpn.PostfixNotation[index - 2].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmovsd xmm0, qword [rel {_rpn.PostfixNotation[index - 2].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmovsd xmm0, {_rpn.PostfixNotation[index - 2].Value}");
                        }

                        string command = ConvertToAssemblyCommand1(_rpn.PostfixNotation[index]);
                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\t{command} xmm0, qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\t{command} xmm0, {_rpn.PostfixNotation[index - 1].Value}");
                        }

                        _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                        index -= 3;
                        WhatNext1(ref asmCode, ref index);
                    }
                    else if (_rpn.PostfixNotation[index].IsLogicalOperation())
                    {
                        if (_rpn.PostfixNotation[index - 2].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmovsd xmm0, qword [rel {_rpn.PostfixNotation[index - 2].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmovsd xmm0, {_rpn.PostfixNotation[index - 2].Value}");
                        }

                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tcmp xmm0, qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tcmp xmm0, {_rpn.PostfixNotation[index - 1].Value}");
                        }

                        if (_rpn.PostfixNotation[index + 1].Type is TokenType.LabelInfo)
                        {
                            _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[index + 1].Line), out var label);
                            if (_rpn.PostfixNotation[index + 1].Value.Equals("false"))
                            {
                                string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], false);
                                asmCode.AppendLine($"\t{command} {label?.Value}");
                                _rpn.PostfixNotation.RemoveRange(index - 2, 4);
                                index -= 3;
                            }
                            else
                            {
                                string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                asmCode.AppendLine($"\t{command} {label?.Value}");
                                _rpn.PostfixNotation.RemoveRange(index - 2, 4);
                                index -= 3;
                            }
                        }
                        else if (_rpn.PostfixNotation[index + 1].IsIdentifierOrNumberOrBool())
                        {
                            int AndOrPos = index + 2;
                            while (_rpn.PostfixNotation[AndOrPos].Type is not TokenType.And
                                && _rpn.PostfixNotation[AndOrPos].Type is not TokenType.Or)
                            {
                                AndOrPos++;
                            }

                            int LabelInfoPos = index + 2;
                            while (_rpn.PostfixNotation[LabelInfoPos].Type is not TokenType.LabelInfo)
                            {
                                LabelInfoPos++;
                            }

                            if (_rpn.PostfixNotation[AndOrPos].Type is TokenType.And)
                            {
                                if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("false"))
                                {
                                    _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[LabelInfoPos].Line), out var label);
                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], false);
                                    asmCode.AppendLine($"\t{command} {label?.Value}");
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("true"))
                                {
                                    int labelCount = 0;
                                    foreach (var item in _rpn.LabelTable)
                                    {
                                        Token value = item.Value;
                                        if (value.Type is TokenType.Label && value.Value.Equals($"L{labelCount}"))
                                        {
                                            labelCount++;
                                        }
                                    }

                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} L{labelCount}");
                                    _rpn.PostfixNotation.Insert(LabelInfoPos + 1, new Token(TokenType.Label, $"L{labelCount}"));
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    ; // ???
                                }
                            }
                            else if (_rpn.PostfixNotation[AndOrPos].Type is TokenType.Or)
                            {
                                if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("true"))
                                {
                                    _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[LabelInfoPos].Line), out var label);
                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} {label?.Value}");
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else if (_rpn.PostfixNotation[LabelInfoPos].Value.Equals("false"))
                                {
                                    int labelCount = 0;
                                    foreach (var item in _rpn.LabelTable)
                                    {
                                        Token value = item.Value;
                                        if (value.Type is TokenType.Label && value.Value.Equals($"L{labelCount}"))
                                        {
                                            labelCount++;
                                        }
                                    }

                                    string command = ConvertConditionToAssemblyCommand(_rpn.PostfixNotation[index], true);
                                    asmCode.AppendLine($"\t{command} L{labelCount}");
                                    _rpn.PostfixNotation.Insert(LabelInfoPos + 1, new Token(TokenType.Label, $"L{labelCount}"));
                                    _rpn.PostfixNotation.RemoveAt(AndOrPos);
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    ; // ???
                                }
                            }
                            else
                            {
                                ; // ???
                            }
                        }
                        else
                        {
                            ; // ???
                        }
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.LabelInfo)
                    {
                        _rpn.LabelTable.TryGetValue(Convert.ToString(_rpn.PostfixNotation[index].Line), out var label);

                        // только если условие без какого-либо знака сравнения
                        if (_rpn.PostfixNotation[index].Value.Equals("false"))
                        {
                            if (index - 1 > -1)
                            {
                                if (_rpn.PostfixNotation[index - 1].Type is TokenType.Not)
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 2].Value}], 0");
                                    asmCode.AppendLine($"\tjne {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 1].Value}], 0");
                                    asmCode.AppendLine($"\tje {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 1, 2);
                                    index -= 2;
                                }
                            }
                            else
                            {
                                _rpn.PostfixNotation.RemoveAt(index--);
                            }
                        }
                        // только если БП
                        else if (_rpn.PostfixNotation[index].Value.Equals(string.Empty))
                        {
                            asmCode.AppendLine($"\tjmp {label?.Value}");
                            _rpn.PostfixNotation.RemoveAt(index--);
                        }
                        // только если условие без какого-либо знака сравнения
                        else
                        {
                            if (index - 1 > -1)
                            {
                                if (_rpn.PostfixNotation[index - 1].Type is TokenType.Not)
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 2].Value}], 0");
                                    asmCode.AppendLine($"\tje {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                                    index -= 3;
                                }
                                else
                                {
                                    asmCode.AppendLine($"\tcmp qword [rel {_rpn.PostfixNotation[index - 1].Value}], 0");
                                    asmCode.AppendLine($"\tjne {label?.Value}");
                                    _rpn.PostfixNotation.RemoveRange(index - 1, 2);
                                    index -= 2;
                                }
                            }
                            else
                            {
                                asmCode.AppendLine($"\tjmp {label?.Value}");

                                int matches = 0;
                                foreach (var item in _rpn.LabelTable)
                                {
                                    string key = item.Key;
                                    Token value = item.Value;

                                    if (_rpn.PostfixNotation[index + 1].Value != value.Value)
                                    {
                                        continue;
                                    }

                                    foreach (var _item in _rpn.LabelTable)
                                    {
                                        Token _value = _item.Value;

                                        if (Convert.ToInt32(key) == _value.Line)
                                        {
                                            matches++;
                                        }

                                        if (matches > 2)
                                        {
                                            break;
                                        }
                                    }
                                    break;
                                }

                                if (matches < 2)
                                {
                                    _rpn.PostfixNotation.RemoveRange(index, 2);
                                    index--;
                                }
                                else
                                {
                                    _rpn.PostfixNotation.RemoveAt(index--);
                                }
                            }
                        }
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Ass)
                    {
                        if (_rpn.PostfixNotation[index - 1].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmovsd qword [rel {_rpn.PostfixNotation[index - 2].Value}], qword [rel {_rpn.PostfixNotation[index - 1].Value}]");
                        }
                        else
                        {
                            asmCode.AppendLine($"\tmovsd qword [rel {_rpn.PostfixNotation[index - 2].Value}], {_rpn.PostfixNotation[index - 1].Value}");
                        }
                        _rpn.PostfixNotation.RemoveRange(index - 2, 3);
                        index -= 3;
                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Read)
                    {
                        insertScanf = true;

                        // input format creation
                        string format = string.Empty;
                        for (int j = 0; _rpn.PostfixNotation[j].Type is not TokenType.Read; j++)
                        {
                            format += "%.2f ";
                        }
                        format = format.Remove(format.Length - 1, 1);

                        if (inputFormats.TryGetValue(format, out var value))
                        {
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel {value}]");
                        }
                        else
                        {
                            inputFormats.Add(format, $"formatin{inputCount}");
                            asmCode.Replace("section .data\n", $"section .data\nformatin{inputCount} db \"{format}\", 0\n");
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel formatin{inputCount++}]");
                        }

                        int idCount = 0;
                        asmCode.AppendLine($"\tlea rdx, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tlea r8, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 2);
                            asmCode.AppendLine("\tcall scanf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tlea r9, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 3);
                            asmCode.AppendLine("\tcall sacnf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        int shift = 32;
                        while (_rpn.PostfixNotation[idCount].Type is not TokenType.Read)
                        {
                            asmCode.AppendLine($"\tlea r10, [rel {_rpn.PostfixNotation[idCount++].Value}]\n\tmov qword [rsp+{shift}], r10");
                            shift += 8;
                        }
                        asmCode.AppendLine("\tcall scanf\n\tadd rsp, 40");
                        _rpn.PostfixNotation.RemoveRange(0, 4 + (shift - 32) / 8);
                        index = -1;

                    }
                    else if (_rpn.PostfixNotation[index].Type is TokenType.Output)
                    {
                        insertPrintf = true;

                        string format = string.Empty;
                        for (int i = 0; _rpn.PostfixNotation[i].Type is not TokenType.Output; i++)
                        {
                            format += "%.2f ";
                        }
                        format = format.Remove(format.Length - 1, 1);

                        if (outputFormats.TryGetValue(format, out var value))
                        {
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel {value}]");
                        }
                        else
                        {
                            outputFormats.Add(format, $"formatin{inputCount}");
                            asmCode.Replace("section .data\n", $"section .data\nformatout{outputCount} db \"{format}\", 10, 0\n");
                            asmCode.AppendLine($"\tsub rsp, 40\n\tlea rcx, [rel formatout{outputCount++}]");
                        }

                        int idCount = 0;
                        asmCode.AppendLine($"\tmov rdx, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov r8, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 2);
                            asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        if (_rpn.PostfixNotation[idCount].Type is TokenType.Identifier)
                        {
                            asmCode.AppendLine($"\tmov r9, [rel {_rpn.PostfixNotation[idCount++].Value}]");
                        }
                        else
                        {
                            _rpn.PostfixNotation.RemoveRange(0, 3);
                            asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                            index = -1;
                            continue;
                        }

                        int shift = 32;
                        while (_rpn.PostfixNotation[idCount].Type is not TokenType.Output)
                        {
                            asmCode.AppendLine($"\tmov r10, [rel {_rpn.PostfixNotation[idCount++].Value}]\n\tmov qword [rsp+{shift}], r10");
                            shift += 8;
                        }
                        asmCode.AppendLine("\tcall printf\n\tadd rsp, 40");
                        _rpn.PostfixNotation.RemoveRange(0, 4 + (shift - 32) / 8);
                        index = -1;
                    }
                    else
                    {
                        ; // ???
                    }
                }

                if (insertScanf is true)
                {
                    asmCode.Insert(13, "extern scanf\n");
                }
                if (insertPrintf is true)
                {
                    asmCode.Insert(13, "extern printf\n");
                }
                if (insertScanf is true || insertPrintf is true)
                {
                    asmCode.Replace("section .data\n", "\nsection .data\n");
                }

                asmCode.AppendLine("\tret");

                return asmCode.ToString();
            }
        }

        private static string ConvertToAssemblyCommand(Token token) => token.Type switch
        {
            TokenType.Plus     => "add",
            TokenType.Minus    => "sub",
            TokenType.Multiply => "imul",
            TokenType.Divide   => "idiv",

            _ => "mov"
        };

        private static string ConvertToAssemblyCommand1(Token token) => token.Type switch
        {
            TokenType.Plus     => "addsd",
            TokenType.Minus    => "subsd",
            TokenType.Multiply => "mulsd",
            TokenType.Divide   => "divsd",

            _ => "movsd"
        };

        private static string ConvertConditionToAssemblyCommand(Token token, bool condition)
        {
            if (condition is true)
            {
                return token.Type switch
                {
                    TokenType.LogicalEqual   => "je",
                    TokenType.NotEqual       => "jne",
                    TokenType.Less           => "jl",
                    TokenType.LessOrEqual    => "jle",
                    TokenType.Greater        => "jg",
                    TokenType.GreaterOrEqual => "jge",

                    _ => throw new Exception()
                };
            }
            else
            {
                return token.Type switch
                {
                    TokenType.LogicalEqual   => "jne",
                    TokenType.NotEqual       => "je",
                    TokenType.Less           => "jge",
                    TokenType.LessOrEqual    => "jg",
                    TokenType.Greater        => "jle",
                    TokenType.GreaterOrEqual => "jl",

                    _ => throw new Exception()
                };
            }
        }

        private static void WhatNext(ref StringBuilder sb, ref int index)
        {
            if (_rpn.PostfixNotation[index + 1].Type is TokenType.Ass)
            {
                _rpn.PostfixNotation.Insert(++index, new Token(TokenType.Number, "rax"));
            }
            else if (_rpn.PostfixNotation[index + 1].IsIdentifierOrNumberOrBool())
            {
                sb.AppendLine($"\tmov rdx, rax");
                _rpn.PostfixNotation.Insert(++index, new Token(TokenType.Number, "rdx"));
            }
            else if (_rpn.PostfixNotation[index + 1].IsArithmeticOperation())
            {
                string command = ConvertToAssemblyCommand(_rpn.PostfixNotation[index + 1]);
                sb.AppendLine($"\t{command} rax, {_rpn.PostfixNotation[index].Value}");
                _rpn.PostfixNotation.RemoveRange(index, 2);
                index--;
                WhatNext(ref sb, ref index);
            }
            else
            {
                ; // ???
            }
        }

        private static void WhatNext1(ref StringBuilder sb, ref int index)
        {
            if (_rpn.PostfixNotation[index + 1].Type is TokenType.Ass)
            {
                _rpn.PostfixNotation.Insert(++index, new Token(TokenType.Number, "xmm0"));
            }
            else if (_rpn.PostfixNotation[index + 1].IsIdentifierOrNumberOrBool())
            {
                sb.AppendLine($"\tmovsd xmm1, xmm0");
                _rpn.PostfixNotation.Insert(++index, new Token(TokenType.Number, "xmm1"));
            }
            else if (_rpn.PostfixNotation[index + 1].IsArithmeticOperation())
            {
                string command = ConvertToAssemblyCommand1(_rpn.PostfixNotation[index + 1]);
                sb.AppendLine($"\t{command} xmm0, {_rpn.PostfixNotation[index].Value}");
                _rpn.PostfixNotation.RemoveRange(index, 2);
                index--;
                WhatNext1(ref sb, ref index);
            }
            else
            {
                ; // ???
            }
        }
    }
}
