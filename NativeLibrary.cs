namespace AVXFramework
{
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Unicode;
    using YamlDotNet.Core.Tokens;

    public class NativeLibrary
    {
        // AVX-Search:
        [DllImport("AVXSearch.dll", CharSet = CharSet.Ansi)]
        private static extern UInt64 query_create(UInt64 client_id_1, UInt64 client_id_2, string blueprint, UInt16 span, byte lexicon, byte similarity, byte fuzzy_lemmata);
        [DllImport("AVXSearch.dll")]
        private static extern byte query_add_scope(UInt64 client_id_1, UInt64 client_id_2, UInt64 query_id, byte book, byte chapter, byte verse);
        [DllImport("AVXSearch.dll", CharSet = CharSet.Ansi)]
        private static extern string query_fetch(UInt64 client_id_1, UInt64 client_id_2, UInt64 query_id);
        [DllImport("AVXSearch.dll", CharSet = CharSet.Ansi)]
        private static extern string chapter_fetch(UInt64 client_id_1, UInt64 client_id_2, UInt64 query_id, byte book);
        [DllImport("AVXSearch.dll")]
        private static extern void client_release(UInt64 client_id_1, UInt64 client_id_2);
        [DllImport("AVXSearch.dll")]
        private static extern void query_release(UInt64 client_id_1, UInt64 client_id_2, UInt64 query_id);

        private UInt64 ClientId_1
        {
            get
            {
                byte[] bytes = this.ClientId.ToByteArray();
                UInt64 result = 0;

                for (int i = 0; i < 8; i++)
                {
                    result <<= 8;
                    result |= bytes[i];
                }
                return result;
            }
        }
        private UInt64 ClientId_2
        {
            get
            {
                byte[] bytes = this.ClientId.ToByteArray();
                UInt64 result = 0;

                for (int i = 8; i < 16; i++)
                {
                    result <<= 8;
                    result |= bytes[i];
                }
                return result;
            }
        }
        private Guid ClientId;
        internal NativeLibrary()
        {
            this.ClientId = new Guid();
        }
        internal UInt64 query_create(string blueprint, UInt16 span, byte lexicon, byte similarity, byte fuzzy_lemmata)
        {
            var result = NativeLibrary.query_create(this.ClientId_1, this.ClientId_2, blueprint, span, lexicon, similarity, fuzzy_lemmata);
            return result;
        }
        internal string fetch_results(UInt64 query_id, byte book)
        {
            return NativeLibrary.chapter_fetch(this.ClientId_1, this.ClientId_2, query_id, book);
        }
        internal bool query_add_scope(UInt64 query_id, byte book, byte chapter, byte verse)
        {
            return NativeLibrary.query_add_scope(this.ClientId_1, this.ClientId_2, query_id, book, chapter, verse) == (byte)1 ? true : false;
        }
        internal string fetch_summary(UInt64 query_id)
        {
            return NativeLibrary.query_fetch(this.ClientId_1, this.ClientId_2, query_id);
        }
        internal void client_release()
        {
            NativeLibrary.client_release(this.ClientId_1, this.ClientId_2);
        }
        internal void query_release(UInt64 queryId) // QueryId is in the payload returned by create_query
        {
            NativeLibrary.query_release(this.ClientId_1, this.ClientId_2, queryId);
        }

        // AVX-Text
        [DllImport("AVXSearch.dll")]
        internal static extern UInt64 create_avxtext(byte[] path);

        [DllImport("AVXSearch.dll")]
        internal static extern void free_avxtext(UInt64 data);
    }

    public class NativeStatement
    {
        private UInt64 AVXTextData;
        private NativeLibrary External;
        private UInt64 Address;
        public string Summary { get; private set; } // This is the YAML representation of the TQuery object

        public NativeStatement(string omega)  // (full path-spec to omega file)
        {
            byte[] omega_utf8 = System.Text.Encoding.UTF8.GetBytes(omega);
            this.AVXTextData = NativeLibrary.create_avxtext(omega_utf8);
            this.External = new NativeLibrary();
            this.Address = 0; // we need to extract this from the yaml/result
            this.Summary = string.Empty;
        }
        public bool Search(string blueprint, UInt16 span, byte lexicon, byte similarity, bool fuzzy_lemmata, List<(byte book, byte chapter, byte verse)> scope)
        {
            this.Free();
            this.Summary = string.Empty;

            this.Address = this.External.query_create(blueprint, span, lexicon, similarity, fuzzy_lemmata ? (byte)1 : (byte)0);

            if (this.Address != 0)
            {
                foreach (var spec in scope)
                {
                    this.External.query_add_scope(this.Address, spec.book, spec.chapter, spec.verse);
                }
                this.Summary = this.External.fetch_summary(this.Address);

                return (this.Address != 0 && !string.IsNullOrWhiteSpace(this.Summary));
            }
            return false;
        }
        public string Fetch(UInt64 client_id, byte book)
        {
            this.Summary = this.External.fetch_results(this.Address, book);

            return this.Summary;
        }
        public void Free()
        {
            if (this.Address != 0)
                this.External.query_release(this.Address);
            this.Address = 0;
        }
        public void Release()
        {
            this.Free();
            NativeLibrary.free_avxtext(this.AVXTextData);
        }
        ~NativeStatement()
        {
            this.Release();
        }
    }
    public class NativeText
    {
        private UInt64 address;
        public NativeText(byte[] path) 
        {
            this.address = NativeLibrary.create_avxtext(path);
        }
        public void Free()
        {
            if (this.address != 0)
                NativeLibrary.free_avxtext(this.address);
            this.address = 0;
        }
        ~NativeText()
        {
            this.Free();
        }
    }
}
