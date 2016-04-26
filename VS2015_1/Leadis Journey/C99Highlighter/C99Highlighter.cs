using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leadis_Journey
{
    public sealed partial class C99Highlighter : IHighlighter
    {
        private struct TextBlock
        {
            public enum Tag
            {
                Insert,
                Remove,
                Set,
            }

            public Tag Action;
            public string Text;
            public int Where;

            public TextBlock(Tag action, string text, int where)
            {
                this.Action = action;
                this.Text = text;
                this.Where = where;
            }

            public static TextBlock Insert(string text, int where)
            {
                return new TextBlock(Tag.Insert, text, where);
            }

            public static TextBlock Remove(int len, int where)
            {
                return new TextBlock(Tag.Remove, new string('\0', len), where);
            }

            public static TextBlock Set(string text)
            {
                return new TextBlock(Tag.Set, text, 0);
            }
        }

        private sealed class ParserTask : IDisposable
        {
            private const int WAIT_FOR_BLOCK = 100;
            private static readonly HashSet<string> KEYWORDS = new HashSet<string>()
            {
                "auto",         "break",        "case",         "char",         "const",        "continue",
                "default",      "do",           "double",       "else",         "enum",         "extern",
                "float",        "for",          "goto",         "if",           "int",          "long",
                "register",     "return",       "short",        "signed",       "sizeof",       "static",
                "struct",       "switch",       "typedef",      "union",        "unsigned",     "void",
                "volatile",     "while",        "_Bool",        "_Complex",     "_Imaginary",   "inline",
                "restrict",
            };
            private HashSet<string> macros;
            private ConcurrentQueue<TextBlock> inqueue;
            private ConcurrentQueue<Token> outqueue;
            private CancellationTokenSource taskToken;
            private Task task;
            private StateString<IToken, NoneToken> text;

            public Action Start;

            public ParserTask(ConcurrentQueue<TextBlock> inqueue, ConcurrentQueue<Token> outqueue)
            {
                this.macros = new HashSet<string>();
                this.inqueue = inqueue;
                this.outqueue = outqueue;
                this.taskToken = new CancellationTokenSource();
                this.task = new Task(this.Loop, this.taskToken.Token);
                this.text = new StateString<IToken, NoneToken>();
                this.Start = task.Start;
            }

            private void Emit(int begin, int end, TokenType type)
            {
                this.outqueue.Enqueue(new Token(begin, end, type));
            }

            private void Parse(int idx = 0)
            {
                if (this.text.Length <= 0)
                    return;
                Pstate cstate = this.text.States[idx].State;
                --idx;
                string id = null;
                char cchar;
                int start = 0;
                goto jumptable;

            jumptable:
                switch (cstate)
                {
                case Pstate.None: goto none;
                case Pstate.NoneNoPP: goto noneNoPP;
                case Pstate.JumpToDefineAE: goto ppDefineAfterIdentifier;
                case Pstate.JumpToUndefAE: goto ppUndefAfterIdentifier;
                }

            none:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (Char.IsWhiteSpace(cchar))
                    goto none;
                if (cchar == '#')
                    goto ppStart;
                --idx;
                goto noneNoPP;

            noneNoPP:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\r')
                    goto none;
                if (cchar == '\'')
                    goto litCharStart;
                if (cchar == '"')
                    goto litStringStart;
                if (Char.IsDigit(cchar))
                    goto litNumberStart;
                if (Char.IsWhiteSpace(cchar))
                    goto noneNoPP;
                if (Char.IsLetter(cchar) || cchar == '_' || cchar == '$')
                    goto identifierStart;
                goto noneNoPP;

            #region Directives parsing
            ppStart:
                start = idx;
                goto ppCont;

            ppCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\r')
                    goto none;
                if (Char.IsWhiteSpace(cchar))
                    goto ppCont;
                if ((idx + 6 < this.text.Chars.Length) && (this.text.Chars.Substring(idx, 6) == "define"))
                {
                    idx += 6;
                    goto ppDefineStart;
                }
                if ((idx + 5 < this.text.Chars.Length) && (this.text.Chars.Substring(idx, 5) == "undef"))
                {
                    idx += 5;
                    goto ppUndefStart;
                }
                goto ppStart;

            #region Define parsing
            ppDefineStart:
                this.Emit(start, idx, TokenType.Directive);
                goto ppDefineBeforeIdentifier;

            ppDefineBeforeIdentifier:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\r')
                    goto none;
                if (Char.IsWhiteSpace(cchar))
                    goto ppDefineBeforeIdentifier;
                cstate = Pstate.JumpToDefineAE;
                goto identifierStart;

            ppDefineAfterIdentifier:
                cstate = Pstate.None;
                this.Emit(start, idx + 1, TokenType.Macro);
                this.macros.Add(id);
                --idx;
                goto noneNoPP;
            #endregion

            #region Undef parsing
            ppUndefStart:
                this.Emit(start, idx, TokenType.Directive);
                goto ppUndefBeforeIdentifier;

            ppUndefBeforeIdentifier:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\r')
                    goto none;
                if (Char.IsWhiteSpace(cchar))
                    goto ppUndefBeforeIdentifier;
                cstate = Pstate.JumpToUndefAE;
                goto identifierStart;

            ppUndefAfterIdentifier:
                cstate = Pstate.None;
                this.Emit(start, idx + 1, TokenType.Macro);
                this.macros.Remove(id);
                --idx;
                goto noneNoPP;
            #endregion
            #endregion

            #region Char parsing
            litCharStart:
                start = idx;
                goto litCharCont;

            litCharCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\'')
                {
                    this.Emit(start, idx + 1, TokenType.Error);
                    goto noneNoPP;
                }
                if (cchar == '\\')
                    goto litCharEscaped;
                goto litCharEnd;

            litCharEscaped:
                if (++idx >= this.text.Length)
                    return;
                switch (this.text.Chars[idx])
                {
                case 'a': goto litCharEnd;
                case 'b': goto litCharEnd;
                case 'f': goto litCharEnd;
                case 'n': goto litCharEnd;
                case 'r': goto litCharEnd;
                case 't': goto litCharEnd;
                case 'v': goto litCharEnd;
                case '\\': goto litCharEnd;
                case '\'': goto litCharEnd;
                case '\r': goto litCharEnd;
                case '"': goto litCharEnd;
                case '?': goto litCharEnd;
                case 'x': goto litCharEscapedXStart;
                case '0': goto litCharEscapedO;
                case '1': goto litCharEscapedO;
                case '2': goto litCharEscapedO;
                case '3': goto litCharEscapedO;
                case '4': goto litCharEscapedO;
                case '5': goto litCharEscapedO;
                case '6': goto litCharEscapedO;
                case '7': goto litCharEscapedO;
                case '8': goto litCharEscapedO;
                case '9': goto litCharEscapedO;
                default:
                    this.Emit(start, idx - 1, TokenType.LitChar);
                    this.Emit(idx - 1, idx + 1, TokenType.Error);
                    start = idx + 1;
                    goto litCharEnd;
                }

            litCharEscapedXStart:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (Char.IsDigit(cchar) || (cchar >= 'A' && cchar <= 'F') || (cchar >= 'a' && cchar <= 'f'))
                    goto litCharEscapedXCont;
                this.Emit(start, idx - 1, TokenType.LitChar);
                this.Emit(idx - 1, idx + 1, TokenType.Error);
                start = idx + 1;
                goto litCharEnd;

            litCharEscapedXCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (Char.IsDigit(cchar) || (cchar >= 'A' && cchar <= 'F') || (cchar >= 'a' && cchar <= 'f'))
                    goto litCharEscapedXCont;
                --idx;
                goto litCharEnd;

            litCharEscapedO:
                goto litCharEnd;

            litCharEnd:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '\'')
                    this.Emit(start, idx + 1, TokenType.LitChar);
                else
                    this.Emit(start, idx + 1, TokenType.Error);
                goto noneNoPP;
            #endregion Char parsing

            #region String parsing
            litStringStart:
                start = idx + 1;
                this.Emit(idx, start, TokenType.LitString);
                goto litStringCont;

            litStringCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (cchar == '"')
                {
                    this.Emit(start, idx + 1, TokenType.LitString);
                    goto noneNoPP;
                }
                if (cchar == '\r')
                {
                    this.Emit(start, idx - 1, TokenType.LitString);
                    this.Emit(idx - 1, idx, TokenType.Error);
                    goto none;
                }
                if (cchar == '\\')
                    goto litStringEscaped;
                goto litStringCont;

            litStringEscaped:
                if (++idx >= this.text.Length)
                    return;
                switch (this.text.Chars[idx])
                {
                case 'a': goto litStringCont;
                case 'b': goto litStringCont;
                case 'f': goto litStringCont;
                case 'n': goto litStringCont;
                case 'r': goto litStringCont;
                case 't': goto litStringCont;
                case 'v': goto litStringCont;
                case '\\': goto litStringCont;
                case '\'': goto litStringCont;
                case '\r': goto litStringCont;
                case '"': goto litStringCont;
                case '?': goto litStringCont;
                case 'x': goto litStringEscapedXStart;
                case '0': goto litStringEscapedO;
                case '1': goto litStringEscapedO;
                case '2': goto litStringEscapedO;
                case '3': goto litStringEscapedO;
                case '4': goto litStringEscapedO;
                case '5': goto litStringEscapedO;
                case '6': goto litStringEscapedO;
                case '7': goto litStringEscapedO;
                case '8': goto litStringEscapedO;
                case '9': goto litStringEscapedO;
                default:
                    this.Emit(start, idx - 1, TokenType.LitString);
                    this.Emit(idx - 1, idx + 1, TokenType.Error);
                    start = idx + 1;
                    goto litStringCont;
                }

            litStringEscapedXStart:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (Char.IsDigit(cchar) || (cchar >= 'A' && cchar <= 'F') || (cchar >= 'a' && cchar <= 'f'))
                    goto litStringEscapedXCont;
                this.Emit(start, idx - 1, TokenType.LitString);
                this.Emit(idx - 1, idx + 1, TokenType.Error);
                start = idx + 1;
                goto litStringCont;

            litStringEscapedXCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (Char.IsDigit(cchar) || (cchar >= 'A' && cchar <= 'F') || (cchar >= 'a' && cchar <= 'f'))
                    goto litStringEscapedXCont;
                --idx;
                goto litStringCont;

            litStringEscapedO:
                goto litStringCont;
            #endregion

            #region Number parsing
            litNumberStart:
                start = idx + 1;
                this.Emit(idx, start, TokenType.LitNumber);
                goto litDecNumberCont;

            litDecNumberCont:
                if (++idx >= this.text.Length)
                    return;
                cchar = this.text.Chars[idx];
                if (!Char.IsDigit(cchar))
                {
                    this.Emit(start, idx, TokenType.LitNumber);
                    --idx;
                    goto noneNoPP;
                }
                goto litDecNumberCont;
            #endregion

            #region Identifier parsing
            identifierStart:
                start = idx;
                goto identifierCont;

            identifierCont:
                if (++idx >= this.text.Length)
                    goto identifierEnd;
                cchar = this.text.Chars[idx];
                if (Char.IsLetterOrDigit(cchar) || cchar == '_' || cchar == '$')
                    goto identifierCont;
                --idx;
                goto identifierEnd;

            identifierEnd:
                ++idx;
                id = this.text.Chars.Substring(start, idx - start);
                if (cstate != Pstate.None)
                    goto jumptable;
                cchar = this.text.Chars[idx];
                if (this.macros.Contains(id))
                    this.Emit(start, idx, TokenType.Macro);
                else if (KEYWORDS.Contains(id))
                    this.Emit(start, idx, TokenType.Keyword);
                else
                    this.Emit(start, idx, TokenType.Error);
                --idx;
                if (cchar == '\r')
                    goto none;
                goto noneNoPP;
                #endregion
            }

            async private void Loop()
            {
                TextBlock block;
                while (!this.taskToken.IsCancellationRequested)
                {
                    while (!this.inqueue.TryDequeue(out block))
                        await Task.Delay(WAIT_FOR_BLOCK);
                    switch (block.Action)
                    {
                    case TextBlock.Tag.Insert:
                        text.Insert(block.Where, block.Text);
                        this.Parse(block.Where);
                        break;
                    case TextBlock.Tag.Remove:
                        text.Remove(block.Where, block.Text.Length);
                        this.Parse();
                        break;
                    case TextBlock.Tag.Set:
                        this.macros.Clear();
                        text.Remove(0, text.Length);
                        text.Insert(0, block.Text);
                        this.Parse();
                        break;
                    }
                }
            }

            public void Dispose()
            {
                this.taskToken.Cancel();
                this.task.Wait();
                this.taskToken.Dispose();
            }
        }

        private ConcurrentQueue<TextBlock> outqueue;
        private ConcurrentQueue<Token> inqueue;
        private ParserTask parserTask;

        public C99Highlighter()
        {
            this.outqueue = new ConcurrentQueue<TextBlock>();
            this.inqueue = new ConcurrentQueue<Token>();
            this.parserTask = new ParserTask(this.outqueue, this.inqueue);
            this.parserTask.Start();
        }

        public void RemoveText(int where, int len)
        {
            this.outqueue.Enqueue(TextBlock.Remove(len, where));
        }

        public void InsertText(int where, string text)
        {
            this.outqueue.Enqueue(TextBlock.Insert(text, where));
        }

        public void SetText(string text)
        {
            this.outqueue.Enqueue(TextBlock.Set(text));
        }

        public bool TryGetToken(out Token result)
        {
            return this.inqueue.TryDequeue(out result);
        }

        public void Dispose()
        {
            this.parserTask.Dispose();
        }
    }
}
