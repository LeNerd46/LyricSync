using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricSyncWindows.Entities
{
    public class Word
    {
        public class SyncTime
        {
            public int Start { get; set; }
            public int End { get; set; }
        }

        public class Syllable
        {
            public SyncTime Time { get; set; } = new SyncTime();
            public string Content { get; set; }
        }

        public SyncTime Time { get; set; } = new SyncTime();
        public string Content { get; set; }

        public List<Syllable> Syllables { get; set; } = new List<Syllable>();

        public bool LineStart { get; set; }
        public bool LineEnd { get; set; }

        /// <summary>
        /// The index of the line this backing vocal is attached to
        /// </summary>
        public int? LeadVocalAttatchment { get; set; }
        public bool IsAlone { get; set; }

        public Word WithContent(string content)
        {
            Content = content;
            return this;
        }

        public Word WithLeadVocalAttatchment(int index)
        {
            LeadVocalAttatchment = index;
            return this;
        }
    }
}
