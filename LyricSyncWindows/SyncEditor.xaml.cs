using HandyControl.Tools.Extension;
using LyricSyncWindows.Entities;
using MahApps.Metro.IconPacks.Converter;
using SkiaSharp;
using SpotifyAPI.Web;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyricSyncWindows
{
    /// <summary>
    /// Interaction logic for SyncEditor.xaml
    /// </summary>
    public partial class SyncEditor : Page
    {
        public Song Song { get; set; }

        private TextPointer pointer = null;
        private RichTextBox selectedText = null;

        public SyncEditor(Song song)
        {
            InitializeComponent();

            Song = song;

            Loaded += OnLoaded;
            PreviewMouseDown += OnMouseDown;
        }

        private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && selectedText is RichTextBox text)
            {
                /*List<Run> runs = new List<Run>();

                if (text.Document.Blocks.FirstBlock is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines.Where(x => x is Run))
                    {
                        runs.Add(inline as Run);
                    }
                }

                int index = 0;
                foreach(var run in runs)
                {
                    if (pointer.CompareTo(run.ElementEnd) <= 0)
                        break;

                    index++;
                }

                if (index == 0)
                    index = runs.Count;

                if(index < runs.Count)
                {
                    Run clickedRun = runs[index];

                    if(clickedRun.Foreground == Brushes.Blue)
                    {
                        int position = pointer.GetOffsetToPosition(clickedRun.ElementEnd);
                        Run newBlueRun = new Run(clickedRun.Text[position..]) { Foreground = Brushes.Blue };
                        runs.Insert(index + 1, newBlueRun);
                        clickedRun.Text = clickedRun.Text[..position];
                        clickedRun.Foreground = Brushes.Green;
                    }
                    else if(clickedRun.Foreground == Brushes.Green)
                    {
                        int previousBlueIndex = -1;

                        for (int i = index - 1; i >= 0; i--)
                        {
                            if (runs[i].Foreground == Brushes.Blue)
                            {
                                previousBlueIndex = i;
                                break;
                            }
                        }

                        if(previousBlueIndex > 0)
                        {
                            Run newPurpleRun = new Run(clickedRun.Text) { Foreground = Brushes.Purple };
                            runs.Insert(index + 1, newPurpleRun);
                            clickedRun.Text = clickedRun.Text.Substring(0, index - previousBlueIndex);
                        }
                    }

                    text.Document.Blocks.Clear();

                    Paragraph newParagraph = new Paragraph();
                    newParagraph.Inlines.AddRange(runs);

                    text.Document.Blocks.Add(newParagraph);
                }
                else
                {*/
                    TextRange begin = new TextRange(pointer, text.Document.ContentStart);
                    begin.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Red);

                    TextRange end = new TextRange(pointer, text.Document.ContentEnd);
                    end.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Blue);
                // }

                int position = 0;

                for (int i = 0; i < lyricStack.Children.Count; i++)
                {
                    if (i == lyricStack.Children.IndexOf(text.Parent as UIElement))
                    {
                        position += (text.Parent as StackPanel).Children.IndexOf(text);
                        break;
                    }
                    else
                        position += (lyricStack.Children[i] as StackPanel).Children.Count;
                }

                // MessageBox.Show($"{Song.Words.ElementAt(position).Content}: {begin.Text} {end.Text}");

                Word word = Song.Words.ElementAt(position);

                word.Syllables.Clear();

                word.Syllables.Add(new Word.Syllable
                {
                    Content = begin.Text,
                });
                
                word.Syllables.Add(new Word.Syllable
                {
                    Content = end.Text,
                });
            }
        }

        private void RichTextBox_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RichTextBox text = sender as RichTextBox;
            TextPointer textPointer = text.GetPositionFromPoint(e.GetPosition(text), false);

            if (textPointer != null)
            {
                pointer = textPointer;
                selectedText = text;

                text.Focusable = true;
                text.Focus();

                text.CaretBrush = new SolidColorBrush(Colors.Red);
                text.IsReadOnlyCaretVisible = true;
                text.CaretPosition = textPointer;
            }
            else
            {
                pointer = null;
                selectedText = null;

                text.Focusable = false;
                Keyboard.ClearFocus();
            }
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                var search = await MainWindow.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{Song.Title} {Song.Artist}"));
                FullTrack track = search.Tracks.Items.First();

                using HttpClient download = new HttpClient();

                var imageStream = await download.GetStreamAsync(track.Album.Images[0].Url);

                AlbumArtImage.Dispatcher.Invoke(() =>
                {
                    SKBitmap skImage = SKBitmap.Decode(imageStream);
                    SKSurface surface = SKSurface.Create(new SKImageInfo(track.Album.Images[0].Width, track.Album.Images[0].Height));
                    SKCanvas canvas = surface.Canvas;

                    SKImageFilter filter = SKImageFilter.CreateBlur(25, 25);
                    SKPaint paint = new SKPaint
                    {
                        ImageFilter = filter
                    };

                    canvas.DrawBitmap(skImage, new SKPoint(0, 0), paint);

                    using var thingImage = surface.Snapshot();
                    using var data = thingImage.Encode(SKEncodedImageFormat.Png, 100);

                    BitmapImage newImage = new BitmapImage();
                    newImage.BeginInit();
                    newImage.CacheOption = BitmapCacheOption.OnLoad;
                    newImage.StreamSource = data.AsStream();
                    newImage.EndInit();

                    AlbumArtImage.Source = newImage;
                });

                await lyricStack.Dispatcher.BeginInvoke(() =>
                {
                    foreach (var word in Song.Words)
                    {
                        if (word.LineStart && word.LineEnd)
                        {
                            RichTextBox textBlock = new RichTextBox
                            {
                                FontSize = 24
                            };

                            textBlock.AppendText(word.Content.Trim());
                            FormattedText format = new FormattedText(word.Content.Trim(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 24, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            textBlock.Width = format.Width + 16;
                            textBlock.Document.PageWidth = format.Width + 16;

                            StackPanel stack = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Children = { textBlock }
                            };

                            lyricStack.Children.Add(stack);
                        }
                        else if (word.LineStart)
                        {
                            RichTextBox textBlock = new RichTextBox
                            {
                                FontSize = 24,
                                Margin = new Thickness(0, 0, 1, 0)
                            };

                            textBlock.AppendText(word.Content.Trim());
                            FormattedText format = new FormattedText(word.Content.Trim(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 24, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            textBlock.Width = format.Width + 16;
                            textBlock.Document.PageWidth = format.Width + 16;

                            StackPanel stack = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Center
                            };

                            stack.Children.Add(textBlock);
                            lyricStack.Children.Add(stack);
                        }
                        else
                        {
                            StackPanel stack = lyricStack.Children[lyricStack.Children.Count - 1] as StackPanel;

                            RichTextBox textBlock = new RichTextBox()
                            {
                                FontSize = 24,
                                Margin = new Thickness(-15, 0, 1, 0)
                            };

                            textBlock.AppendText($" {word.Content.Trim()}");
                            FormattedText format = new FormattedText($" {word.Content.Trim()}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 24, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                            textBlock.Width = format.Width + 16;
                            textBlock.Document.PageWidth = format.Width + 16;

                            stack.Children.Add(textBlock);
                        }
                    }
                });
            });

            MainWindow.instance.editorDoneButton.Visibility = Visibility.Visible;
        }
    }
}