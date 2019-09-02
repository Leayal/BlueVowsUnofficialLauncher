using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BlueVowsLauncher.Controls
{
    /// <summary>
    /// Interaction logic for PlaceholderTextBox.xaml
    /// </summary>
    public partial class PlaceholderTextBox : TextBox
    {
        public PlaceholderTextBox()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty PlaceholderColorProperty = DependencyProperty.Register("PlaceholderColor", typeof(Brush), typeof(PlaceholderTextBox), new PropertyMetadata((new Func<Brush>(() => { var result = new SolidColorBrush(Color.FromRgb(230, 230, 230)); result.Freeze(); return result; }))()));

        public string PlaceholderColor
        {
            get => (string)this.GetValue(PlaceholderColorProperty);
            set => this.SetValue(PlaceholderColorProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register("Placeholder", typeof(string), typeof(PlaceholderTextBox), new PropertyMetadata(string.Empty));

        public string Placeholder
        {
            get => (string)this.GetValue(PlaceholderProperty);
            set => this.SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextAlignmentProperty = DependencyProperty.Register("PlaceholderTextAlignment", typeof(TextAlignment), typeof(PlaceholderTextBox), new PropertyMetadata(TextAlignment.Center));

        public TextAlignment PlaceholderTextAlignment
        {
            get => (TextAlignment)this.GetValue(PlaceholderTextAlignmentProperty);
            set => this.SetValue(PlaceholderTextAlignmentProperty, value);
        }
    }
}
