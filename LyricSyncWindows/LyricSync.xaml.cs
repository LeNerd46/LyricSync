using LRCLibNet;
using SpotifyAPI.Web;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.IO;
using System.Windows.Media.Imaging;
using SkiaSharp;
using LyricSyncWindows.Entities;
using Newtonsoft.Json;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Threading;
using Swan;

namespace LyricSyncWindows
{
    /// <summary>
    /// Interaction logic for LyricSync.xaml
    /// </summary>
    public partial class LyricSync : Page
    {
        public LrcTrack Track { get; set; }
        public Song Song { get; set; }
        public List<Word> LeadVocals { get; set; }

        private Stopwatch progressTimer;
        private int progressOffset = 0;

        private int index = 0;
        private int lineIndex = 0;
        private int wordIndex = 0;

        private int syllableIndex = 0;

        private int backingVocalsIndex = 0;
        private int backingVocalsLineIndex = 1;
        private int backingVocalsWordIndex = 0;

        internal bool started = false;
        private bool autoScroll = true;

        public LyricSync(LrcTrack track)
        {
            Track = track;
            InitializeComponent();

            started = false;

            InputManager.Current.PreProcessInput += OnInputReceived;

            // var storyboard = (Storyboard)FindResource("RotateStoryboard");
            // storyboard.Begin();

            progressTimer = new Stopwatch();
            Song = JsonConvert.DeserializeObject<Song>(File.ReadAllText($"Lyrics/{Track.Id}.json"));
            Song.SetTrack(track);

            switch (Song.SyncLevel)
            {
                case 2:
                    LeadVocals = [.. Song.Words];
                    break;

                case 3:
                    LeadVocals = [.. Song.Words];
                    break;

                case 4:
                    LeadVocals = [.. Song.Words];
                    break;

                case 5:
                    LeadVocals = [.. Song.Words];
                    break;
            }
        }

