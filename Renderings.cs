using AVXLib;
using AVXLib.Memory;
using Blueprint.Model.Implicit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVXFramework
{
    public class ChapterRendering
    {
        public string BookName;
        public string BookAbbreviation3;
        public string BookAbbreviation4;
        public byte BookNumber;
        public byte ChapterNumber;
        public Dictionary<byte, VerseRendering> Verses;

        public ChapterRendering(byte b, byte c)
        {
            this.BookNumber = b;
            this.ChapterNumber = c;
            this.Verses = new();

            if (b >= 1 && b <= 66)
            {
                Book book = ObjectTable.AVXObjects.Mem.Book.Slice(this.BookNumber, 1).Span[0];

                this.BookName = book.name.ToString();
                this.BookAbbreviation3 = book.abbr3.ToString();
                this.BookAbbreviation4 = book.abbr4.ToString();

                if (this.ChapterNumber > book.chapterCnt)
                    this.ChapterNumber = 1;
            }
            else
            {
                this.BookNumber = 0;
                this.ChapterNumber = 0;
                this.BookName = string.Empty;
                this.BookAbbreviation3 = string.Empty;
                this.BookAbbreviation4 = string.Empty;
            }
        }
    }
    public class VerseRendering
    {
        public BCVW Coordinates;
        public WordRendering[] Words;

        public VerseRendering(byte b, byte c, byte v, byte wc)
        {
            this.Coordinates = new(b, c, v, wc);
            this.Words = new WordRendering[wc];
        }
        public VerseRendering(BCVW coordinates)
        {
            this.Coordinates = coordinates;
            this.Words = new WordRendering[coordinates.WC];
        }
    }
    public class WordRendering
    {
        public BCVW Coordinates;
        public string Text;   // KJV
        public string Modern; // AVX
        public byte Punctuation;
        public Dictionary<UInt32, string> Triggers;       // <highlight-id, feature-match-string>
        public Dictionary<UInt32, UInt16> HighlightSpans; // <highlight-id, span-length>

        public WordRendering()
        {
            this.Coordinates = new();
            this.Text = string.Empty;
            this.Modern = string.Empty;
            this.Punctuation = 0;
            this.Triggers = new();
        }
    }
    public class SoloVerseRendering: VerseRendering
    {
        public string BookName;
        public string BookAbbreviation3;
        public string BookAbbreviation4;
        public byte BookNumber;
        public byte ChapterNumber;

        public SoloVerseRendering(VerseRendering baseclass) : base(baseclass.Coordinates)
        {
            this.Words = baseclass.Words;

            if (this.Words.Length > 0)
            {
                this.BookNumber = this.Words[0].Coordinates.B;
                this.ChapterNumber = this.Words[0].Coordinates.C;

                Book book = ObjectTable.AVXObjects.Mem.Book.Slice(this.BookNumber, 1).Span[0];

                this.BookName = book.name.ToString();
                this.BookAbbreviation3 = book.abbr3.ToString();
                this.BookAbbreviation4 = book.abbr4.ToString();
            }
            else
            {
                this.BookNumber = 0;
                this.ChapterNumber = 0;
                this.BookName = string.Empty;
                this.BookAbbreviation3 = string.Empty;
                this.BookAbbreviation4 = string.Empty;
            }
        }
    }
}
