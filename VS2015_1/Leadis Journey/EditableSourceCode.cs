using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

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
        private ConcurrentBag<Cparser.Token> bag;
        private Cparser.CParser cparser;

        public EditableSourceCode()
        {
            this.DefaultStyleKey = typeof(EditableSourceCode);
            this.ApplyTemplate();
            this.Background = new SolidColorBrush(colorBackground);
            this.cparser = new Cparser.CParser("test");
            this.cparser.NewTokenParsed += Cparser_NewTokenParsed;
        }

        private void Cparser_NewTokenParsed(Cparser.Token token)
        {
            this.bag.Add(token);
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
        }
    }
}
