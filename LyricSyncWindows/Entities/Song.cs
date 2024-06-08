using HandyControl.Tools.Extension;
using LRCLibNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LyricSyncWindows.Entities
{
    public class Song
    {
        public int Id { get => internalTrack.Id; }

        public string Title { get => internalTrack.Title; }
        public string Artist { get => internalTrack.Artist; }
        public string Album { get => internalTrack.Album; }

        public int Duration { get => (int)internalTrack.Duration * 1000; }

        public Queue<Word> Words { get; set; } = new Queue<Word>();
        public Queue<Word> BackingVocals { get; set; } = new Queue<Word>();

        [JsonProperty("SyncLevel")]
        public int SyncLevel { get; set; } = 1;

        public Song() { }

        public Song(LrcTrack track)
        {
            internalTrack = track;

            if (track != null)
            {
                var lines = track.Lyrics.Split('\n').ToList();
                lines.RemoveAll(line => string.IsNullOrWhiteSpace(line));

                if (lines.Any(line => line.Contains('(')))
                {
                    // There are backing vocals

                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i].Contains('('))
                            continue;

                        Word word = new Word
                        {
                            LineStart = true
                        };

                        var split = lines[i].Split('(');
                        var splitAfter = lines[i].Split(')');

                        string line = string.Empty;

                        if (split.Length == 2)
                        {
                            string lineBefore = split[1];
                            line = lineBefore.Split(')')[0];

                            if (string.IsNullOrWhiteSpace(split[0]) && string.IsNullOrWhiteSpace(splitAfter[1]))
                                word.IsAlone = true;
                            else
                                word.IsAlone = false;
                        }
                        else
                        {
                            // (Hey!) I don't know about you (I don't know about you) but I'm feeling 22

                            string first = split[1];
                            string second = first.Split(')')[0]; // Hey!
                            string third = split[2];
                            string fourth = third.Split(')')[0]; // I don't know about you

                            line = $"{second} {fourth}"; // Hey! I don't know about you
                            word.IsAlone = false;
                        }

                        BackingVocals.Enqueue(word.WithContent(line.Split(' ')[0].Trim()).WithLeadVocalAttatchment(i));

                        foreach (var content in line.Split(' '))
                        {
                            if (content.Equals(word.Content))
                                continue;

                            Word thingWord = new Word().WithContent(string.Join("", content.Split("\n")).Trim()).WithLeadVocalAttatchment(i);

                            if (string.IsNullOrWhiteSpace(split[0]) && string.IsNullOrWhiteSpace(splitAfter[1]))
                                thingWord.IsAlone = true;
                            else
                                thingWord.IsAlone = false;

                            BackingVocals.Enqueue(thingWord);
                        }

                        if (split.Length == 2)
                        {
                            string outputBefore = split[0].Trim();
                            string outputAfter = splitAfter[1];

                            lines[i] = $"{outputBefore} {outputAfter}";
                        }
                        else
                        {
                            string first = split[0].Trim(); // ""
                            string second = splitAfter[1]; // I don't know about you (I don't know about you
                            string third = splitAfter[2]; // but I'm feeling 22

                            lines[i] = $"{first} {second.Split('(')[0]} {third}".Trim();
                        }
                    }
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    Regex regex = new Regex("[ ]{2,}", RegexOptions.None);
                    lines[i] = regex.Replace(lines[i], " ");
                    lines[i] = lines[i].Trim();

                    Word word = new Word
                    {
                        LineStart = true
                    };

                    var words = lines[i].Split(' ').ToList();

                    if (words.Count == 1)
                        word.LineEnd = true;

                    Words.Enqueue(word.WithContent(words[0].Trim()));

                    for (int j = 0; j < words.Count; j++)
                    {
                        string content = words[j];

                        if (content.Equals(word.Content))
                            continue;

                        Word newWord = new Word().WithContent(content.Trim());

                        if (j == words.Count - 1)
                            newWord.LineEnd = true;

                        Words.Enqueue(newWord);
                    }
                }
            }
        }

        public Song SetTrack(LrcTrack track)
        {
            internalTrack = track;
            return this;
        }

        private LrcTrack internalTrack;

        public void UpdateSyncLevel() => SyncLevel++;
        public void UpdateSyncLevel(int syncType) => SyncLevel = syncType;
    }

    public enum SyncType
    {
        None,
        Line,
        Word,
        WordBacked,
        Syllable,
        SyllableBacked
    }
}
