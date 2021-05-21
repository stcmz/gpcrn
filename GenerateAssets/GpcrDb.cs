using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace GenerateAssets
{
    public static class GpcrDb
    {
        public static string GetGpcrDbUri(this IDictionary<string, object> bag)
        {
            return "https://gpcrdb.org/structure/";
        }

        public static IDictionary<string, object> FetchGpcrDbHtml(this IDictionary<string, object> bag)
        {
            try
            {
                using var wc = new WebClient();
                bag["GpcrDbHtml"] = wc.DownloadString(bag.GetGpcrDbUri());
            }
            catch (Exception)
            {
                bag["GpcrDbHtml"] = null;
                Logger.Current.LogError($"Failed to get GPCRdb data.");
            }
            return bag;
        }

        public enum ReceptorState
        {
            Unknown = 0,
            Active,
            Intermediate,
            Inactive,
        }

        public class GpcrDbRecord
        {
            public string UniProtId { get; set; }
            public string ProteinName { get; set; }
            public string Iuphar { get; set; }
            public string Family { get; set; }
            public string Class { get; set; }
            public string Species { get; set; }
            public string PdbEntry { get; set; }
            public string PdbEntryRefined { get; set; }
            public string Resolution { get; set; }
            public string Chain { get; set; }
            public ReceptorState State { get; set; }
            public string Distance { get; set; }

            public override bool Equals(object obj)
            {
                return obj is GpcrDbRecord that
                    && UniProtId == that.UniProtId
                    && ProteinName == that.ProteinName
                    && Iuphar == that.Iuphar
                    && Family == that.Family
                    && Class == that.Class
                    && Species == that.Species
                    && PdbEntry == that.PdbEntry
                    && PdbEntryRefined == that.PdbEntryRefined
                    && Resolution == that.Resolution
                    && Chain == that.Chain
                    && State == that.State
                    && Distance == that.Distance;
            }

            public override int GetHashCode()
            {
                return UniProtId?.GetHashCode() ?? 0
                    ^ ProteinName?.GetHashCode() ?? 0
                    ^ Iuphar?.GetHashCode() ?? 0
                    ^ Family?.GetHashCode() ?? 0
                    ^ Class?.GetHashCode() ?? 0
                    ^ Species?.GetHashCode() ?? 0
                    ^ PdbEntry?.GetHashCode() ?? 0
                    ^ PdbEntryRefined?.GetHashCode() ?? 0
                    ^ Resolution?.GetHashCode() ?? 0
                    ^ Chain?.GetHashCode() ?? 0
                    ^ (int)State
                    ^ Distance?.GetHashCode() ?? 0;
            }

            public static bool operator ==(GpcrDbRecord a, GpcrDbRecord b) => a.Equals(b);
            public static bool operator !=(GpcrDbRecord a, GpcrDbRecord b) => !a.Equals(b);
        }

        public static ReceptorState ParseState(this string state)
        {
            return state.ToLower().Trim() switch
            {
                "active" => ReceptorState.Active,
                "inactive" => ReceptorState.Inactive,
                "intermediate" => ReceptorState.Intermediate,
                _ => ReceptorState.Unknown,
            };
        }

        public static IDictionary<string, object> ParseGpcrDbStructures(this IDictionary<string, object> bag)
        {
            string pattern =
                //                                                              1uniprotid 2protein_name
                @"<td[^<>]*>(?:<span>)?<a [^<>]*href=""http://www.uniprot.org/uniprot/([\w-]+)"">([\w-]+)</a>(?:</span>)?</td>\s*" +
                //                                     3iuphar
                @"<td[^<>]*>(?:<span>)?<a [^<>]*href="".+?"">(.{0,20})</a>(?:</span>)?</td>\s*" +
                //                        4family
                @"<td[^<>]*>(?:<span>)?\s*([^<>]+)(?:</span>)?</td>\s*" +
                //                        5class
                @"<td[^<>]*>(?:<span>)?\s*(.*?)(?:</span>)?</td>\s*" +
                //          6species
                @"<td[^<>]*>(.*?)</td>\s*(?:<!--[^<>]+-->)?\s*" +
                //          method
                @"<td[^<>]*>\s*(?:.*?)\s*</td>\s*" +
                //                       7pdb_entry
                @"<td[^<>]*>\s*<a [^<>]*href=""([^""]+)"">[^<>]+</a>\s*</td>\s*" +
                //                          8pdb_entry_refined
                @"<td[^<>]*>\s*(?:<a [^<>]*href=""([^""]+)"">)?[^<>]+(?:</a>)?\s*</td>\s*" +
                //             9resolution
                @"<td[^<>]*>\s*(.*?)\s*</td>\s*" +
                //             10chain
                @"<td[^<>]*>\s*(.*?)\s*</td>\s*" +
                //             11state
                @"<td[^<>]*>\s*(.*?)\s*</td>\s*" +
                //             12distance
                @"<td[^<>]*>\s*(.*?)\s*</td>\s*";

            var rs = Regex.Matches((string)bag["GpcrDbHtml"], pattern)
                .Cast<Match>()
                .Select(o => new GpcrDbRecord
                {
                    UniProtId = o.Groups[1].Value, // uniprot id
                    ProteinName = o.Groups[2].Value, // protein name
                    Iuphar = o.Groups[3].Value, // iuphar
                    Family = o.Groups[4].Value, // family
                    Class = o.Groups[5].Value, // class
                    Species = o.Groups[6].Value, // species
                    PdbEntry = o.Groups[7].Value, // pdb_entry
                    PdbEntryRefined = o.Groups[8].Value, // pdb_entry_refined
                    Resolution = o.Groups[9].Value, // resolution
                    Chain = o.Groups[10].Value, // chain
                    State = ParseState(o.Groups[11].Value), // state
                    Distance = o.Groups[12].Value, // distance
                })
                .ToArray();

            var dict = new Dictionary<string, GpcrDbRecord>();
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

            bag["GpcrDbPdbList"] = dict.Values.ToArray();
            if (rs.Length == 0)
                Logger.Current.LogWarning($"No PDBs found on GPCRdb");

            return bag;
        }

        public static GpcrDbRecord[] GetGpcrDbPdbList(this IDictionary<string, object> bag)
        {
            return bag.Get<GpcrDbRecord[]>("GpcrDbPdbList");
        }

        private static bool IsHtmlSpace(char c)
        {
            return char.IsWhiteSpace(c) || c == '\r' || c == '\n';
        }

        private static string TrimHtmlSpaces(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                if (!IsHtmlSpace(c))
                    sb.Append(c);
                else if (sb.Length > 0 && !IsHtmlSpace(sb[sb.Length - 1]))
                    sb.Append(' ');
            return sb.ToString();
        }

        public static IDictionary<string, object> FetchGpcrDbResidueTableHtml(this IDictionary<string, object> bag)
        {
            string url = "https://gpcrdb.org/residue/residuetable";
            using var wc = new WebClient();
            string html = wc.DownloadString(url);
            return bag.Set("GpcrDbResidueTableHtml", html);
        }

        public static IDictionary<string, object> ParseGpcrDbNumberingSchemeIds(this IDictionary<string, object> bag)
        {
            string html = bag.Get<string>("GpcrDbResidueTableHtml");
            var schemeIds = Regex.Matches(html, @"SelectionSchemesToggle\('(\d+)'\)[^<>]*>([\s\S]+?)</a>")
                .Cast<Match>()
                .ToDictionary(o => int.Parse(o.Groups[1].Value), o => o.Groups[2].Value.Trim(" \r\t\v\n".ToCharArray()));
            return bag.Set("GpcrDbNumberingSchemeIds", schemeIds);
        }

        public static IDictionary<string, object> FetchGpcrDbNumberingSpecies(this IDictionary<string, object> bag)
        {
            string html = bag.Get<string>("GpcrDbResidueTableHtml");
            var species = Regex.Matches(html, @"SelectionSpeciesToggle\('(\d+)'\)[^<>]*>([\s\S]+?)</a>")
                .Cast<Match>()
                .ToDictionary(o => o.Groups[2].Value.Trim(" \r\t\v\n".ToCharArray()), o => int.Parse(o.Groups[1].Value));
            return bag.Set("GpcrDbNumberingSpecies", species);
        }

        public static Dictionary<int, string> GetGpcrDbNumberingSchemeIds(this IDictionary<string, object> bag)
        {
            return bag.Get<Dictionary<int, string>>("GpcrDbNumberingSchemeIds");
        }

        private static string GetTerm(string s)
        {
            return TrimHtmlSpaces(Regex.Replace(s, @"\[.*\]|<[^<>]+>", "").Trim());
        }

        public static IDictionary<string, object> FetchGpcrDbCrystalStructureSelectionIds(this IDictionary<string, object> bag)
        {
            string url = "https://gpcrdb.org/common/addtoselection?selection_type=targets&selection_subtype=protein_set&selection_id=1";
            string html;
            using (var wc = new WebClient())
            {
                html = wc.DownloadString(url);
            }

            var divs = Regex.Matches(html,
                @"<div[^<>]*>([\s\S]+?)\(Protein\)\s*<a\s+[^<>]+,\s*(\d+)[^<>]*>[\s\S]*?</a>\s*</div>");
            //               1Name                2Id

            var dict = divs.Cast<Match>().ToDictionary(o => int.Parse(o.Groups[2].Value), o => TrimHtmlSpaces(o.Groups[1].Value.Trim()));
            var newDict = new Dictionary<int, string>();

            foreach (var item in dict)
            {
                if (!item.Value.Contains("[Human]"))
                {
                    string kw = GetTerm(item.Value);
                    string term = WebUtility.UrlEncode(kw);
                    Console.WriteLine($"Finding {term} for human...");

                    using var wc = new WebClient();
                    wc.Headers.Set("X-Requested-With", "XMLHttpRequest");
                    url = $"https://gpcrdb.org/protein/autocomplete?term={term}";
                    string json = wc.DownloadString(url);
                    var objs = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(json);

                    foreach (var obj in objs)
                    {
                        if ((string)obj["type"] == "protein" && GetTerm((string)obj["label"]) == kw)
                        {
                            int id = Convert.ToInt32(obj["id"]);
                            newDict[id] = (string)obj["label"];
                            Console.WriteLine($"Found {id} for {kw} for human!");
                            break;
                        }
                    }
                }
                else
                {
                    newDict[item.Key] = item.Value;
                }
            }
            return bag.Set("GpcrDbCrystalStructureSelectionIds", newDict);
        }

        public static Dictionary<int, string> GetGpcrDbCrystalStructureSelectionIds(this IDictionary<string, object> bag)
        {
            return bag.Get<Dictionary<int, string>>("GpcrDbCrystalStructureSelectionIds");
        }

        public static IDictionary<string, object> SetGpcrDbSelectionId(this IDictionary<string, object> bag, int selectionId)
        {
            return bag.Set("GpcrDbSelectionId", selectionId);
        }

        public static int GetGpcrDbSelectionId(this IDictionary<string, object> bag)
        {
            return bag.Get<int>("GpcrDbSelectionId");
        }

        public static IDictionary<string, object> SetGpcrDbNumberingSchemes(this IDictionary<string, object> bag, params string[] schemes)
        {
            var ids = new List<(int Id, string Name)>();
            if (schemes.Length == 0)
            {
                ids.AddRange(bag.GetGpcrDbNumberingSchemeIds().Select(o => (o.Key, o.Value)));
            }
            else
            {
                var _ids = bag.GetGpcrDbNumberingSchemeIds();
                foreach (string scheme in schemes)
                    foreach (var p in _ids)
                        if (p.Value.Contains(scheme))
                            ids.Add((p.Key, p.Value));
            }
            return bag.Set("GpcrDbNumberingSchemes", ids);
        }

        public static List<(int Id, string Name)> GetGpcrDbNumberingSchemes(this IDictionary<string, object> bag)
        {
            return bag.Get<List<(int, string)>>("GpcrDbNumberingSchemes");
        }

        public static IDictionary<string, object> FetchGpcrDbResidueTableDisplayHtml(this IDictionary<string, object> bag)
        {
            var schemes = bag.GetGpcrDbNumberingSchemes();

            using var hc = new HttpClient();
            // Use the preselected id
            int selectionId = bag.GetGpcrDbSelectionId();
            string url = $"https://gpcrdb.org/common/addtoselection?selection_type=targets&selection_subtype=protein&selection_id={selectionId}";
            _ = hc.GetStringAsync(url).Result;

            foreach (var (Id, Name) in schemes)
            {
                // Unselect the "Common arrestin numbering scheme"
                url = $"https://gpcrdb.org/common/selectionschemestoggle?numbering_scheme_id={Id}";
                _ = hc.GetStringAsync(url).Result;
            }

            url = "https://gpcrdb.org/residue/residuetabledisplay";
            string html = hc.GetStringAsync(url).Result;
            return bag.Set("GpcrDbResidueTableDisplayHtml", html);
        }

        public static IDictionary<string, object> FetchGpcrDbResidueTableDisplayHtmlByUniProtId(this IDictionary<string, object> bag, string uniProtId)
        {
            var schemes = bag.GetGpcrDbNumberingSchemes();

            using var hc = new HttpClient();
            // Use the input UniProt Id
            string url = "https://gpcrdb.org/common/targetformread";
            _ = hc.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> { { "input-targets", uniProtId } })).Result;

            url = $"https://gpcrdb.org/common/selectionschemespredefined?numbering_schemes=All";
            _ = hc.GetStringAsync(url).Result;
            _ = hc.GetStringAsync(url).Result;

            foreach (var (Id, Name) in schemes)
            {
                // Select only those in schemes
                url = $"https://gpcrdb.org/common/selectionschemestoggle?numbering_scheme_id={Id}";
                _ = hc.GetStringAsync(url).Result;
            }

            url = "https://gpcrdb.org/residue/residuetabledisplay";
            string html = hc.GetStringAsync(url).Result;
            return bag.Set("GpcrDbResidueTableDisplayHtml", html);
        }

        public static string GetGpcrDbResidueTableDisplayHtml(this IDictionary<string, object> bag)
        {
            return bag.Get<string>("GpcrDbResidueTableDisplayHtml");
        }

        public static IDictionary<string, object> ParseGpcrDbResidueTableDisplayHtml(this IDictionary<string, object> bag)
        {
            string html = bag.GetGpcrDbResidueTableDisplayHtml();

            var schemes = new List<(string Name, string ShortName)>(); // Per scheme names
            var numberings = new Dictionary<int, List<string>>(); // A dictionary mapping residue number to numberings of multiple schemes.
            var residueNames = new Dictionary<int, char>(); // residue number => residue name

            var table = Regex.Match(html, @"<table.*?>([\s\S]+)</table>");
            var trs = Regex.Matches(table.Groups[1].Value, @"<tr.*?>([\s\S]+?)</tr>");

            foreach (Match tr in trs)
            {
                var tds = Regex.Matches(tr.Groups[1].Value,
                    @"<td\s+[^<>]*id=""(.*?)""\s+class=""(.*?)""[^<>]*>([\s\S]+?)</td>");
                //                     1P1Rx19           2info/residue 3P1Rx19
                if (tds.Count > 0)
                {
                    var numbering = new List<string>();

                    foreach (Match td in tds)
                    {
                        if (td.Groups[2].Value.Contains("residue"))
                        {
                            string residue = td.Groups[3].Value.Trim(' ', '-');
                            if (residue.Length > 0)
                            {
                                if (!char.IsUpper(residue[0]))
                                    throw new Exception($"Residue number '{residue[0]}' is not in a recognizable format.");
                                if (!int.TryParse(residue.Substring(1), out int residueNum))
                                    throw new Exception($"Residue number '{residueNum}' is not a number.");
                                if (numberings.ContainsKey(residueNum))
                                    throw new Exception($"Duplicate residue number '{residueNum}'.");
                                residueNames[residueNum] = residue[0];
                                numberings[residueNum] = numbering;
                                continue;
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            numbering.Add(td.Groups[3].Value.Split(' ').Last());
                        }
                    }

                    continue;
                }

                var ths = Regex.Matches(tr.Groups[1].Value,
                    @"<th\s+[^<>]*class="".*?""[^<>]*>\s*<div>\s*<span\s+id='(.*?)'>(?:<b>)?([\s\S]+?)(?:</b>)?</span>\s*</div>\s*</th>");
                //                                                          1uniprot/scheme 2name<br/>species
                if (ths.Count > 0)
                {
                    int col = 0;
                    foreach (Match th in ths)
                    {
                        if (th.Groups[2].Value.Contains("<br />"))
                        {
                            var lines = th.Groups[2].Value.Split("<br />");
                            if (lines.Length != 2)
                                throw new Exception($"Expect two line column header for a protein.");
                            if (++col > 1)
                                throw new Exception($"Multiple proteins are not supported.");
                            bag.Set("UniProtId", th.Groups[1].Value.ToUpper());
                            bag.Set("ProteinName", lines[0]);
                        }
                        else
                        {
                            schemes.Add((th.Groups[1].Value, th.Groups[2].Value));
                        }
                    }

                    if (col == 0)
                        throw new Exception($"No protein column found.");
                    continue;
                }
            }

            bag.Set("GpcrDbResidueNames", residueNames);
            bag.Set("GpcrDbResidueSchemes", schemes);
            bag.Set("GpcrDbResidueNumberings", numberings);

            return bag;
        }

        public static Dictionary<int, char> GetGpcrDbResidueNames(this IDictionary<string, object> bag)
        {
            return bag.Get<Dictionary<int, char>>("GpcrDbResidueNames");
        }

        public static List<(string Name, string ShortName)> GetGpcrDbResidueSchemes(this IDictionary<string, object> bag)
        {
            return bag.Get<List<(string Name, string ShortName)>>("GpcrDbResidueSchemes");
        }

        public static IDictionary<string, object> FindGpcrDbResidueNumberingIndices(this IDictionary<string, object> bag)
        {
            var schemes = bag.GetGpcrDbResidueSchemes();
            var numberSchemes = bag.GetGpcrDbNumberingSchemes();
            var ids = new List<int>();
            var names = new List<(string Name, string ShortName)>();

            foreach (var (_, Name) in numberSchemes)
            {
                for (int i = 0; i < schemes.Count; i++)
                {
                    if (Name == schemes[i].Name)
                    {
                        ids.Add(i);
                        names.Add(schemes[i]);
                    }
                }
            }

            bag.Set("GpcrDbResidueNumberingHeaders", names);
            return bag.Set("GpcrDbResidueNumberingIndices", ids);
        }

        public static List<(string Name, string ShortName)> GetGpcrDbResidueNumberingHeaders(this IDictionary<string, object> bag)
        {
            return bag.Get<List<(string, string)>>("GpcrDbResidueNumberingHeaders");
        }

        public static Dictionary<int, string[]> GetGpcrDbResidueNumberings(this IDictionary<string, object> bag)
        {
            var ids = bag.Get<List<int>>("GpcrDbResidueNumberingIndices");
            var dict = bag.GetGpcrDbAllResidueNumberings();
            return dict.ToDictionary(o => o.Key, o => ids.Select(p => o.Value[p]).ToArray());
        }

        public static Dictionary<int, List<string>> GetGpcrDbAllResidueNumberings(this IDictionary<string, object> bag)
        {
            return bag.Get<Dictionary<int, List<string>>>("GpcrDbResidueNumberings");
        }

        public static List<string> GetGpcrNumberings(this IDictionary<string, object> bag, int residueNum)
        {
            if (bag.GetGpcrDbAllResidueNumberings().TryGetValue(residueNum, out var list))
                return list;
            return null;
        }

        public static string GetGpcrNumberings(this IDictionary<string, object> bag, int residueNum, string scheme)
        {
            if (!bag.GetGpcrDbAllResidueNumberings().TryGetValue(residueNum, out var list))
                return null;

            var schemes = bag.GetGpcrDbResidueSchemes();
            for (int i = 0; i < schemes.Count; i++)
                if (schemes[i].Name == scheme || schemes[i].ShortName == scheme)
                    return list[i];

            throw new IndexOutOfRangeException($"Scheme {scheme} not found.");
        }
    }
}