        private void OnInputReceived(object sender, ProcessInputEventArgs e)
        {
            if (e.StagingItem.Input is MouseButtonEventArgs mouse && mouse.LeftButton is MouseButtonState.Pressed && !started)
            {
                var parentContainer = startText.Parent as Panel;

                if (!started)
                {
                    started = true;

                    Panel panel = startText.Parent as Panel;
                    panel.Children.Remove(startText);

                    Task.Run(async () => await MainWindow.Spotify.Player.SkipNext());
                    progressTimer.Start();

                    System.Timers.Timer checkTimer = new System.Timers.Timer(5000);
                    checkTimer.Start();

                    checkTimer.Elapsed += async (sender, e) =>
                    {
                        var playback = await MainWindow.Spotify.Player.GetCurrentPlayback();

                        if (playback is not null)
                            progressOffset = (int)progressTimer.ElapsedMilliseconds - playback.ProgressMs;
                    };

                    try
                    {
                        Task.Run(async () =>
                        {
                            var search = await MainWindow.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{Track.Title} {Track.Artist}"));
                            FullTrack track = search.Tracks.Items.First();

                            using HttpClient download = new HttpClient();

                            try
                            {
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
                                    string line = string.Empty;

                                    foreach (var word in LeadVocals)
                                    {
                                        if (word.LineStart && word.LineEnd)
                                        {
                                            TextBlock textBlock = new TextBlock
                                            {
                                                Text = word.Content.Trim(),
                                                FontSize = 24
                                            };

                                            if (Song.SyncLevel >= 5 && word.Syllables?.Count > 0)
                                            {
                                                textBlock.Text = string.Empty;

                                                foreach (var syllable in word.Syllables)
                                                {
                                                    textBlock.Inlines.Add(new Run(syllable.Content));
                                                }
                                            }

                                            StackPanel stack = new StackPanel
                                            {
                                                Orientation = Orientation.Horizontal,
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

                                            if (Song.SyncLevel >= 5 && word.Syllables?.Count > 0)
                                            {
                                                textBlock.Text = string.Empty;

                                                foreach (var syllable in word.Syllables)
                                                {
                                                    textBlock.Inlines.Add(new Run(syllable.Content));
                                                }
                                            }

                                            StackPanel stack = new StackPanel
                                            {
                                                Orientation = Orientation.Horizontal,
                                                HorizontalAlignment = HorizontalAlignment.Center,
                                                Children = { textBlock }
                                            };

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
                                            line += $" {word.Content.Trim()}";

                                            StackPanel stack = lyricStack.Children[lyricStack.Children.Count - 1] as StackPanel;

                                            TextBlock textBlock = new TextBlock()
                                            {
                                                Text = $" {word.Content.Trim()}",
                                                FontSize = 24,
                                                Margin = new Thickness(0, 0, 1, 0)
                                            };

                                            if (Song.SyncLevel >= 5 && word.Syllables?.Count > 0)
                                            {
                                                textBlock.Text = string.Empty;

                                                foreach (var syllable in word.Syllables)
                                                {
                                                    textBlock.Inlines.Add(new Run(syllable.Content));
                                                }
                                            }

                                            stack.Children.Add(textBlock);
                                        }
                                    }

                                    int backingLinesAdded = 1;

                                    if (Song.SyncLevel >= 3 && Song.BackingVocals.Count > 0)
                                    {
                                        foreach (var word in Song.BackingVocals)
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
                                    }
                                });

                                while (progressTimer.ElapsedMilliseconds - progressOffset < Song.Duration * 1000)
                                {
                                    if (LeadVocals[index].Time.Start <= progressTimer.ElapsedMilliseconds - progressOffset)
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
                                                    if (LeadVocals[index].Syllables?.Count == 0)
                                                    {
                                                        text.Foreground = Brushes.White;

                                                        DoubleAnimation thing = new DoubleAnimation(25, TimeSpan.FromMilliseconds(Math.Max(LeadVocals[index].Time.End - LeadVocals[index].Time.Start, 0)));
                                                        thing.AutoReverse = true;
                                                        thing.EasingFunction = new SineEase();

                                                        text.BeginAnimation(TextBlock.FontSizeProperty, thing);

                                                        // We've reached the last word, move onto the next line
                                                        if (wordIndex + 1 == stack.Children.Count)
                                                        {
                                                            lineIndex++;
                                                            wordIndex = 0;
                                                        }
                                                        else
                                                            wordIndex++;

                                                        index++;
                                                    }
                                                    else if (syllableIndex == 0 || LeadVocals[index].Syllables?[syllableIndex].Time.Start - 300 <= progressTimer.ElapsedMilliseconds - progressOffset)
                                                    {
                                                        text.Inlines.ElementAt(syllableIndex).Foreground = Brushes.White;

                                                        DoubleAnimation thing = new DoubleAnimation(25, TimeSpan.FromMilliseconds(Math.Abs((syllableIndex == 0 ? LeadVocals[index].Syllables[syllableIndex].Time.End : LeadVocals[index].Time.End + 300) - (syllableIndex == 0 ? LeadVocals[index].Time.Start : LeadVocals[index].Syllables[syllableIndex].Time.Start))));
                                                        thing.AutoReverse = true;
                                                        thing.EasingFunction = new SineEase();

                                                        text.Inlines.ElementAt(syllableIndex).BeginAnimation(TextBlock.FontSizeProperty, thing);

                                                        // We've reached the last syllable of the word, move onto the next word
                                                        if(syllableIndex + 1 == text.Inlines.Count)
                                                        {
                                                            syllableIndex = 0;

                                                            // We've reached the last word, move onto the next line
                                                            if (wordIndex + 1 == stack.Children.Count)
                                                            {
                                                                lineIndex++;
                                                                wordIndex = 0;
                                                            }
                                                            else
                                                                wordIndex++;

                                                            index++;
                                                        }
                                                        else
                                                            syllableIndex++;
                                                    }
                                                }

                                                while (lineIndex < lyricStack.Children.Count && ((StackPanel)lyricStack.Children[lineIndex]).Children.OfType<UIElement>().Any(x => ((TextBlock)x).FontSize < 24))
                                                {
                                                    lineIndex++;
                                                }
                                            }
                                        });
                                    }

                                    if (Song.SyncLevel >= 3 && Song.BackingVocals.Count > backingVocalsIndex && Song.BackingVocals.ElementAt(backingVocalsIndex).Time.Start <= progressTimer.ElapsedMilliseconds - progressOffset)
                                    {
                                        await lyricStack.Dispatcher.BeginInvoke(() =>
                                        {
                                            StackPanel stack = lyricStack.Children[(int)Song.BackingVocals.ElementAt(backingVocalsIndex).LeadVocalAttatchment + backingVocalsLineIndex] as StackPanel;
                                            TextBlock text = stack.Children[backingVocalsWordIndex] as TextBlock;

                                            if (backingVocalsWordIndex < stack.Children.Count)
                                            {
                                                text.Foreground = Brushes.White;

                                                DoubleAnimation thing = new DoubleAnimation(17, TimeSpan.FromMilliseconds(LeadVocals[index].Time.End - LeadVocals[index].Time.Start));
                                                thing.AutoReverse = true;
                                                thing.EasingFunction = new SineEase();

                                                text.BeginAnimation(TextBlock.FontSizeProperty, thing);

                                                if (backingVocalsWordIndex + 1 == stack.Children.Count)
                                                {
                                                    backingVocalsLineIndex++;
                                                    backingVocalsWordIndex = 0;
                                                }
                                                else
                                                    backingVocalsWordIndex++;

                                                backingVocalsIndex++;
                                            }
                                        });
                                    }

                                    await progressText.Dispatcher.BeginInvoke((Action)(() => progressText.Text = (progressTimer.ElapsedMilliseconds - progressOffset).ToString()));
                                }

                                NavigationService.Navigate(new SearchPage());
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }

