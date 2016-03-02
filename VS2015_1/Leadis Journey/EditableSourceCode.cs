using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using CodeParser;

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace Leadis_Journey
{
    public sealed class EditableSourceCode : Control
    {
        static readonly Color colorBackground = Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E);
        static readonly Color colorText = Color.FromArgb(0xFF, 0xDC, 0xDC, 0xDC);
        static readonly Color colorKeyword = Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6);
        static readonly Color colorDirective = Color.FromArgb(0xFF, 0x9B, 0x9B, 0x9B);
        static readonly Color colorLitNumber = Color.FromArgb(0xFF, 0xB5, 0xCE, 0xA8);
        static readonly Color colorLitString = Color.FromArgb(0xFF, 0xD6, 0x9D, 0x85);
        static readonly Color colorMacro = Color.FromArgb(0xFF, 0xBD, 0x63, 0xC5);
        static readonly Color colorComment = Color.FromArgb(0xFF, 0x57, 0xA6, 0x4A);

        static readonly Dictionary<TokenType, Color> colorOfToken = new Dictionary<TokenType, Color>
        {
            { TokenType.None, colorText },
            { TokenType.Error, colorText },
            { TokenType.Directive, colorDirective },
            { TokenType.Macro, colorMacro },
        };

        private RichEditBox xEditBox;
        private ConcurrentBag<Token> bag;
        private AsyncParser parser;

        public EditableSourceCode()
        {
            this.DefaultStyleKey = typeof(EditableSourceCode);
            this.ApplyTemplate();
            this.Background = new SolidColorBrush(colorBackground);
            this.bag = new ConcurrentBag<Token>();
            this.parser = new AsyncParser("test");
            this.parser.NewTokenParsed += token => this.bag.Add(token);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.xEditBox = this.GetTemplateChild("xEditBox") as RichEditBox;
            this.xEditBox.IsSpellCheckEnabled = false;
            this.xEditBox.IsTextPredictionEnabled = false;
            this.xEditBox.UseLayoutRounding = false;
            this.xEditBox.FontFamily = new FontFamily("Consolas");
            this.xEditBox.Background = new SolidColorBrush(Colors.Transparent);
            this.xEditBox.Foreground = new SolidColorBrush(colorText);
            this.xEditBox.TextChanging += XEditBox_TextChanging;
            this.xEditBox.TextChanged += XEditBox_TextChanged;
        }

        private void XEditBox_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            var doc = sender.Document;
            Token token;
            while (this.bag.TryTake(out token))
            {
                var range = doc.GetRange(token.Begin, token.End);
                range.CharacterFormat.ForegroundColor = colorOfToken[token.Type];
            }
            doc.ApplyDisplayUpdates();
        }

        private void XEditBox_TextChanged(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var editBox = sender as RichEditBox;
            string doc;
            editBox.Document.GetText(TextGetOptions.UseCrlf, out doc);
            this.parser.Clear();
            this.parser.NewTextBlock(doc, 0);
        }
    }
}
