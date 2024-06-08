using LRCLibNet;
using LyricSyncWindows.Entities;
using SpotifyAPI.Web;
using Swan;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup.Localizer;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using SkiaSharp;

namespace LyricSyncWindows
{
    /// <summary>
    /// Interaction logic for EditSync.xaml
    /// </summary>
    public partial class EditSync : Page
    {
        public LrcTrack Track { get; set; }
        internal bool started;

        public Song Song { get; set; }
        public List<Word> LocalWords { get; set; } = [];

        private int index = 0;
        /// <summary>
        /// The current word index
        /// </summary>
        private int currentLineIndex = 0;

        /// <summary>
        /// The current line index
        /// </summary>
        private int currentSyncLineIndex = 0;
        private int backingLinesAdded = 1;
        private int backingLinesSynced = 1;

        private int playerIndex = 0;
        private int lineIndex = 0;
        private int wordIndex = 0;

        private int syllableIndex = 0;

        private int progressOffset = 0;
        private Stopwatch progressTimer;

        private bool autoScroll = true;

        public EditSync(LrcTrack track)
        {
            InitializeComponent();
            Track = track;

            MainWindow.instance.homeButton.Background = new SolidColorBrush(Colors.Transparent);
            MainWindow.instance.searchButton.Background = new SolidColorBrush(Colors.Transparent);
            MainWindow.instance.contributeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2e2e2e"));

            if (File.Exists($"Lyrics/{Track.Id}.json"))
                Song = JsonSerializer.Deserialize<Song>(File.ReadAllText($"Lyrics/{Track.Id}.json"));
            else
                Song = new Song(track);

            Song.SetTrack(track);

            switch (Song.SyncLevel)
            {
                case 1:
                    LocalWords = [.. Song.Words];
                    LocalWords.RemoveAll(line => string.IsNullOrWhiteSpace(line.Content));

                    break;

                case 2:

                    if (Song.BackingVocals.Count > 0)
                        LocalWords = [.. Song.BackingVocals];

                    break;

                case 3:
                    LocalWords = [.. Song.Words];
                    LocalWords.RemoveAll(line => string.IsNullOrWhiteSpace(line.Content));

                    break;

                case 4:

                    foreach(var word in Song.Words)
                    {
                        if (word.Syllables?.Count > 0)
                            LocalWords.Add(word);
                    }

                    break;

                default:

                    LocalWords = [.. Song.Words];
                    LocalWords.RemoveAll(line => string.IsNullOrWhiteSpace(line.Content));

                    break;
            }

            PreviewMouseDown += OnMouseDown;
            PreviewMouseUp += OnMouseUp;

            progressTimer = new Stopwatch();

            System.Timers.Timer checkTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5).TotalMilliseconds);
            checkTimer.Start();

            // After 5 seconds, get the actual Spotify progress instead of guessing
            checkTimer.Elapsed += async (sender, e) =>
            {
                var playback = await MainWindow.Spotify.Player.GetCurrentPlayback();

                if (playback != null)
                    progressOffset = (int)progressTimer.ElapsedMilliseconds - playback.ProgressMs;
            };

