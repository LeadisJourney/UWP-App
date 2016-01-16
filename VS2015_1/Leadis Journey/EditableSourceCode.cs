using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The Templated Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234235

namespace Leadis_Journey
{
    public sealed class EditableSourceCode : Control
    {
        readonly Color colorBackground = Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E);
        readonly Color colorText = Color.FromArgb(0xFF, 0xDC, 0xDC, 0xDC);
        readonly Color colorKeyword = Color.FromArgb(0xFF, 0x56, 0x9C, 0xD6);
        readonly Color colorDirective = Color.FromArgb(0xFF, 0x9B, 0x9B, 0x9B);
        readonly Color colorLitNumber = Color.FromArgb(0xFF, 0xB5, 0xCE, 0xA8);
        readonly Color colorLitString = Color.FromArgb(0xFF, 0xD6, 0x9D, 0x85);
        readonly Color colorMacro = Color.FromArgb(0xFF, 0xBD, 0x63, 0xC5);
        readonly Color colorComment = Color.FromArgb(0xFF, 0x57, 0xA6, 0x4A);

        private RichEditBox xEditBox;
        private CParser cParser = new CParser();

        public EditableSourceCode()
        {
            this.DefaultStyleKey = typeof(EditableSourceCode);
            this.ApplyTemplate();
            this.Background = new SolidColorBrush(colorBackground);
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
            this.xEditBox.KeyDown += XEditBox_KeyDown;
        }

        private void XEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var xEditBox = sender as RichEditBox;
            if (e.Key == Windows.System.VirtualKey.Tab)
            {
                xEditBox.Document.Selection.TypeText("  ");
                e.Handled = true;
            }
        }

        private void XEditBox_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            var document = sender.Document;
            string text;
            document.GetText(0, out text);
            document.GetRange(0, text.Length).CharacterFormat.ForegroundColor = colorText;
            var tokens = this.cParser.Parse(text);
            foreach (var token in tokens)
            {
                var range = document.GetRange(token.Start, token.End);
                switch (token.Type)
                {
                    case TokenType.Keyword:
                        range.CharacterFormat.ForegroundColor = colorKeyword;
                        break;
                    case TokenType.Directive:
                        range.CharacterFormat.ForegroundColor = colorDirective;
                        break;
                    case TokenType.LitNumber:
                        range.CharacterFormat.ForegroundColor = colorLitNumber;
                        break;
                    case TokenType.LitString:
                        range.CharacterFormat.ForegroundColor = colorLitString;
                        break;
                    case TokenType.Macro:
                        range.CharacterFormat.ForegroundColor = colorMacro;
                        break;
                    case TokenType.Comment:
                        range.CharacterFormat.ForegroundColor = colorComment;
                        break;
                    default:
                        break;
                }
            }
            document.ApplyDisplayUpdates();
        }
    }
}