                startText.Visibility = Visibility.Visible;

                try
                {
                    Clipboard.SetText(Track.SyncedLyrics);
                }
                catch (Exception ex) { }
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

            string content = await File.ReadAllTextAsync($"Lyrics/{track.Id}.json");
            Song song = JsonConvert.DeserializeObject<Song>(content);
            song.SetTrack(track);
            Word word = song.Words.Dequeue();

            // int progress = 0;
            int progressOffset = 0;

            int lineIndex = 0;
            int wordIndex = 0;

            Stopwatch progressTimer = new Stopwatch();
            progressTimer.Start();


            /*progressTimer.Elapsed += (sender, e) =>
            {
                progress++;
                
            };*/

            // Account for initial delay, after 5 seconds get the actual Spotify progress instead of guessing
            System.Timers.Timer syncTimer = new System.Timers.Timer(TimeSpan.FromSeconds(5));
            syncTimer.Start();
            syncTimer.Elapsed += async (sender, e) =>
            {
                var playback = await client.Player.GetCurrentPlayback();
                progressOffset = (int)Math.Abs(progressTimer.ElapsedMilliseconds - playback.ProgressMs);

                syncTimer.Stop();
            };

            string currentLine = first.Split(']')[1][1..];

            timer.Elapsed += async (sender, e) =>
            {
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

            while (progressTimer.ElapsedMilliseconds + progressOffset < song.Duration * 1000)
            {
                if (progressTimer.ElapsedMilliseconds + progressOffset >= word.Time.Start)
                {
                    await lyricStack.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        if (lyricStack.Children.Count > lineIndex)
                        {
                            TextBlock text = lyricStack.Children[lineIndex] as TextBlock;

                            if (wordIndex < text.Inlines.Count)
                                text.Inlines.ElementAt(wordIndex).Foreground = Brushes.White;

                            if (wordIndex + 1 == text.Inlines.Count)
                            {
                                lineIndex++;
                                wordIndex = 0;
                            }
                            else
                                wordIndex++;

                            word = song.Words.Dequeue();

                            while (word.Content == "\n")
                            {
                                word = song.Words.Dequeue();
                            }
                        }
                    }));
                }

                await timeText.Dispatcher.BeginInvoke((Action)(() => { double minute = Math.Floor((double)(progressTimer.ElapsedMilliseconds + progressOffset) / 60000); timeText.Text = $"{minute}:{Math.Floor((double)(((progressTimer.ElapsedMilliseconds + progressOffset) / 60000) % 1) * 100)}"; }));
                await wordTestThing.Dispatcher.BeginInvoke((Action)(() => wordTestThing.Text = $"{word.Time.Start} - {word.Content}"));
                await progressText.Dispatcher.BeginInvoke((Action)(() => progressText.Text = (progressTimer.ElapsedMilliseconds + progressOffset).ToString()));
            }
        }

        /*private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
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

    internal struct LrcLine
    {
        public int Minute { get; set; }
        public int Second { get; set; }
        public int Milisecond { get; set; }
    }
}