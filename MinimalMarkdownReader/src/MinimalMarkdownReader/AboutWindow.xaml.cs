using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using MinimalMarkdownReader.Services;

namespace MinimalMarkdownReader;

public partial class AboutWindow : Window
{
    public AboutWindow(string title, string content, string closeText)
    {
        InitializeComponent();
        Title = title;
        CloseButton.Content = closeText;
        foreach (var line in AboutContent.Parse(content)) ContentPanel.Children.Add(CreateLine(line));
    }

    private static UIElement CreateLine(AboutLine line)
    {
        if (line.Segments.Count == 0) return new Border { Height = 8 };
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, line.IsHeading ? 6 : 2) };
        if (line.IsHeading)
        {
            block.FontSize = 20;
            block.FontWeight = FontWeights.SemiBold;
        }
        foreach (var segment in line.Segments)
        {
            if (segment.Link is null)
            {
                block.Inlines.Add(new Run(segment.Text));
                continue;
            }
            var link = new Hyperlink(new Run(segment.Text)) { NavigateUri = segment.Link, ToolTip = segment.Link.AbsoluteUri };
            link.RequestNavigate += (_, e) => { ExternalLinks.Open(e.Uri); e.Handled = true; };
            block.Inlines.Add(link);
        }
        return block;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
