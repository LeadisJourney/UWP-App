using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Diagnostics;
using Windows.UI.Text;
using Windows.UI.Xaml;

namespace Leadis_Journey
{
    public enum TokenType
    {
        None,
        Macro,
        Directive,
        LitString,
        LitChar,
        LitNumber,
        Keyword,
        Error,
    }

    public struct Token
    {
        public int Begin;
        public int End;
        public TokenType Type;

        public Token(int begin, int end, TokenType type)
        {
            this.Begin = begin;
            this.End = end;
            this.Type = type;
        }
    }

    public interface IHighlighter : IDisposable
    {
        void RemoveText(int where, int len);
        void InsertText(int where, string text);
        void SetText(string text);
        bool TryGetToken(out Token result);
    }

    public sealed class C99Highlighter : IHighlighter
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
            private enum Pstate
            {
                None            = 0x00000000,
                NoneNoPP        = 0x00000001,
                JumpToDefineAE  = 0x00000002,
                JumpToUndefAE   = 0x00000003,
            }

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
            private StateString<Pstate> text;

            public Action Start;

            public ParserTask(ConcurrentQueue<TextBlock> inqueue, ConcurrentQueue<Token> outqueue)
            {
                this.macros = new HashSet<string>();
                this.inqueue = inqueue;
                this.outqueue = outqueue;
                this.taskToken = new CancellationTokenSource();
                this.task = new Task(this.Loop, this.taskToken.Token);
                this.text = new StateString<Pstate>();
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
                Pstate cstate = this.text.States[idx];
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

# region Define parsing
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
# endregion

# region Undef parsing
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
                case  'a': goto litCharEnd;
                case  'b': goto litCharEnd;
                case  'f': goto litCharEnd;
                case  'n': goto litCharEnd;
                case  'r': goto litCharEnd;
                case  't': goto litCharEnd;
                case  'v': goto litCharEnd;
                case '\\': goto litCharEnd;
                case '\'': goto litCharEnd;
                case '\r': goto litCharEnd;
                case  '"': goto litCharEnd;
                case  '?': goto litCharEnd;
                case  'x': goto litCharEscapedXStart;
                case  '0': goto litCharEscapedO;
                case  '1': goto litCharEscapedO;
                case  '2': goto litCharEscapedO;
                case  '3': goto litCharEscapedO;
                case  '4': goto litCharEscapedO;
                case  '5': goto litCharEscapedO;
                case  '6': goto litCharEscapedO;
                case  '7': goto litCharEscapedO;
                case  '8': goto litCharEscapedO;
                case  '9': goto litCharEscapedO;
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
                case  'a': goto litStringCont;
                case  'b': goto litStringCont;
                case  'f': goto litStringCont;
                case  'n': goto litStringCont;
                case  'r': goto litStringCont;
                case  't': goto litStringCont;
                case  'v': goto litStringCont;
                case '\\': goto litStringCont;
                case '\'': goto litStringCont;
                case '\r': goto litStringCont;
                case  '"': goto litStringCont;
                case  '?': goto litStringCont;
                case  'x': goto litStringEscapedXStart;
                case  '0': goto litStringEscapedO;
                case  '1': goto litStringEscapedO;
                case  '2': goto litStringEscapedO;
                case  '3': goto litStringEscapedO;
                case  '4': goto litStringEscapedO;
                case  '5': goto litStringEscapedO;
                case  '6': goto litStringEscapedO;
                case  '7': goto litStringEscapedO;
                case  '8': goto litStringEscapedO;
                case  '9': goto litStringEscapedO;
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
                        break;
                    case TextBlock.Tag.Remove:
                        text.Remove(block.Where, block.Text.Length);
                        break;
                    case TextBlock.Tag.Set:
                        this.macros.Clear();
                        text.Remove(0, text.Length);
                        text.Insert(0, block.Text);
                        break;
                    }
                    this.Parse();
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

