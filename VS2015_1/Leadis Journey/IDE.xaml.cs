using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Pour plus d'informations sur le modèle d'élément Page vierge, voir la page http://go.microsoft.com/fwlink/?LinkId=234238

namespace Leadis_Journey
{
    /// <summary>
    /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
    /// </summary>
    public sealed partial class IDE : Page
    {
        public IDE()
        {
            this.InitializeComponent();
            this.test2.Visibility = Visibility.Collapsed;
            this.test1btn.Tapped += Test1btn_Tapped;
            this.test2btn.Tapped += Test2btn_Tapped;
        }

        private void Test1btn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.test2.Visibility = Visibility.Collapsed;
            this.test1.Visibility = Visibility.Visible;
        }

        private void Test2btn_Tapped(object sender, TappedRoutedEventArgs e)
        {
            this.test1.Visibility = Visibility.Collapsed;
            this.test2.Visibility = Visibility.Visible;
        }
    }
}
