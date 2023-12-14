namespace AVXFramework
{
    using Blueprint.Blue;
    using Pinshot.Blue;
    using AVSearch;

    public class AVEngine
    {
        private Blueprint QuelleModel;
        private PinshotLib QuelleParser;
        private readonly Guid ClientId;
        private const string SDK = "C:/src/AVX/omega/AVX-Omega-3911.data";

#if USE_NATIVE_LIBRARIES
        private NativeStatement SearchEngine;
#else
        private AVQueryManager SearchEngine;
#endif

        public AVEngine()
        {
            this.QuelleModel = new();
            this.QuelleParser = new();
            this.ClientId = Guid.NewGuid();

#if USE_NATIVE_LIBRARIES
            this.SearchEngine = new NativeStatement(SDK);
#else
            this.SearchEngine = new();
#endif        
    }
    public void Release()
        {
#if USE_NATIVE_LIBRARIES
            this.SearchEngine.Release();
#else
            this.SearchEngine.ReleaseAll(this.ClientId);
#endif
        }
        ~AVEngine()
        {
            this.Release();
        }
        public (QStatement? stmt, string error, string result) Execute(string command)
        {
            var pinshot = this.QuelleParser.Parse(command);
            if (pinshot.root != null)
            {
                if (string.IsNullOrWhiteSpace(pinshot.root.error))
                {
                    var blueprint = this.QuelleModel.Create(pinshot.root);

                    if (blueprint != null)
                    {
                        if (blueprint.IsValid)
                        {
                            if (blueprint.Singleton != null)
                            {
                                ; // process singleton command
                                return (blueprint, "", "Pretend that this is the result of an executed Quelle command");
                            }
                            else if (blueprint.Commands != null)
                            {
                                var expression = blueprint.Commands.Searches.ToList();
                                string yaml = ICommand.YamlSerializerRaw(expression);
                                string json = ICommand.JsonSerializerRaw(expression);
                                
                                // brute-force pretty-print (for debugging)
                                var json_pretty = json
                                    .Replace("}], \"Text\":",  "}],\n\tText:")
                                    .Replace("}]}]", "  }]}]").Replace("}],", "  }],")
                                    .Replace("\"IsQuoted\":",   "\n\tIsQuoted:")
                                    .Replace("Fragments\":",    "\n\tFragments:")
                                    .Replace("\"MatchAll\":",   "\n\t\tMatchAll:")
                                    .Replace("\"AnyFeature\":", "\n\t\t\tAnyFeature:")
                                    .Replace("\"Type\":",       "\n\t\t\t\tType:")
                                    .Replace("\"WordKeys\":",   "\n\t\t\t\tWordKeys:")
                                    .Replace("\"Phonetics\":",  "\n\t\t\t\tPhonetics:")
                                    .Replace("\"Negate\":",     "\n\t\t\t\tNegate:")
                                    .Replace("\"Text\":",       "\n\t\t\t\tText:")
                                    .Replace("\"Anchored\":",   "\n\t\tAnchored:")
                                    .Replace("\"Verb\":",       "\n\tVerb:")
                                    .Replace("\"find\"}]",      "\"find\"\n}]");

                                Console.Write("Specify yaml and/or json display (default is none) > ");
                                var answer = Console.ReadLine().ToLower();

                                if (answer.Contains("yaml") || answer.Contains("both"))
                                {
                                    Console.WriteLine("YAML:");
                                    Console.WriteLine(yaml);
                                }
                                if (answer.Contains("json") || answer.Contains("both"))
                                {
                                    Console.WriteLine("JSON:");
                                    Console.WriteLine(json_pretty);
                                }

                                QSettings qsettings = blueprint.LocalSettings;
                                TSettings settings = new TSettings(in qsettings);

                                List<(byte book, byte chapter, byte verse)> scope = new();

                                TQuery query = this.SearchEngine.Create(in this.ClientId, in blueprint);
                            }
                            else
                            {
                                return (blueprint, "Internal Error: Unexpected blueprint encountered.", "");
                            }
                        }
                        else
                        {
                            if (blueprint.Errors.Count > 0)
                            {
                                var errors = string.Join("; ", blueprint.Errors);
                                return (blueprint, errors, "");
                            }
                            else
                            {
                                return (blueprint, "Blueprint was invalid, but the error list was empty.", "");
                            }
                        }
                    }
                    else
                    {
                        return (blueprint, "Blueprint was invalid (unexpected error).", "");
                    }
                }
                else
                {
                    return (null, pinshot.root.error, "");
                }
            }
            return (null, "Unable to parse the statement.", "");
        }
    }
}