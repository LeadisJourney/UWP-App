using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leadis_Journey
{
    public partial class C99Highlighter : IHighlighter
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
            private static readonly Dictionary<string, Ltype> KEYWORDS = new Dictionary<string, Ltype>()
            #region Dictionary initialisation
            {
                { "auto",       Ltype.KwAuto },
                { "break",      Ltype.KwBreak },
                { "case",       Ltype.KwCase },
                { "char",       Ltype.KwChar },
                { "const",      Ltype.KwConst },
                { "continue",   Ltype.KwContinue },
                { "default",    Ltype.KwDefault },
                { "do",         Ltype.KwDo },
                { "double",     Ltype.KwDouble },
                { "else",       Ltype.KwElse },
                { "enum",       Ltype.KwEnum },
                { "extern",     Ltype.KwExtern },
                { "float",      Ltype.KwFloat },
                { "for",        Ltype.KwFor },
                { "goto",       Ltype.KwGoto },
                { "if",         Ltype.KwIf },
                { "int",        Ltype.KwInt },
                { "long",       Ltype.KwLong },
                { "register",   Ltype.KwRegister },
                { "return",     Ltype.KwReturn },
                { "short",      Ltype.KwShort },
                { "signed",     Ltype.KwSigned },
                { "sizeof",     Ltype.KwSizeof },
                { "static",     Ltype.KwStatic },
                { "struct",     Ltype.KwStruct },
                { "switch",     Ltype.KwSwitch },
                { "typedef",    Ltype.KwTypedef },
                { "union",      Ltype.KwUnion },
                { "unsigned",   Ltype.KwUnsigned },
                { "void",       Ltype.KwVoid },
                { "volatile",   Ltype.KwVolatile },
                { "while",      Ltype.KwWhile },
                { "_Bool",      Ltype.KwBool },
                { "_Complex",   Ltype.KwComplex },
                { "_Imaginary", Ltype.KwImaginary },
                { "inline",     Ltype.KwInline },
                { "restrict",   Ltype.KwRestrict },
            };
            #endregion
            private ConcurrentQueue<TextBlock> inqueue;
            private ConcurrentQueue<Token> outqueue;
            private CancellationTokenSource taskToken;
            private Task task;
            private Stack<ITk> tokens;
            private LinkedList<Token> idsHg;
            private LinkedList<Token> keywordsHg;
            private StateString<ITk, NoneTk> text;

            public Action Start;

            public ParserTask(ConcurrentQueue<TextBlock> inqueue, ConcurrentQueue<Token> outqueue)
            {
                this.inqueue = inqueue;
                this.outqueue = outqueue;
                this.taskToken = new CancellationTokenSource();
                this.task = new Task(this.Loop, this.taskToken.Token);
                this.tokens = new Stack<ITk>();
                this.idsHg = new LinkedList<Token>();
                this.keywordsHg = new LinkedList<Token>();
                this.text = new StateString<ITk, NoneTk>();
                this.Start = task.Start;
            }

            private Lexeme Lex(ref int idx)
            {
                int begin;
                char cchar;
                string str;

            start:
                if (idx >= this.text.Chars.Length)
                    goto eof;
                switch (this.text.Chars[idx])
                {
                case ' ':
                    ++idx;
                    goto start;
                case '\t':
                    ++idx;
                    goto start;
                case '\r':
                    ++idx;
                    goto start;
                case '{':
                    ++idx;
                    return new Lexeme(Ltype.Lacc, null);
                case '}':
                    ++idx;
                    return new Lexeme(Ltype.Racc, null);
                case ';':
                    ++idx;
                    return new Lexeme(Ltype.Semic, null);
                default:
                    cchar = this.text.Chars[idx];
                    if (Char.IsLetter(cchar) || cchar == '_' || cchar == '$')
                        goto identifier;
                    ++idx;
                    return new Lexeme(Ltype.Unknown, this.text.Chars[idx - 1].ToString());
                }

            identifier:
                begin = idx;
                ++idx;
                while (idx < this.text.Chars.Length)
                {
                    cchar = this.text.Chars[idx];
                    if (Char.IsLetterOrDigit(cchar) || cchar == '_' || cchar == '$')
                        ++idx;
                    else
                        break;
                }
                str = this.text.Chars.Substring(begin, idx - begin);
                if (KEYWORDS.ContainsKey(str))
                {
                    this.keywordsHg.AddLast(new Token(begin, idx, TokenType.Keyword));
                    return new Lexeme(KEYWORDS[str], str);
                }
                return new Lexeme(Ltype.Identifier, str);

            eof:
                return new Lexeme(Ltype.Eof, null);
            }

            private void Parse(int idx = 0)
            {
                this.tokens.Clear();
                Lexeme lexeme;
                idx = 0;
                goto none;

            none:
                lexeme = this.Lex(ref idx);
                switch (lexeme.Type)
                {
                case Ltype.Identifier:
                    goto identifier;
                case Ltype.KwEnum:
                    goto enumBegin;
                default:
                    goto backToSafety;
                }

            identifier:

                goto none;

            enumBegin:
                lexeme = this.Lex(ref idx);
                switch (lexeme.Type)
                {
                case Ltype.Identifier:
                    this.tokens.Push(new EnumTk(lexeme.Str));
                    goto enumDeclOrDef;
                default:
                    goto backToSafety;
                }

            enumDeclOrDef:
                lexeme = this.Lex(ref idx);
                switch (lexeme.Type)
                {
                case Ltype.Lacc:
                    goto enumDeclareMembers;
                default:
                    goto backToSafety;
                }

            enumDeclareMembers:
                lexeme = this.Lex(ref idx);
                switch (lexeme.Type)
                {
                case Ltype.Identifier:
                    goto enumDeclareMembers;
                default:
                    goto backToSafety;
                }

            backToSafety:
                switch (this.Lex(ref idx).Type)
                {
                case Ltype.Semic:
                    goto none;
                case Ltype.Eof:
                    return;
                default:
                    goto backToSafety;
                }

            }

            async private void Loop()
            {
                TextBlock block;
                while (!this.taskToken.IsCancellationRequested)
                {
                    while (!this.inqueue.TryDequeue(out block))
                        await Task.Delay(WAIT_FOR_BLOCK);
                    var idsHgOld = new LinkedList<Token>(this.keywordsHg);
                    var keywordsHgOld = new LinkedList<Token>(this.keywordsHg);
                    this.idsHg.Clear();
                    this.keywordsHg.Clear();
                    switch (block.Action)
                    {
                    case TextBlock.Tag.Insert:
                        text.Insert(block.Where, block.Text);
                        this.Parse(block.Where);
                        break;
                    case TextBlock.Tag.Remove:
                        text.Remove(block.Where, block.Text.Length);
                        this.Parse(block.Where);
                        break;
                    case TextBlock.Tag.Set:
                        text.Set(block.Text);
                        this.Parse();
                        break;
                    }
                    foreach (var toRemove in keywordsHgOld.Except(this.keywordsHg))
                        this.outqueue.Enqueue(new Token(toRemove.Begin, toRemove.End, TokenType.None));
                    foreach (var toAdd in this.keywordsHg.Except(keywordsHgOld))
                        this.outqueue.Enqueue(toAdd);
                    foreach (var toAdd in this.idsHg.Except(idsHgOld))
                        this.outqueue.Enqueue(toAdd);
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