            Task.Run(async () =>
            {
                var search = await MainWindow.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{Track.Title} {Track.Artist}"));
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
            });
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!started)
            {
                started = true;
                progressTimer.Start();

                return;
            }

            if (index >= LocalWords.Count)
            {
                // Song is over
                MessageBox.Show("Song is over!");

                progressTimer.Stop();

                PreviewMouseDown -= OnMouseDown;
                PreviewMouseUp -= OnMouseUp;

                // foreach (var child in lyricStack.Children) // The enumerator is not valid because the collection changed
                // {
                //     lyricStack.Children.Remove(child as UIElement);
                // 
                //     Song.UpdateSyncLevel(SyncType.Word);
                // }


                lyricStack.Dispatcher.BeginInvoke((Action)(() =>
                {
                    // foreach(var child in lyricStack.Children)
                    // {
                    //     lyricStack.Children.Remove(child as UIElement);
                    // }

                    /*switch (Song.SyncLevel)
                    {
                        case SyncType.None:
                            Song.UpdateSyncLevel(SyncType.Line);
                            break;

                        case SyncType.Line:
                            Song.UpdateSyncLevel(SyncType.Word);
                            break;

                        case SyncType.Word:
                            Song.UpdateSyncLevel(SyncType.WordBacked);
                            break;

                        case SyncType.WordBacked:
                            Song.UpdateSyncLevel(SyncType.Syllable);
                            break;

                        case SyncType.Syllable:
                            Song.UpdateSyncLevel(SyncType.SyllableBacked);
                            break;
                    }*/


                    NavigationService.Navigate(new SearchPage());
                }));

                Song.UpdateSyncLevel(Song.SyncLevel + 1);

                Directory.CreateDirectory("Lyrics");
                File.WriteAllText($"Lyrics/{Track.Id}.json", JsonSerializer.Serialize(Song));

                return;
            }

            if (Song.SyncLevel < 4)
                LocalWords[index].Time.End = (int)(progressTimer.ElapsedMilliseconds - progressOffset);

            if (Song.SyncLevel == 1)
            {
                if (lyricStack.Children.Count > currentSyncLineIndex)
                {
                    StackPanel stack = lyricStack.Children[currentSyncLineIndex] as StackPanel;
                    TextBlock text = stack.Children[currentLineIndex] as TextBlock;

                    // If there are more words in this line, then you can continue syncing the next word
                    if (currentLineIndex < stack.Children.Count)
                    {
                        text.Foreground = Brushes.White;

                        if (currentLineIndex + 1 == stack.Children.Count)
                        {
                            // We've reached the last word, move onto the next line

                            currentSyncLineIndex++;
                            currentLineIndex = 0;
                        }
                        else
                            currentLineIndex++;

                        index++;

                        /*while (string.IsNullOrWhiteSpace(LocalWords[index].Content))
                        {
                            index++;

                            currentSyncLineIndex++;
                            currentLineIndex = 0;
                        }*/
                    }
                }
            }
            else if (Song.SyncLevel == 2)
            {
                if (lyricStack.Children.Count > currentSyncLineIndex && LocalWords[index].LeadVocalAttatchment is int leadIndex)
                {
                    StackPanel stack = lyricStack.Children[leadIndex + backingLinesSynced] as StackPanel;
                    TextBlock text = stack.Children[currentLineIndex] as TextBlock;

                    // If there are more words in this line, then you can continue syncing the next word
                    if (currentLineIndex < stack.Children.Count)
                    {
                        text.Foreground = Brushes.White;

                        if (currentLineIndex + 1 == stack.Children.Count)
                        {
                            // We've reached the last word, move onto the next line

                            currentSyncLineIndex++;
                            backingLinesSynced++;
                            currentLineIndex = 0;
                        }
                        else
                            currentLineIndex++;

                        index++;

                        if (index >= LocalWords.Count)
                        {
                            MessageBox.Show("Song is over!");

                            progressTimer.Stop();

                            PreviewMouseDown -= OnMouseDown;
                            PreviewMouseUp -= OnMouseUp;

                            Song.UpdateSyncLevel(Song.SyncLevel + 1);

                            Directory.CreateDirectory("Lyrics");
                            File.WriteAllText($"Lyrics/{Track.Id}.json", JsonSerializer.Serialize(Song));

                            lyricStack.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                NavigationService.Navigate(new SearchPage());
                            }));
                        }

                        /*while (string.IsNullOrWhiteSpace(LocalWords[index].Content))
                        {
                            index++;

                            currentSyncLineIndex++;
                            currentLineIndex = 0;
                        }*/
                    }
                }
            }
            else if (Song.SyncLevel == 4)
            {
                if (lyricStack.Children.Count > currentSyncLineIndex && LocalWords[index].Syllables?.Count > 0)
                {
                    List<StackPanel> stacks = new List<StackPanel>();

                    foreach (var stackPanel in lyricStack.Children)
                    {
                        if (stackPanel is StackPanel panel && panel.Children.OfType<TextBlock>().Any(x => x.Inlines.Count > 1))
                            stacks.Add(panel);
                    }

                    StackPanel stack = stacks[currentSyncLineIndex];
                    IEnumerable<UIElement> texts = stack.Children.OfType<UIElement>().Where(x => ((TextBlock)x).Inlines.Count > 1);
                    TextBlock textBlock = texts.ElementAt(currentLineIndex) as TextBlock;
                    // TextBlock textBlock = stack.Children[currentLineIndex] as TextBlock;

                    // If there are more words in this line, then you can continue syncing the next word
                    if (currentLineIndex < stack.Children.Count)
                    {
                        textBlock.Inlines.ElementAt(syllableIndex).Foreground = Brushes.White;
                        LocalWords[index].Syllables[syllableIndex].Time.End = (int)(progressTimer.ElapsedMilliseconds - progressOffset);

                        // We've reached the last syllable in the word, move onto the next syllable word
                        if (syllableIndex + 1 == textBlock.Inlines.Count)
                        {
                            syllableIndex = 0;

                            if (currentLineIndex + 1 == texts.Count())
                            {
                                // We've reached the last word, move onto the next line

                                currentSyncLineIndex++;
                                currentLineIndex = 0;
                            }
                            else
                                currentLineIndex++;

                            index++;

                            if(index >= LocalWords.Count)
                            {
                                MessageBox.Show("Song is over!");

                                progressTimer.Stop();

                                PreviewMouseDown -= OnMouseDown;
                                PreviewMouseUp -= OnMouseUp;

                                Song.UpdateSyncLevel();

                                Directory.CreateDirectory("Lyrics");
                                File.WriteAllText($"Lyrics/{Track.Id}.json", JsonSerializer.Serialize(Song));

                                lyricStack.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    NavigationService.Navigate(new SearchPage());
                                }));
                            }
                        }
                        else
                            syllableIndex++;
                    }
                }
            }

            downText.Visibility = Visibility.Hidden;
            upText.Visibility = Visibility.Visible;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs mouse)
        {
            if (mouse.LeftButton == MouseButtonState.Pressed)
            {
                if (!started)
                {
                    // Song started

                    Panel panel = startText.Parent as Panel;
                    panel.Children.Remove(startText);

                    Task.Run(async () =>
                    {
                        if (Song.SyncLevel == 1 || Song.SyncLevel == 3)
                        {
                            await lyricStack.Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var word in LocalWords)
                                {
                                    if (word.LineStart && word.LineEnd)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Children = { textBlock }
                                        };

                                        // textBlock.Inlines.Add(new Run(line));

                                        lyricStack.Children.Add(stack);
                                    }
                                    else if (word.LineStart)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center
                                        };

                                        stack.Children.Add(textBlock);
                                        lyricStack.Children.Add(stack);
                                    }
                                    /*else if (word.LineEnd)
                                    {
                                        line += $" {word.Content.Trim()}";

                                        TextBlock textBlock = new TextBlock();

                                        line.Trim().Split(' ').ToList().ForEach(word =>
                                        {
                                            textBlock.Inlines.Add(new Run($"{word} "));
                                        });

                                        lyricStack.Children.Add(textBlock);
                                    }*/
                                    else
                                    {
                                        StackPanel stack = lyricStack.Children[lyricStack.Children.Count - 1] as StackPanel;

                                        stack.Children.Add(new TextBlock()
                                        {
                                            Text = $" {word.Content.Trim()}",
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        });
                                    }
                                }
                            });
                        }
                        else if (Song.SyncLevel == 2)
                        {
                            await lyricStack.Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var word in Song.Words)
                                {
                                    if (word.LineStart && word.LineEnd)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Children = { textBlock }
                                        };

                                        // textBlock.Inlines.Add(new Run(line));

                                        lyricStack.Children.Add(stack);
                                    }
                                    else if (word.LineStart)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center
                                        };

                                        stack.Children.Add(textBlock);
                                        lyricStack.Children.Add(stack);
                                    }
                                    /*else if (word.LineEnd)
                                    {
                                        line += $" {word.Content.Trim()}";

                                        TextBlock textBlock = new TextBlock();

                                        line.Trim().Split(' ').ToList().ForEach(word =>
                                        {
                                            textBlock.Inlines.Add(new Run($"{word} "));
                                        });

                                        lyricStack.Children.Add(textBlock);
                                    }*/
                                    else
                                    {
                                        StackPanel stack = lyricStack.Children[lyricStack.Children.Count - 1] as StackPanel;

                                        stack.Children.Add(new TextBlock()
                                        {
                                            Text = $" {word.Content.Trim()}",
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        });
                                    }
                                }

                                // Add the backing vocals to the stack
                                foreach (var word in LocalWords)
                                {
                                    if (word.LineStart && word.LineEnd && word.LeadVocalAttatchment is int leadIndex)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = word.IsAlone ? 24 : 16
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            Children = { textBlock }
                                        };

                                        lyricStack.Children.Insert(leadIndex + backingLinesAdded, stack);
                                        backingLinesAdded++;
                                    }
                                    else if (word.LineStart && word.LeadVocalAttatchment is int leadIndexOne)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = word.IsAlone ? 24 : 16,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Children = { textBlock }
                                        };

                                        lyricStack.Children.Insert(leadIndexOne + backingLinesAdded, stack);
                                        backingLinesAdded++;
                                    }
                                    else if (word.LeadVocalAttatchment is int leadIndexTwo)
                                    {
                                        StackPanel stack = lyricStack.Children[leadIndexTwo + backingLinesAdded - 1] as StackPanel;

                                        stack.Children.Add(new TextBlock()
                                        {
                                            Text = $" {word.Content.Trim()}",
                                            FontSize = 16,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        });
                                    }
                                }
                            });
                        }
                        else if (Song.SyncLevel == 4)
                        {
                            await lyricStack.Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var word in Song.Words)
                                {
                                    if (word.LineStart && word.LineEnd)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24
                                        };

                                        StackPanel stack = new StackPanel
                                        {
                                            Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            Children = { textBlock }
                                        };

                                        if (word.Syllables?.Count > 0)
                                        {
                                            textBlock.Text = "";

                                            foreach (var syllable in word.Syllables)
                                            {
                                                Run run = new Run(syllable.Content)
                                                {
                                                    FontWeight = FontWeights.Bold
                                                };

                                                textBlock.Inlines.Add(run);
                                            }
                                        }

                                        lyricStack.Children.Add(stack);
                                    }
                                    else if (word.LineStart)
                                    {
                                        TextBlock textBlock = new TextBlock
                                        {
                                            Text = word.Content.Trim(),
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        };

                                        if (word.Syllables?.Count > 0)
                                        {
                                            textBlock.Text = "";

                                            foreach (var syllable in word.Syllables)
                                            {
                                                Run run = new Run(syllable.Content)
                                                {
                                                    FontWeight = FontWeights.Bold
                                                };

                                                textBlock.Inlines.Add(run);
                                            }
                                        }

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

                                        TextBlock textBlock = new TextBlock()
                                        {
                                            Text = $" {word.Content.Trim()}",
                                            FontSize = 24,
                                            Margin = new Thickness(0, 0, 1, 0)
                                        };

                                        if (word.Syllables?.Count > 0)
                                        {
                                            textBlock.Text = "";
                                            int index = 0;

                                            foreach (var syllable in word.Syllables)
                                            {
                                                Run run = new Run(index == 0 ? $" {syllable.Content}" : syllable.Content)
                                                {
                                                    FontWeight = FontWeights.Bold
                                                };

                                                textBlock.Inlines.Add(run);
                                                index++;
                                            }
                                        }

                                        stack.Children.Add(textBlock);
                                    }
                                }
                            });
                        }

                        var search = await MainWindow.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{Track.Title} {Track.Artist}"));
                        FullTrack track = search.Tracks.Items.First();

                        await MainWindow.Spotify.Player.AddToQueue(new PlayerAddToQueueRequest(track.Uri.ToString()));
                        await MainWindow.Spotify.Player.SkipNext();

                        int duration = Song.Duration * 1000;

                        if (Song.SyncLevel == 2)
                        {
                            while (progressTimer.ElapsedMilliseconds - progressOffset < duration)
                            {
                                if (Song.Words.ElementAt(playerIndex).Time.Start <= progressTimer.ElapsedMilliseconds - progressOffset)
                                {
                                    await lyricStack.Dispatcher.BeginInvoke(() =>
                                    {
                                        // If there are more lines in the lyrics, then continue checking
                                        if (lyricStack.Children.Count > lineIndex)
                                        {
                                            StackPanel stack = lyricStack.Children[lineIndex] as StackPanel;
                                            TextBlock text = stack.Children[wordIndex] as TextBlock;

                                            // If there are more words in this line, then you can continue highlighting the next word in the line
                                            if (wordIndex < stack.Children.Count)
                                            {
                                                text.Foreground = Brushes.SkyBlue;

                                                DoubleAnimation thing = new DoubleAnimation(25, TimeSpan.FromMilliseconds(Math.Max(Song.Words.ElementAt(playerIndex).Time.End - Song.Words.ElementAt(playerIndex).Time.Start, 0)));
                                                thing.AutoReverse = true;
                                                thing.EasingFunction = new SineEase();

                                                text.BeginAnimation(TextBlock.FontSizeProperty, thing);

                                                if (wordIndex + 1 == stack.Children.Count)
                                                {
                                                    // We've reached the last word, move onto the next line

                                                    lineIndex++;
                                                    wordIndex = 0;
                                                }
                                                else
                                                    wordIndex++;

                                                playerIndex++;
                                            }

                                            while (lineIndex < lyricStack.Children.Count && ((StackPanel)lyricStack.Children[lineIndex]).Children.OfType<UIElement>().Any(x => ((TextBlock)x).FontSize < 24))
                                            {
                                                lineIndex++;
                                            }
                                        }
                                    });
                                }
                            }
                        }
                        else if (Song.SyncLevel == 3)
                        {
                            while (progressTimer.ElapsedMilliseconds + progressOffset < duration)
                            {
                                if (Song.Words.ElementAt(playerIndex).Time.Start <= progressTimer.ElapsedMilliseconds - progressOffset)
                                {
                                    await lyricStack.Dispatcher.BeginInvoke(() =>
                                    {
                                        // If there are more lines in the lyrics, then continue checking
                                        if (lyricStack.Children.Count > lineIndex)
                                        {
                                            StackPanel stack = lyricStack.Children[lineIndex] as StackPanel;
                                            TextBlock text = stack.Children[wordIndex] as TextBlock;

                                            // If there are more words in this line, then you can continue highlighting the next word in the line
                                            if (wordIndex < stack.Children.Count)
                                            {
                                                text.Foreground = Brushes.DarkGray;

                                                if (wordIndex + 1 == stack.Children.Count)
                                                {
                                                    // We've reached the last word, move onto the next line

                                                    lineIndex++;
                                                    wordIndex = 0;
                                                }
                                                else
                                                    wordIndex++;

                                                playerIndex++;
                                            }
                                        }
                                    });
                                }

                                await progressText.Dispatcher.BeginInvoke(() =>
                                {
                                    progressText.Text = $"{progressTimer.ElapsedMilliseconds - progressOffset}";
                                });
                            }
                        }
                        else if (Song.SyncLevel == 4)
                        {
                            while (progressTimer.ElapsedMilliseconds + progressOffset < duration)
                            {
                                if (Song.Words.ElementAt(playerIndex).Time.Start <= progressTimer.ElapsedMilliseconds - progressOffset)
                                {
                                    await lyricStack.Dispatcher.BeginInvoke(() =>
                                    {
                                        // If there are more lines in the lyrics, then continue checking
                                        if (lyricStack.Children.Count > lineIndex)
                                        {
                                            StackPanel stack = lyricStack.Children[lineIndex] as StackPanel;
                                            TextBlock text = stack.Children[wordIndex] as TextBlock;

                                            // If there are more words in this line, then you can continue highlighting the next word in the line
                                            if (wordIndex < stack.Children.Count)
                                            {
                                                if (Song.Words.ElementAt(playerIndex).Syllables?.Count == 0)
                                                    text.Foreground = Brushes.SkyBlue;

                                                if (wordIndex + 1 == stack.Children.Count)
                                                {
                                                    // We've reached the last word, move onto the next line

                                                    lineIndex++;
                                                    wordIndex = 0;
                                                }
                                                else
                                                    wordIndex++;

                                                playerIndex++;
                                            }
                                        }
                                    });
                                }

                                await progressText.Dispatcher.BeginInvoke(() =>
                                {
                                    progressText.Text = $"{progressTimer.ElapsedMilliseconds - progressOffset}";
                                });
                            }
                        }
                    });

                    System.Timers.Timer syncTimer = new System.Timers.Timer(TimeSpan.FromSeconds(3));
                    syncTimer.Start();

                    syncTimer.Elapsed += async (sender, e) =>
                    {
                        var playback = await MainWindow.Spotify.Player.GetCurrentPlayback();
                        progressOffset = playback.ProgressMs;

                        syncTimer.Stop();
                    };
                }
                else
                {
                    if (Song.SyncLevel < 4 && index < LocalWords.Count)
                        LocalWords[index].Time.Start = (int)(progressTimer.ElapsedMilliseconds - progressOffset);

                    if (Song.SyncLevel == 1 || Song.SyncLevel == 3)
                    {
                        if (lyricStack.Children.Count > currentSyncLineIndex)
                        {
                            StackPanel stack = lyricStack.Children[currentSyncLineIndex] as StackPanel;
                            TextBlock text = stack.Children[currentLineIndex] as TextBlock;

                            wordCountText.Text = stack.Children.Count.ToString();

                            // If there are more words in this line, then you can continue syncing the next word
                            if (currentLineIndex < stack.Children.Count)
                                text.Foreground = Brushes.Gray;
                        }
                    }
                    else if (Song.SyncLevel == 2)
                    {
                        if (lyricStack.Children.Count > currentSyncLineIndex && LocalWords[index].LeadVocalAttatchment is int leadIndex)
                        {
                            StackPanel stack = lyricStack.Children[leadIndex + backingLinesSynced] as StackPanel;
                            TextBlock text = stack.Children[currentLineIndex] as TextBlock;

                            wordCountText.Text = stack.Children.Count.ToString();

                            // If there are more words in this line, then you can continue syncing the next word
                            if (currentLineIndex < stack.Children.Count)
                                text.Foreground = Brushes.Gray;
                        }
                    }
                    else if (Song.SyncLevel == 4)
                    {
                        if (lyricStack.Children.Count > currentSyncLineIndex && LocalWords[index].Syllables?.Count > 0)
                        {
                            List<StackPanel> stacks = new List<StackPanel>();

                            foreach (var stackPanel in lyricStack.Children)
                            {
                                if (stackPanel is StackPanel panel && panel.Children.OfType<TextBlock>().Any(x => x.Inlines.Count > 1))
                                    stacks.Add(panel);
                            }

                            StackPanel stack = stacks[currentSyncLineIndex];

                            IEnumerable<UIElement> texts = stack.Children.OfType<UIElement>().Where(x => ((TextBlock)x).Inlines.Count > 1);
                            TextBlock text = texts.ElementAt(currentLineIndex) as TextBlock;
                            // TextBlock text = stack.Children[currentLineIndex] as TextBlock;

                            if (syllableIndex < text.Inlines.Count)
                            {
                                text.Inlines.ElementAt(syllableIndex).Foreground = Brushes.Gray;
                                LocalWords[index].Syllables[syllableIndex].Time.Start = (int)(progressTimer.ElapsedMilliseconds - progressOffset);
                            }
                        }
                    }

                    upText.Visibility = Visibility.Hidden;
                    downText.Visibility = Visibility.Visible;

                    // currentLineIndex++;
                }
            }
        }

        private async Task PlayLineByLine(LrcTrack track)
        {
            SpotifyClient client = MainWindow.Spotify;

            int index = 0;

            string[] lines = track.SyncedLyrics.Split('\n');

            string first = lines[index];
            string[] firstNumbers = first[1..].Split(']')[0].Split(':');
            string[] firstSeconds = firstNumbers[1].Split(']')[0].Split('.');

            LrcLine time = new LrcLine
            {
                Minute = int.Parse(firstNumbers[0]),
                Second = int.Parse(firstSeconds[0]),
                Milisecond = int.Parse(firstSeconds[1])
            };

            System.Timers.Timer timer = new System.Timers.Timer(time.Minute * 60000 + time.Second * 1000 + time.Milisecond);
            timer.Start();

            int progress = -250;

            System.Timers.Timer progressTimer = new System.Timers.Timer(1);
            progressTimer.Start();
            progressTimer.Elapsed += (sender, e) => progress++;

            // Account for initial delay, after 5 seconds get the actual Spotify progress instead of guessing
            System.Timers.Timer syncTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5));
            syncTimer.Start();
            syncTimer.Elapsed += async (sender, e) =>
            {
                var playback = await client.Player.GetCurrentPlayback();
                progress = playback.ProgressMs;
                syncTimer.Stop();
            };

            string currentLine = first.Split(']')[1][1..];

            timer.Elapsed += async (sender, e) =>
            {
                // await textBlock.Dispatcher.BeginInvoke((Action)(() => textBlock.Text = currentLine));

                await lyricStack.Dispatcher.BeginInvoke(() =>
                {
                    TextBlock textBlock = new TextBlock();

                    currentLine.Split(' ').ToList().ForEach(word =>
                    {
                        textBlock.Inlines.Add(new Run($"{word} "));
                    });

                    lyricStack.Children.Add(textBlock);
                });

                index++;
                // currentLineIndex = 0;

                if (index > lines.Length - 1)
                {
                    timer.Stop();
                    return;
                }

                // Parse the timestamp data
                string current = lines[index];
                string[] numbers = current[1..].Split(']')[0].Split(':');
                string[] seconds = numbers[1].Split(']')[0].Split('.');

                LrcLine time = new LrcLine
                {
                    Minute = int.Parse(numbers[0]),
                    Second = int.Parse(seconds[0]),
                    Milisecond = int.Parse(seconds[1])
                };

                string previousCurrent = index == 0 ? lines[index] : lines[index - 1];
                string[] previousNumbers = previousCurrent[1..].Split(']')[0].Split(':');
                string[] previousSeconds = previousNumbers[1].Split(']')[0].Split('.');

                LrcLine previousTime = new LrcLine
                {
                    Minute = int.Parse(previousNumbers[0]),
                    Second = int.Parse(previousSeconds[0]),
                    Milisecond = int.Parse(previousSeconds[1])
                };

                timer.Interval = (time.Minute * 60000 + time.Second * 1000 + time.Milisecond) - (index == 0 ? 0 : (previousTime.Minute * 60000 + previousTime.Second * 1000 + previousTime.Milisecond)); // Works, but it's too slow
                currentLine = current.Split(']')[1][1..];
            };
        }

        /*private void scroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
            {
                if (scroller.VerticalOffset == scroller.ScrollableHeight)
                    autoScroll = true;
                else
                    autoScroll = false;
            }

            if (autoScroll && e.ExtentHeightChange != 0)
                scroller.ScrollToVerticalOffset(scroller.ExtentHeight);
        }*/
    }
}