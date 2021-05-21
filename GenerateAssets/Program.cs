using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GenerateAssets
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var serviceProvider = new ServiceCollection()
             .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
             .BuildServiceProvider();

            Logger.Current = serviceProvider.GetService<ILoggerFactory>().CreateLogger<Program>();

            var bag = new Dictionary<string, object>()
                .FetchGpcrDbResidueTableHtml()
                .ParseGpcrDbNumberingSchemeIds()
                .SetGpcrDbNumberingSchemes();

            var schemes = bag.GetGpcrDbNumberingSchemes();
            string[][] schemeNames = schemes.Select(o => new[] { o.Name, null }).ToArray();

            var rs = bag
                .FetchGpcrDbHtml()
                .ParseGpcrDbStructures()
                .GetGpcrDbPdbList();

            var pdbDict = new List<(string pdb_id, string uniprot_id, string symbol_species, string symbol, string species, string lspecies, string gene_name)>();
            var numberingDict = new Dictionary<string, Dictionary<int, List<string>>>();
            var residueNameDict = new Dictionary<string, Dictionary<int, char>>();
            bool first = true;

            if (!Directory.Exists("dbcache"))
                Directory.CreateDirectory("dbcache");

            // for the first 10 schemes
            foreach (var pdb in rs)
            {
                string geneName = bag.SetUniProtId(pdb.UniProtId).FetchUniProtHtml().ParseHgncIdAndGeneName().ParseProteinName().GetGeneName();
                string species = bag.GetSpecies();

                string symbol = pdb.ProteinName;
                if (symbol == pdb.UniProtId)
                    symbol = geneName;

                Logger.Current.LogInformation($"Working on {pdb.UniProtId}, {pdb.PdbEntry}, {symbol}, {species}({pdb.Species}), {pdb.Iuphar}, {geneName}");

                pdbDict.Add((pdb.PdbEntry.ToUpper(), pdb.UniProtId.ToUpper(), $"{symbol}_{species}".ToUpper(), symbol.ToUpper(), species.ToUpper(), pdb.Species, geneName.ToUpper()));

                List<(string Name, string ShortName)> headers = null;

                var headerfile = Path.Combine("dbcache", "headers.json");
                if (File.Exists(headerfile))
                    headers = JsonConvert.DeserializeObject<List<(string Name, string ShortName)>>(File.ReadAllText(headerfile));

                if (!numberingDict.ContainsKey(pdb.UniProtId))
                {
                    Logger.Current.LogInformation($"Fetching numbering for {pdb.UniProtId}");

                    Dictionary<int, List<string>> numberings;
                    Dictionary<int, char> residueNames = null;

                    var cachefile = Path.Combine("dbcache", $"{pdb.UniProtId}.json");

                    if (File.Exists(cachefile))
                    {
                        (residueNames, numberings) = JsonConvert.DeserializeObject<(Dictionary<int, char>, Dictionary<int, List<string>>)>(File.ReadAllText(cachefile));
                    }
                    else
                    {
                        numberings = new Dictionary<int, List<string>>();

                        bool writeHeaderFile = headers == null;
                        if (writeHeaderFile)
                            headers = new List<(string Name, string ShortName)>();

                        foreach (var scheme in schemes)
                        {
                            bag.Set("GpcrDbNumberingSchemes", new[] { scheme }.ToList());
                            var subnum = bag
                                .FetchGpcrDbResidueTableDisplayHtmlByUniProtId($"{pdb.ProteinName}_{species}")
                                .ParseGpcrDbResidueTableDisplayHtml()
                                .FindGpcrDbResidueNumberingIndices()
                                .GetGpcrDbResidueNumberings();

                            if (numberings.Count == 0)
                            {
                                foreach (var (key, list) in subnum)
                                    numberings.Add(key, new List<string>(list));
                            }
                            else
                            {
                                if (subnum.Count != numberings.Count)
                                    Logger.Current.LogError($"Unmatched length of numbering for {scheme.Name}");

                                var oldlen = numberings.Count;
                                foreach (var (key, list) in subnum)
                                    numberings[key].Add(list[0]);

                                if (oldlen != numberings.Count)
                                    Logger.Current.LogError($"Inconsistent numbering for {scheme.Name}");
                            }

                            var header = bag.GetGpcrDbResidueNumberingHeaders();
                            headers.Add(header[0]);
                        }

                        if (writeHeaderFile)
                            File.WriteAllText(headerfile, JsonConvert.SerializeObject(headers));

                        var resname = bag.GetGpcrDbResidueNames();
                        if (residueNames == null)
                        {
                            residueNames = resname;
                        }
                        else
                        {
                            if (resname.Count != residueNames.Count)
                                Logger.Current.LogError($"Unmatched residue length for {headers.Last().Name}");

                            var oldlen = residueNames.Count;
                            foreach (var (key, name) in resname)
                                if (residueNames[key] != name)
                                    Logger.Current.LogError($"Unmatched residue sequence for {headers.Last().Name}");

                            if (oldlen != residueNames.Count)
                                Logger.Current.LogError($"Inconsistent residue sequence for {headers.Last().Name}");
                        }

                        File.WriteAllText(cachefile, JsonConvert.SerializeObject((residueNames, numberings)));
                    }

                    numberingDict[pdb.UniProtId] = numberings;

                    residueNameDict[pdb.UniProtId] = residueNames;
                }

                if (first && headers.Count > 0)
                {
                    for (int i = 0; i < headers.Count; i++)
                        schemeNames[i][1] = headers[i].ShortName;
                    first = false;
                }
            }

            // measure widths of numbering strings
            int[] maxWidths = numberingDict
                .SelectMany(o =>
                    o.Value.Values.Select(numberings =>
                        numberings.Select(str => str.Length).ToArray()
                    )
                )
                .Aggregate((a, b) =>
                    a.Select((c, i) => Math.Max(c, b[i])).ToArray()
                )
                .ToArray();

            int[] offsets = new int[maxWidths.Length];
            for (int i = 1; i < offsets.Length; i++)
                offsets[i] = maxWidths[i - 1] + offsets[i - 1];

            Logger.Current.LogInformation($"MaxWidths for numberings: {string.Join(',', maxWidths)}");

            // transform numbering string list into a flatten one
            var flatNumberingDict = numberingDict.ToDictionary(
                o => o.Key,
                o => o.Value.ToDictionary(
                    p => p.Key,
                    p => string.Join("", p.Value.Select((str, idx) => str.PadRight(maxWidths[idx])))
                )
            );

            var strings = flatNumberingDict
                .SelectMany(o => o.Value.Values)
                .Distinct()
                .Select((o, i) => new { str = o, index = i })
                .ToDictionary(o => o.str, o => o.index);

            // output buffers
            var outCpp = new StringBuilder();
            var outHpp = new StringBuilder();

            outHpp.AppendLine("#include <map>");
            outHpp.AppendLine("#include <string>");
            outHpp.AppendLine("#include <vector>");
            outHpp.AppendLine("#include <array>");
            outHpp.AppendLine("#include <tuple>");
            outHpp.AppendLine("using namespace std;");
            outHpp.AppendLine();

            outCpp.AppendLine("#include \"assets.hpp\"");
            outCpp.AppendLine();

            // string table
            outHpp.AppendLine("// string_index => string");
            outHpp.AppendLine($"extern array<string, {strings.Count}> string_table;");
            outHpp.AppendLine();

            outCpp.AppendLine("// string_index => string");
            outCpp.AppendLine($"array<string, {strings.Count}> string_table =");
            outCpp.AppendLine("{{");
            outCpp.AppendLine($"//   {string.Join("", maxWidths.Select(o => "v".PadRight(o)))}");
            foreach (var (str, index) in strings)
                outCpp.AppendLine($"\t\"{str}\",");
            outCpp.AppendLine($"//   {string.Join("", maxWidths.Select(o => "^".PadRight(o)))}");
            outCpp.AppendLine("}};");
            outCpp.AppendLine();

            // schemes
            outHpp.AppendLine("// scheme_index => scheme_numbering_offset, scheme_numbering_length, scheme_names");
            outHpp.AppendLine($"extern array<tuple<size_t, size_t, vector<string>>, {schemeNames.Length}> schemes;");
            outHpp.AppendLine();

            outCpp.AppendLine("// scheme_index => scheme_numbering_offset, scheme_numbering_length, scheme_names");
            outCpp.AppendLine($"array<tuple<size_t, size_t, vector<string>>, {schemeNames.Length}> schemes =");
            outCpp.AppendLine("{{");
            for (int i = 0; i < schemeNames.Length; i++)
            {
                string[] names = schemeNames[i];
                string sname = new string(names[0].ToUpper().Split(" ()-".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(o => o != "CLASS" && o != "SCHEME").Select(o => o[0]).ToArray());
                names[1] = names[1].Replace("(", "").Replace(")", "");
                if (sname != names[1])
                    outCpp.AppendLine($"\t{{{offsets[i]}, {(offsets[i] < 10 ? " " : "")}{maxWidths[i]}, {{\"{names[0]}\", \"{names[1]}\", \"{sname}\"}}}},");
                else
                    outCpp.AppendLine($"\t{{{offsets[i]}, {(offsets[i] < 10 ? " " : "")}{maxWidths[i]}, {{\"{names[0]}\", \"{names[1]}\"}}}},");
            }
            outCpp.AppendLine("}};");
            outCpp.AppendLine();

            // uniprot_id => protein_species
            outHpp.AppendLine("// uniprot_id => [symbol, species, gene_name, long_species]");
            outHpp.AppendLine("extern map<string, tuple<string, string, string, string>> uniprot_dict;");
            outHpp.AppendLine();

            outCpp.AppendLine("// uniprot_id => [symbol, species, gene_name, long_species]");
            outCpp.AppendLine("map<string, tuple<string, string, string, string>> uniprot_dict =");
            outCpp.AppendLine("{");
            foreach (var (uniprot_id, symbol, species, gene_name, lspecies) in pdbDict.Select(o => (o.uniprot_id, o.symbol, o.species, o.gene_name, o.lspecies)).Distinct())
                outCpp.AppendLine(
                    $"\t{{\"{uniprot_id}\", {"".PadRight(pdbDict.Max(o => o.uniprot_id.Length) - uniprot_id.Length)}" +
                    $"{{" +
                    $"\"{symbol}\", {"".PadRight(pdbDict.Max(o => o.symbol.Length) - symbol.Length)}" +
                    $"\"{species}\", {"".PadRight(pdbDict.Max(o => o.species.Length) - species.Length)}" +
                    $"\"{gene_name}\", {"".PadRight(pdbDict.Max(o => o.gene_name.Length) - gene_name.Length)}" +
                    $"\"{lspecies}\"" +
                    $"}}}},");
            outCpp.AppendLine("};");
            outCpp.AppendLine();

            // symbol => uniprot_id
            outHpp.AppendLine("// symbol => uniprot_id");
            outHpp.AppendLine("extern multimap<string, string> symbol_dict;");
            outHpp.AppendLine();

            outCpp.AppendLine("// symbol => uniprot_id");
            outCpp.AppendLine("multimap<string, string> symbol_dict =");
            outCpp.AppendLine("{");
            foreach (var (symbol, uniprot_id) in pdbDict.Select(o => (o.symbol, o.uniprot_id)).Distinct())
                outCpp.AppendLine($"\t{{\"{symbol}\", {"".PadRight(pdbDict.Max(o => o.symbol.Length) - symbol.Length)}\"{uniprot_id}\"}},");
            outCpp.AppendLine("};");
            outCpp.AppendLine();

            // symbol_species => uniprot_id
            outHpp.AppendLine("// symbol_species => uniprot_id");
            outHpp.AppendLine("extern map<string, string> symbol_species_dict;");
            outHpp.AppendLine();

            outCpp.AppendLine("// symbol_species => uniprot_id");
            outCpp.AppendLine("map<string, string> symbol_species_dict =");
            outCpp.AppendLine("{");
            foreach (var (symbol_species, uniprot_id) in pdbDict.Select(o => (o.symbol_species, o.uniprot_id)).Distinct())
                outCpp.AppendLine($"\t{{\"{symbol_species}\", {"".PadRight(pdbDict.Max(o => o.symbol_species.Length) - symbol_species.Length)}\"{uniprot_id}\"}},");
            outCpp.AppendLine("};");
            outCpp.AppendLine();

            // gene_name => uniprot_id
            outHpp.AppendLine("// gene_name => uniprot_id");
            outHpp.AppendLine("extern multimap<string, string> gene_name_dict;");
            outHpp.AppendLine();

            outCpp.AppendLine("// gene_name => uniprot_id");
            outCpp.AppendLine("multimap<string, string> gene_name_dict =");
            outCpp.AppendLine("{");
            foreach (var (gene_name, uniprot_id) in pdbDict.Select(o => (o.gene_name, o.uniprot_id)).Distinct())
                outCpp.AppendLine($"\t{{\"{gene_name}\", {"".PadRight(pdbDict.Max(o => o.gene_name.Length) - gene_name.Length)}\"{uniprot_id}\"}},");
            outCpp.AppendLine("};");
            outCpp.AppendLine();

            // pdb_id => uniprot_id
            outHpp.AppendLine("// pdb_id => uniprot_id");
            outHpp.AppendLine("extern map<string, string> pdb_id_dict;");
            outHpp.AppendLine();

            outCpp.AppendLine("// pdb_id => uniprot_id");
            outCpp.AppendLine("map<string, string> pdb_id_dict =");
            outCpp.AppendLine("{");
            foreach (var (pdb_id, uniprot_id) in pdbDict.Select(o => (o.pdb_id, o.uniprot_id)).Distinct())
                outCpp.AppendLine($"\t{{\"{pdb_id}\", \"{uniprot_id}\"}},");
            outCpp.AppendLine("};");
            outCpp.AppendLine();

            // uniprot_id => { low, length, string_table_index... }
            outHpp.AppendLine("// uniprot_id => [low, length, string_table_index...]");
            outHpp.AppendLine("extern map<string, tuple<int, int, string, vector<short>>> numbering_data;");

            outCpp.AppendLine("// uniprot_id => [low, length, string_table_index...]");
            outCpp.AppendLine("map<string, tuple<int, int, string, vector<short>>> numbering_data =");
            outCpp.AppendLine("{");
            foreach (var (uniprot_id, numbering) in flatNumberingDict)
            {
                int min = numbering.Keys.Min(), len = numbering.Keys.Max() - min + 1;
                outCpp.AppendLine($"\t{{");
                outCpp.AppendLine($"\t\t\"{uniprot_id}\",");
                outCpp.AppendLine($"\t\t{{");
                outCpp.AppendLine($"\t\t\t{min}, {len},");
                outCpp.AppendLine($"\t\t\t\"{string.Join("", Enumerable.Range(min, len).Select(i => residueNameDict[uniprot_id].TryGetValue(i, out char res) ? res : '.'))}\",");
                outCpp.AppendLine($"\t\t\t{{{string.Join(", ", Enumerable.Range(min, len).Select(i => numbering.TryGetValue(i, out string str) ? strings[str] : -1))}}}");
                outCpp.AppendLine($"\t\t}}");
                outCpp.AppendLine($"\t}},");
            }
            outCpp.AppendLine("};");

            string path = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..");
            string targetHpp = Path.Combine(path, "assets.hpp");
            File.WriteAllText(targetHpp, outHpp.ToString());
            Console.WriteLine($"{targetHpp} written.");

            string targetCpp = Path.Combine(path, "assets.cpp");
            File.WriteAllText(targetCpp, outCpp.ToString());
            Console.WriteLine($"{targetCpp} written.");
        }
    }
}
