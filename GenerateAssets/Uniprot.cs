using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace GenerateAssets
{
    public static class UniProt
    {
        public static IDictionary<string, object> SetUniProtId(this IDictionary<string, object> bag, string uniProtId)
        {
            bag["UniProtId"] = uniProtId;
            return bag;
        }

        public static IDictionary<string, object> SetProteinIdForUniProt(this IDictionary<string, object> bag, string proteinId, string species = "HUMAN")
        {
            bag["ProteinId"] = proteinId;
            bag["Species"] = species;
            bag["UniProtId"] = $"{proteinId}_{species}";
            return bag;
        }

        public static string[] GetUniProtIds(this IDictionary<string, object> bag)
        {
            return bag.Get<string[]>("UniProtIds");
        }

        public static string GetUniProtId(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("UniProtId");
        }

        public static string GetProteinId(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("ProteinId");
        }

        public static string GetProteinNameFull(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("ProteinNameFull");
        }

        public static IDictionary<string, object> ParseRecommendedNames(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), "<span property=\"name\" class=\"recommended-name\"><strong>([^<>]+)</strong>");
            var m2 = Regex.Match(bag.Get<string>("UniProtHtml"), "class=\"entry-overview-content\"><h2>([^<>]+)</h2>");
            if (m.Success)
            {
                bag["ApprovedName"] = m.Groups[1].Value;
            }
            else
            {
                bag["ApprovedName"] = null;
            }
            if (m2.Success)
            {
                bag["ApprovedSymbol"] = m2.Groups[1].Value;
            }
            else
            {
                bag["ApprovedSymbol"] = null;
            }
            return bag;
        }

        public static IDictionary<string, object> ParseProteinName(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"<span property=""alternateName"">\((\w+)_(\w+)\)</span>");
            var m2 = Regex.Match(bag.Get<string>("UniProtHtml"), @"Submitted name:\s*<h1 property=""name"">\s*([^<>]+)\s*</h1>");
            var m3 = Regex.Match(bag.Get<string>("UniProtHtml"), @"<h1 property=""name"">\s*([^<>]+?)\s*</h1>");
            if (m.Success)
            {
                bag["UniProtSymbol"] = $"{m.Groups[1].Value}_{m.Groups[2].Value}";
                bag["ProteinName"] = m.Groups[1].Value;
                bag["Species"] = m.Groups[2].Value;
            }
            else
            {
                bag["UniProtSymbol"] = null;
                bag["ProteinName"] = null;
                bag["Species"] = null;
                Logger.Current.LogError($"No ProteinName found for uniProt Id {bag["UniProtId"]}");
            }
            if (m2.Success)
            {
                bag["SubmittedProteinName"] = m2.Groups[1].Value;
            }
            else
            {
                bag["SubmittedProteinName"] = null;
            }
            if (m3.Success)
            {
                bag["ProteinNameFull"] = m3.Groups[1].Value;
            }
            else
            {
                bag["ProteinNameFull"] = null;
            }
            return bag;
        }

        public static IDictionary<string, object> ParseOrganisms(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"<div[^<>]+?id=""content-organism""[^<>]*><em>\s*([^<>]+?)\s*</em></div>");
            if (m.Success)
            {
                bag["Organisms"] = m.Groups[1].Value.Split("()".ToArray(), StringSplitOptions.RemoveEmptyEntries).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray();
            }
            else
            {
                bag["Organisms"] = new string[0];
            }
            return bag;
        }

        public static IDictionary<string, object> ParseUniProtId(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"var entryId\s*=\s*'([^']+)';");
            if (m.Success)
            {
                bag["UniProtId"] = m.Groups[1].Value;
            }
            else
            {
                bag["UniProtId"] = null;
            }
            return bag;
        }

        public static IDictionary<string, object> ParseUniProtEntry(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"</span>Entry name<sup>i</sup></span></td><td[^<>]*>([^<>]+)_([^<>]+)</td></tr>");
            if (m.Success)
            {
                bag["ProteinSymbol"] = m.Groups[1].Value;
                bag["OrganismSymbol"] = m.Groups[2].Value;
            }
            else
            {
                bag["ProteinSymbol"] = null;
                bag["OrganismSymbol"] = null;
            }
            return bag;
        }

        public static string[] GetOrganisms(this IDictionary<string, object> bag)
        {
            return bag.Get<string[]>("Organisms");
        }

        public static string GetProteinName(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("ProteinName");
        }

        public static string GetProteinSymbol(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("ProteinSymbol");
        }

        public static string GetOrganismSymbol(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("OrganismSymbol");
        }

        public static string GetSubmittedProteinName(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("SubmittedProteinName");
        }

        public static string GetSpecies(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("Species");
        }

        public static IDictionary<string, object> FetchUniProtHtml(this IDictionary<string, object> bag)
        {
            var waitTime = new int[] { 0, 500, 2000 };
            bag["UniProtHtml"] = null;

            for (int i = 0; bag["UniProtHtml"] == null; i++)
            {
                if (i == 3)
                {
                    Logger.Current.LogError($"Failed to get uniProt data for uniProt Id {bag["UniProtId"]} after 3 attempts");
                    break;
                }

                if (i > 0 && i < 3)
                {
                    Logger.Current.LogError($"Failed to get uniProt data for uniProt Id {bag["UniProtId"]}, retry in {waitTime[i]}ms");
                    Thread.Sleep(waitTime[i]);
                }

                try
                {
                    using var wc = new WebClient();
                    bag["UniProtHtml"] = wc.DownloadString(bag.GetUniProtUri());
                }
                catch (Exception)
                {
                }
            }
            return bag;
        }

        public static string GetUniProtSymbol(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("UniProtSymbol");
        }

        public static string GetUniProtHtml(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("UniProtHtml");
        }

        public static string GetUniProtUri(this IDictionary<string, object> bag)
        {
            string id = bag.GetUniProtId();
            return id != null ? $"https://www.uniprot.org/uniprot/{id}" : null;
        }

        public static string GetInterProUri(this IDictionary<string, object> bag)
        {
            string id = bag.GetUniProtId();
            return id != null ? $"https://www.ebi.ac.uk/interpro/protein/{id}" : null;
        }

        public static string GetPDBeUri(this IDictionary<string, object> bag)
        {
            string id = bag.GetUniProtId();
            return id != null ? $"https://www.ebi.ac.uk/pdbe/searchResults.html?display=both&term={id}" : null;
        }

        public static string GetReactomeUri(this IDictionary<string, object> bag)
        {
            string id = bag.GetUniProtId();
            return id != null ? $"http://www.reactome.org/content/query?q={id}" : null;
        }

        public static IDictionary<string, object> ParseChemblId(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"https://www.ebi.ac.uk/chembldb/target/inspect/(\w+)");
            if (m.Success)
                bag["ChemblId"] = m.Groups[1].Value;
            else
            {
                bag["ChemblId"] = null;
                Logger.Current.LogWarning($"No ChemblId found for uniProt Id {bag["UniProtId"]}");
            }
            return bag;
        }

        public static IDictionary<string, object> ParseHgncIdAndGeneName(this IDictionary<string, object> bag)
        {
            var m = Regex.Match(bag.Get<string>("UniProtHtml"), @"Gene names<sup>i</sup></span></td><td[^<>]+><div[^<>]+>Name:<strong[^<>]+>(?:<a href=""([^""]+)"">)?([^<>]+)(?:</a>)?");

            bag["HgncId"] = null;
            bag["GeneName"] = null;

            if (!m.Success)
            {
                Logger.Current.LogError($"No GeneNames found for {bag["UniProtId"]}");
            }
            else if (m.Groups[1].Value.StartsWith("https://www.genenames.org/data/gene-symbol-report/#!/hgnc_id/HGNC:")
                && int.TryParse(m.Groups[1].Value.Split(':').Last(), out int gnId))
            {
                Logger.Current.LogInformation($"Got geneNamesId {gnId} for {bag["UniProtId"]}");
                bag["HgncId"] = (int?)gnId;
                bag["GeneName"] = m.Groups[2].Value;
            }
            else if (m.Groups[1].Value.StartsWith("https://www.genenames.org/cgi-bin/gene_symbol_report?hgnc_id=")
                && int.TryParse(m.Groups[1].Value.Split('=').Last(), out gnId))
            {
                Logger.Current.LogInformation($"Got geneNamesId {gnId} for {bag["UniProtId"]}");
                bag["HgncId"] = (int?)gnId;
                bag["GeneName"] = m.Groups[2].Value;
            }
            else
            {
                bag["GeneName"] = m.Groups[2].Value;
                Logger.Current.LogWarning($"HgncId cannot be extracted for {bag["UniProtId"]}");
            }

            if (bag["GeneName"] == null)
            {
                var m2 = Regex.Match(bag.Get<string>("UniProtHtml"), @"<div id=""content-gene"" class=""entry-overview-content""><h2>\s*([^<>]+?)\s*</h2></div>");
                if (m2.Success)
                {
                    bag["GeneName"] = m2.Groups[1].Value.ToUpper();
                }
            }

            return bag;
        }

        public static int? GetHgncId(this IDictionary<string, object> bag)
        {
            return bag.Get<int?>("HgncId");
        }

        public static string GetGeneName(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("GeneName");
        }

        public class UniProtStructRecord
        {
            public string UniProtId { get; set; }
            public string PdbEntry { get; set; }
            public string Method { get; set; }
            public string Resolution { get; set; }
            public string Chain { get; set; }
            public string Positions { get; set; }

            public override bool Equals(object obj)
            {
                return obj is UniProtStructRecord that
                    && UniProtId == that.UniProtId
                    && PdbEntry == that.PdbEntry
                    && Method == that.Method
                    && Resolution == that.Resolution
                    && Chain == that.Chain
                    && Positions == that.Positions;
            }

            public override int GetHashCode()
            {
                return UniProtId?.GetHashCode() ?? 0
                    ^ PdbEntry?.GetHashCode() ?? 0
                    ^ Method?.GetHashCode() ?? 0
                    ^ Resolution?.GetHashCode() ?? 0
                    ^ Chain?.GetHashCode() ?? 0
                    ^ Positions?.GetHashCode() ?? 0;
            }

            public static bool operator ==(UniProtStructRecord a, UniProtStructRecord b) => a is null ? b is null : a.Equals(b);
            public static bool operator !=(UniProtStructRecord a, UniProtStructRecord b) => a is null ? !(b is null) : !a.Equals(b);
        }

        public static IDictionary<string, object> ParseUniProtStructures(this IDictionary<string, object> bag)
        {
            var rs = Regex.Matches(
                    (string)bag["UniProtHtml"],
                    @"href=""https://www.ebi.ac.uk/pdbe-srv/view/entry/(\w+)""[^<>]*>\1</a></td><td>([^<>]+)</td><td>([^<>]+)</td><td>([^<>]+)</td><td><a[^<>]+>([^<>]+)</a>")
                //                                                     1pdb_entry                   2method          3resolution      4chain                    5positions
                .Cast<Match>()
                .Select(o => new UniProtStructRecord
                {
                    UniProtId = bag.Get<string>("UniProtId"),
                    PdbEntry = o.Groups[1].Value,
                    Method = o.Groups[2].Value,
                    Resolution = o.Groups[3].Value,
                    Chain = o.Groups[4].Value,
                    Positions = o.Groups[5].Value,
                })
                .ToArray();

            var dict = new Dictionary<string, UniProtStructRecord>();
            foreach (var r in rs)
            {
                if (!dict.ContainsKey(r.PdbEntry))
                {
                    dict[r.PdbEntry] = r;
                }
                else if (dict[r.PdbEntry] != r)
                {
                    Logger.Current.LogWarning($"Duplicate PDB {r.PdbEntry} found for uniProt Id {r.UniProtId} but with different description.");
                }
            }

            bag["UniProtPdbList"] = dict.Values.ToArray();
            if (rs.Length == 0)
                Logger.Current.LogWarning($"No PDBs found for uniProt Id {bag["UniProtId"]}");

            return bag;
        }

        public static UniProtStructRecord[] GetUniProtPdbList(this IDictionary<string, object> bag)
        {
            return bag.Get<UniProtStructRecord[]>("UniProtPdbList");
        }
    }
}