    public sealed partial class EditableSourceCode : Control
    {
        static readonly Color colorBackground = Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E);
        static readonly Color colorText = Color.FromArgb(0xFF, 0xDC, 0xDC, 0xDC);
        static readonly Color colorKeyword = Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6);
        static readonly Color colorDirective = Color.FromArgb(0xFF, 0x9B, 0x9B, 0x9B);
        static readonly Color colorLitNumber = Color.FromArgb(0xFF, 0xB5, 0xCE, 0xA8);
        static readonly Color colorLitString = Color.FromArgb(0xFF, 0xD6, 0x9D, 0x85);
        static readonly Color colorMacro = Color.FromArgb(0xFF, 0xBD, 0x63, 0xC5);
        static readonly Color colorComment = Color.FromArgb(0xFF, 0x57, 0xA6, 0x4A);
        static readonly Color colorLineNumber = Color.FromArgb(0xFF, 0x2B, 0x91, 0xAF);

        private IHighlighter highlighter;
        private RichEditBox xEditBox;

        public EditableSourceCode()
        {
            this.highlighter = new C99Highlighter();
            this.DefaultStyleKey = typeof(EditableSourceCode);
            this.ApplyTemplate();
            this.Background = new SolidColorBrush(colorBackground);
            this.Unloaded += EditableSourceCode_Unloaded;
        }

        private void EditableSourceCode_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            this.highlighter.Dispose();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var consolas = new FontFamily("Consolas");
            var fontSize = 14.0;
            this.xEditBox = this.GetTemplateChild("xEditBox") as RichEditBox;
            this.xEditBox.IsSpellCheckEnabled = false;
            this.xEditBox.IsTextPredictionEnabled = false;
            this.xEditBox.UseLayoutRounding = false;
            this.xEditBox.FontFamily = consolas;
            this.xEditBox.FontSize = fontSize;
            this.xEditBox.Background = new SolidColorBrush(Colors.Transparent);
            this.xEditBox.Foreground = new SolidColorBrush(colorText);
            this.xEditBox.Loaded += (a, b) =>
            {
                this.xEditBox.KeyUp += XEditBox_KeyUp;
                this.xEditBox.TextChanging += XEditBox_TextChanging;
            };
        }

        private void XEditBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            string text;
            this.xEditBox.Document.GetText(TextGetOptions.None, out text);
            this.highlighter.SetText(text);
        }

        async private Task RetrieveAndColourTokensWithDelay()
        {
            await Task.Delay(200);
            this.RetrieveAndColourTokens();
        }

        private void RetrieveAndColourTokens()
        {
            Token token;
            var doc = this.xEditBox.Document;
            while (this.highlighter.TryGetToken(out token))
            {
                var range = doc.GetRange(token.Begin, token.End);
                switch (token.Type)
                {
                case TokenType.None:
                    range.CharacterFormat.ForegroundColor = colorText;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.Macro:
                    range.CharacterFormat.ForegroundColor = colorMacro;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.Directive:
                    range.CharacterFormat.ForegroundColor = colorDirective;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.LitChar:
                    range.CharacterFormat.ForegroundColor = colorLitString;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.LitString:
                    range.CharacterFormat.ForegroundColor = colorLitString;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.LitNumber:
                    range.CharacterFormat.ForegroundColor = colorLitNumber;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.Keyword:
                    range.CharacterFormat.ForegroundColor = colorKeyword;
                    range.CharacterFormat.Underline = UnderlineType.None;
                    break;
                case TokenType.Error:
                    range.CharacterFormat.ForegroundColor = Colors.Red;
                    range.CharacterFormat.Underline = UnderlineType.Wave;
                    break;
                }
            }
            doc.ApplyDisplayUpdates();
        }

        async private void XEditBox_TextChanging(object sender, RichEditBoxTextChangingEventArgs e)
        {
            this.RetrieveAndColourTokens();
            await this.RetrieveAndColourTokensWithDelay();
        }
    }
}
