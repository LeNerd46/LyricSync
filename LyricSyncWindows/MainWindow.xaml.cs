using LRCLibNet;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Text;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow instance;

        public static SpotifyClient Spotify { get; set; }
        internal string verification;
        internal HttpListener listener;

        public MainWindow()
        {
            var (verifier, challenge) = PKCEUtil.GenerateCodes();
            verification = verifier;

            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5543/callback/");
            listener.Start();

            Task.Run(() => ListenForCallback());

            var loginRequest = new LoginRequest(new Uri("http://localhost:5543/callback"), "cc8f2753503c49d1a3edd49e55c71001", LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = new[] { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackPosition, Scopes.UserReadPlaybackState }
            };

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = loginRequest.ToUri().ToString(), UseShellExecute = true });

            while (Spotify == null) ;

            instance ??= this;

            InitializeComponent();

            MainFrame.Navigate(new HomePage());
        }

        private async Task ListenForCallback()
        {
            try
            {
                var context = await listener.GetContextAsync();
                var code = context.Request.QueryString.Get("code");

                if(!string.IsNullOrWhiteSpace(code))
                {
                    var responseBytes = Encoding.UTF8.GetBytes("<html><body>Authentication successful! You can close this window now.<script>close()</script></body></html>");
                    context.Response.ContentType = "text/html";
                    context.Response.ContentLength64 = responseBytes.Length;
                    await context.Response.OutputStream.WriteAsync(responseBytes);

                    var response = await new OAuthClient().RequestToken(new PKCETokenRequest("cc8f2753503c49d1a3edd49e55c71001", code, new Uri("http://localhost:5543/callback"), verification));

                    var authenticator = new PKCEAuthenticator("cc8f2753503c49d1a3edd49e55c71001", response);

                    var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
                    Spotify = new SpotifyClient(config);

                    context.Response.Close();
                }
            }
            catch(HttpListenerException) { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            listener.Stop();
            listener.Close();
        }

        private void homeButton_Click(object sender, RoutedEventArgs e)
        {
            homeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2e2e2e"));
            searchButton.Background = new SolidColorBrush(Colors.Transparent);
            contributeButton.Background = new SolidColorBrush(Colors.Transparent);

            MainFrame.Navigate(new HomePage());
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            homeButton.Background = new SolidColorBrush(Colors.Transparent);
            searchButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2e2e2e"));
            contributeButton.Background = new SolidColorBrush(Colors.Transparent);

            MainFrame.Navigate(new SearchPage());
        }

        private void contributeButton_Click(object sender, RoutedEventArgs e)
        {
            homeButton.Background = new SolidColorBrush(Colors.Transparent);
            searchButton.Background = new SolidColorBrush(Colors.Transparent);
            contributeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2e2e2e"));
        }

        private void editorDoneButton_Click(object sender, RoutedEventArgs e)
        {
            SyncEditor page = MainFrame.Content as SyncEditor;

            if (page is SyncEditor editor)
            {
                editor.Song.UpdateSyncLevel();

                File.WriteAllText($"Lyrics/{editor.Song.Id}.json", JsonConvert.SerializeObject(editor.Song));

                editorDoneButton.Visibility = Visibility.Hidden;
                MainFrame.Navigate(new HomePage());
            }
            else
                MessageBox.Show("Unable to get editor page");
        }
    }
}