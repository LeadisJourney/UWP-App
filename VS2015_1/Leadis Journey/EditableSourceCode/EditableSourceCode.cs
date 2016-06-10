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
        private int oldlen;

        public EditableSourceCode()
        {
            this.highlighter = new C99Highlighter();
            this.DefaultStyleKey = typeof(EditableSourceCode);
            this.ApplyTemplate();
            this.Background = new SolidColorBrush(colorBackground);
            this.Unloaded += EditableSourceCode_Unloaded;
            this.oldlen = 0;
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
            var doc = this.xEditBox.Document;
            string text;
            doc.GetText(TextGetOptions.UseCrlf, out text);
            if (text.Length == oldlen)
                return;
            if (text.Length < 16)
                this.highlighter.SetText(text);
            else if (text.Length > oldlen)
            {
                var diff = text.Length - this.oldlen;
                var pos = doc.Selection.StartPosition - diff;
                this.highlighter.InsertText(pos, text.Substring(pos, diff));
            }
            else
            {
                var diff = this.oldlen - text.Length;
                var pos = doc.Selection.StartPosition;
                this.highlighter.RemoveText(pos, diff);
            }
            this.oldlen = text.Length;
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
