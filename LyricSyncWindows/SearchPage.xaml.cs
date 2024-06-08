using LRCLibNet;
using LyricSyncWindows.Entities;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LyricSyncWindows
{
    /// <summary>
    /// Interaction logic for SearchPage.xaml
    /// </summary>
    public partial class SearchPage : Page
    {
        public LrcClient Client { get; set; }

        public ObservableCollection<LrcTrack> Tracks { get; set; }

        public SearchPage()
        {
            Tracks = new ObservableCollection<LrcTrack>();

            InitializeComponent();
            DataContext = this;

            Client = new LrcClient();
            lyricsList.ItemsSource = Tracks;

            Task.Run(async () =>
            {
                if (Directory.Exists("Lyrics"))
                {
                    foreach (var file in Directory.GetFiles("Lyrics"))
                    {
                        string name = System.IO.Path.GetFileName(file);
                        LrcTrack track = await Client.GetLyricsAsync(int.Parse(name.Split('.')[0]));

                        Tracks.Add(track);
                    }
                }
            });
        }

        private async void searchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Tracks.Clear();

                if (string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    if(Directory.Exists("Lyrics"))
                    {
                        foreach (var file in Directory.GetFiles("Lyrics"))
                        {
                            string name = System.IO.Path.GetFileName(file);
                            LrcTrack track = await Client.GetLyricsAsync(int.Parse(name.Split('.')[0]));

                            Tracks.Add(track);
                        }
                    }

                    return;
                }

                var tracks = await Client.SearchTrackAsync(searchBox.Text);

                foreach (var track in tracks)
                {
                    Tracks.Add(track);
                }
            }
        }

        private void lyricsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lyricsList.SelectedItem is LrcTrack track)
            {
                string path = File.Exists($"Lyrics/{track.Id}.json") ? $"Lyrics/{track.Id}.json" : null;

                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("No lyrics found for this song.");
                    return;
                }

                Task.Run(async () =>
                {
                    SearchResponse response = await MainWindow.Spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, $"{track.Title} {track.Artist}"));
                    FullTrack spotifyTrack = response.Tracks.Items.First();

                    await MainWindow.Spotify.Player.AddToQueue(new PlayerAddToQueueRequest(spotifyTrack.Uri.ToString()));
                });
                
                NavigationService.Navigate(new LyricSync(track));
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            LrcTrack track = lyricsList.SelectedItem as LrcTrack;
            NavigationService.Navigate(new EditSync(track));
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            LrcTrack track = lyricsList.SelectedItem as LrcTrack;

            if (File.Exists($"Lyrics/{track.Id}.json"))
                NavigationService.Navigate(new SyncEditor(JsonConvert.DeserializeObject<Song>(File.ReadAllText($"Lyrics/{track.Id}.json")).SetTrack(track)));
            else
                MessageBox.Show("No sync data found for this song");
        }
    }
}