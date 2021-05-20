#ifdef _WIN32
#include <windows.h>
#include <io.h>
#endif
#include <iostream>
#include <iomanip>
#include <string>
#include <numeric>
#include <map>
#include <vector>
#include <algorithm>
#include <array>
#include <regex>
#include <cctype>
#include <fstream>
#include <filesystem>
#include <boost/program_options.hpp>
#include "assets.hpp"
#include "string.hpp"
using namespace std;
using namespace std::filesystem;
using namespace boost::program_options;

map<char, string> res_names =
{
	// Charged (side chains often make salt bridges},
	{ 'R', "ARG" }, // Arginine,
	{ 'K', "LYS" }, // Lysine,
	{ 'D', "ASP" }, // AsparticAcid,
	{ 'E', "GLU" }, // GlutamicAcid,

	// Polar (usually participate in hydrogen bonds as proton donors or acceptors},
	{ 'Q', "GLN" }, // Glutamine,
	{ 'N', "ASN" }, // Asparagine,
	{ 'H', "HIS" }, // Histidine,
	{ 'S', "SER" }, // Serine,
	{ 'T', "THR" }, // Threonine,
	{ 'Y', "TYR" }, // Tyrosine,
	{ 'C', "CYS" }, // Cysteine,
	{ 'W', "TRP" }, // Tryptophan,

	// Hydrophobic (normally buried inside the protein core},
	{ 'A', "ALA" }, // Alanine,
	{ 'I', "ILE" }, // Isoleucine,
	{ 'L', "LEU" }, // Leucine,
	{ 'M', "MET" }, // Methionine,
	{ 'F', "PHE" }, // Phenylalanine,
	{ 'V', "VAL" }, // Valine,
	{ 'P', "PRO" }, // Proline,
	{ 'G', "GLY" }, // Glycine
};

enum class headers
{
	symbol,
	gene,
	uniprot,
	residue,
	sequence,
	numbering,
};

array<pair<int, string>, 6> header_fmts =
{ {
	{ 14, "Protein" },
	{ 9,  "Gene" },
	{ 10, "Uniprot" },
	{ 5,  "Res" },
	{ 6,  "Seq" },
	{ 0,  "Numbering" },
} };

// see https://en.wikipedia.org/wiki/ANSI_escape_code#Colors
enum class fgcolor
{
	none    = 0,
	black   = 30, // Black   30 40
	red     = 31, // Red     31 41
	green   = 32, // Green   32 42
	yellow  = 33, // Yellow  33 43
	blue    = 34, // Blue    34 44
	magenta = 35, // Magenta 35 45
	cyan    = 36, // Cyan    36 46
	white   = 37, // White   37 47
	bright_black   = 90, // Bright Black   90 100
	bright_red     = 91, // Bright Red     91 101
	bright_green   = 92, // Bright Green   92 102
	bright_yellow  = 93, // Bright Yellow  93 103
	bright_blue    = 94, // Bright Blue    94 104
	bright_magenta = 95, // Bright Magenta 95 105
	bright_cyan    = 96, // Bright Cyan    96 106
	bright_white   = 97, // Bright White   97 107
};

enum class bgcolor
{
	none    = 0,
	black   = 40, // Black   30 40
	red     = 41, // Red     31 41
	green   = 42, // Green   32 42
	yellow  = 43, // Yellow  33 43
	blue    = 44, // Blue    34 44
	magenta = 45, // Magenta 35 45
	cyan    = 46, // Cyan    36 46
	white   = 47, // White   37 47
	bright_black   = 100, // Bright Black   90 100
	bright_red     = 101, // Bright Red     91 101
	bright_green   = 102, // Bright Green   92 102
	bright_yellow  = 103, // Bright Yellow  93 103
	bright_blue    = 104, // Bright Blue    94 104
	bright_magenta = 105, // Bright Magenta 95 105
	bright_cyan    = 106, // Bright Cyan    96 106
	bright_white   = 107, // Bright White   97 107
};

set<string> colorings
{
	"always",
	"auto",
	"never",
};

set<string> listings
{
	"schemes",
	"residues",
	"symbols",
	"symbol_species",
	"genes",
	"pdbids",
	"uniprots",
};

#ifdef _WIN32

bool is_redirected(FILE* file)
{
	return !_isatty(_fileno(file));
}

// see https://docs.microsoft.com/en-us/windows/console/console-virtual-terminal-sequences#example
void init_console_coloring()
{
	HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
	if (hOut == INVALID_HANDLE_VALUE)
		return;

	DWORD dwMode = 0;
	if (!GetConsoleMode(hOut, &dwMode))
		return;

	dwMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
	if (!SetConsoleMode(hOut, dwMode))
		return;
}

#else // __APPLE__ || __linux__ || __unix__ || defined(_POSIX_VERSION)

bool is_redirected(FILE* file)
{
	return !isatty(fileno(file));
}

void init_console_coloring()
{
}

#endif

template <class _Traits>
inline basic_ostream<char, _Traits>& operator<<(basic_ostream<char, _Traits>& _Ostr,
	const fgcolor& color)
{
	return _Ostr << "\x1b[" << (int)color << "m";
}

bool iequals(const string& a, const string& b)
{
	for (size_t i = 0; i < a.size() || i < b.size(); ++i)
	{
		if (i >= a.size())
		{
			if (!isspace(b[i])) return false;
		}
		else if (i >= b.size())
		{
			if (!isspace(a[i])) return false;
		}
		else if (tolower(a[i]) != tolower(b[i]))
		{
			return false;
		}
	}
	return true;
}

int match_scheme(const string& s)
{
	for (int i = 0; i < (int)schemes.size(); i++)
		for (auto& name : get<2>(schemes[i]))
			if (iequals(s, name))
				return i;
	return -1;
}

void supported_schemes(const string& default_scheme)
{
	cout << "Supported schemes:" << endl;

	for (auto [offset, width, abbrs] : schemes)
	{
		bool first = true;
		string s;
		for (auto it = abbrs.rbegin(); it != abbrs.rend() - 1; ++it)
		{
			if (!first)
				s += ", ";
			s += *it;
			first = false;
		}
		cout << "  " << left << setw(16) << s;
		cout << abbrs.front();
		cout << (find(abbrs.begin(), abbrs.end(), default_scheme) != abbrs.end() ? " -> DEFAULT" : "") << endl;
	}
}

bool get_index_for_seq(const string& uniprot, int seq, int& index, char& res_name)
{
	auto [min_seq, res_count, res_names, numbering] = numbering_data[uniprot];
	if (seq < min_seq || seq >= min_seq + res_count || numbering[seq - min_seq] == -1)
		return false;
	index = numbering[seq - min_seq];
	res_name = res_names[seq - min_seq];
	return true;
}

void output_line(const array<bool, header_fmts.size()>& no_cols, int last_col, const string& scheme)
{
	for (int i = 0; i < (int)header_fmts.size(); i++)
	{
		if (no_cols[i])
			continue;
		if (i != last_col)
			cout << left << setw(header_fmts[i].first);
		cout << header_fmts[i].second;
		if (i == last_col)
			cout << endl;
	}
}

void output_line(const array<bool, header_fmts.size()>& no_cols, int last_col, const array<bool, header_fmts.size()>& highlights, const string& uniprot, const string& seq, const string& numbering, const char& res_name, fgcolor hlcolor)
{
	auto [symbol, species, gene_name, long_species] = uniprot_dict[uniprot];

	vector<string> fields
	{
		symbol + '_' + species,
		gene_name,
		uniprot,
		res_names.count(res_name) ? res_names[res_name] : "?",
		res_names.count(res_name) ? res_name + seq : "?" + seq,
		numbering,
	};

	for (int i = 0; i < (int)header_fmts.size(); i++)
	{
		if (no_cols[i])
			continue;
		if (highlights[i] && hlcolor != fgcolor::none)
			cout << hlcolor;
		if (i != last_col)
			cout << left << setw(header_fmts[i].first);
		cout << fields[i];
		if (highlights[i] && hlcolor != fgcolor::none)
			cout << fgcolor::none;
		if (i == last_col)
			cout << endl;
	}
}

int process(const array<bool, header_fmts.size()>& no_cols, int last_col, bool no_headers, bool show_unmatched, int scheme_id, int& line_no, const string& query, fgcolor hlcolor)
{
	smatch ms;
	if (!regex_match(query, ms, regex("([^:]*):(.*)")))
	{
		cerr << "ERROR: invalid query '" << query << "'; the correct form is '<target>:<numbering>'" << endl;
		return 2;
	}

	string target = ms[1].str(), numbering = ms[2].str();

	if (target.empty() && numbering.empty())
	{
		cerr << "ERROR: invalid query '" << query << "'; at least a target or a numbering is required" << endl;
		return 2;
	}

	vector<string> uids;
	transform(target.begin(), target.end(), target.begin(), ::toupper);
	transform(numbering.begin(), numbering.end(), numbering.begin(), ::tolower);

	array<bool, header_fmts.size()> highlights{};

	// only target is blank
	if (target.empty())
	{
		for (auto [uid, pn] : uniprot_dict)
			uids.push_back(uid);
	}
	// uniprot_id: P28223
	// species: Human
	else if (uniprot_dict.count(target))
	{
		highlights[(int)headers::uniprot] = true;
		uids.push_back(target);
	}
	// pdb_id: 6A93
	else if (pdb_id_dict.count(target)) // unique
	{
		//highlights[(int)headers::pdbid] = true;
		uids.push_back(pdb_id_dict[target]);
	}
	// gene name: HTR2A
	else if (gene_name_dict.count(target)) // could be multiple
	{
		highlights[(int)headers::gene] = true;
		auto [lo, hi] = gene_name_dict.equal_range(target);
		for (; lo != hi; ++lo)
			uids.push_back(lo->second);
	}
	// protein_symbol_species: 5HT2A_HUMAN
	else if (symbol_species_dict.count(target)) // unique
	{
		highlights[(int)headers::symbol] = true;
		uids.push_back(symbol_species_dict[target]);
	}
	// protein_symbol: 5HT2A
	else if (symbol_dict.count(target)) // could be multiple
	{
		highlights[(int)headers::symbol] = true;
		auto [lo, hi] = symbol_dict.equal_range(target);
		for (; lo != hi; ++lo)
			uids.push_back(lo->second);
	}
	else
	{
		cerr << "ERROR: unknown target '" << ms[1].str() << "'; use uniprot id, gene name, protein symbol or pdb id for a GPCR" << endl;
		return 2;
	}

	auto [offset, width, names] = schemes[scheme_id];

	// all residue numberings
	if (numbering.empty())
	{
		if (!no_headers && !line_no)
		{
			output_line(no_cols, last_col, names[1]);
			++line_no;
		}
		for (auto& uid : uids)
		{
			auto [min_seq, seq_count, seq_names, numbering] = numbering_data[uid];
			for (int seq = min_seq; seq < min_seq + seq_count; seq++)
			{
				int stridx;
				char res_name;
				if (get_index_for_seq(uid, seq, stridx, res_name))
				{
					output_line(no_cols, last_col, highlights, uid, to_string(seq), string_table[stridx].substr(offset, width), res_name, hlcolor);
					++line_no;
				}
			}
		}
	}
	// numbering is a residue sequence
	else if (count_if(numbering.begin(), numbering.end(), ::isdigit) == (int)numbering.size())
	{
		highlights[(int)headers::sequence] = true;
		if (!no_headers && !line_no)
		{
			output_line(no_cols, last_col, names[1]);
			++line_no;
		}
		int seq = stoi(numbering);
		for (auto& uid : uids)
		{
			int stridx;
			char res_name;
			if (get_index_for_seq(uid, seq, stridx, res_name))
			{
				output_line(no_cols, last_col, highlights, uid, to_string(seq), string_table[stridx].substr(offset, width), res_name, hlcolor);
				++line_no;
			}
			else if (show_unmatched)
			{
				output_line(no_cols, last_col, highlights, uid, to_string(seq), "?", '?', hlcolor);
				++line_no;
			}
		}
	}
	// numbering is a residue numbering
	else if (numbering.size() <= width)
	{
		highlights[(int)headers::numbering] = true;
		if (!no_headers && !line_no)
		{
			output_line(no_cols, last_col, names[1]);
			++line_no;
		}
		for (auto& uid : uids)
		{
			auto [min_seq, seq_count, seq_names, numbering2] = numbering_data[uid];
			bool hit = false;

			for (int seq = min_seq; seq < min_seq + seq_count; seq++)
			{
				int stridx;
				char res_name;
				if (!get_index_for_seq(uid, seq, stridx, res_name))
					continue;

				auto str = string_table[stridx].substr(offset, width);
				hit = iequals(str, numbering);

				if (hit)
				{
					output_line(no_cols, last_col, highlights, uid, to_string(seq), str, res_name, hlcolor);
					++line_no;
					break;
				}
			}

			if (!hit && show_unmatched)
			{
				output_line(no_cols, last_col, highlights, uid, "?", ms[2].str(), '?', hlcolor);
				++line_no;
			}
		}
	}
	// unknown numbering
	else
	{
		cerr << "ERROR: invalid numbering '" << ms[2].str() << "' for scheme '" << names[0] << "'" << endl;
		return 2;
	}

	return 0;
}

string formatter(const set<string>& set)
{
	string r;
	string last;
	for (auto& s : set)
	{
		if (!last.empty())
		{
			r.push_back('\'');
			r += last;
			r += "', ";
		}
		last = s;
	}
	if (!last.empty())
	{
		r += "or '";
		r += last;
		r.push_back('\'');
	}
	return r;
}

int main(int argc, char* argv[])
{
	static string default_scheme = "BW", default_coloring = "auto";
	static fgcolor default_hlcolor = fgcolor::bright_red;

	try
	{
		vector<string> queries;
		path file;
		string scheme, listing, coloring;
		bool no_headers, show_unmatched, ignore_errors;
		array<bool, header_fmts.size()> no_cols{};
		fgcolor hlcolor = fgcolor::none;

		options_description input_options("Input options");
		input_options.add_options()
			("query,q", value<vector<string>>(&queries)->value_name("QUERY ..."), "a list of case-insensitive queries; QUERY must be in the format of <target>:<numbering> where <target> is any of: uniprot id, gene name, protein symbol or pdb id, <numbering> is either a residue sequence number or a residue numbering in the scheme specified by --scheme argument")
			("file,f", value<path>(&file)->value_name("FILE"), "use queries in the specified file")
			("scheme,s", value<string>(&scheme)->default_value(default_scheme)->value_name("KEYWORD"), "a case-insensitive keyword to match a GPCR numbering scheme; supported schemes are listed with --list schemes")
			;

		options_description output_options("Output options");
		output_options.add_options()
			(",1", bool_switch(&no_cols[0]), "suppress column 1 (Protein Symbol)")
			(",2", bool_switch(&no_cols[1]), "suppress column 2 (Gene Name)")
			(",3", bool_switch(&no_cols[2]), "suppress column 3 (Uniprot Id)")
			(",4", bool_switch(&no_cols[3]), "suppress column 4 (Residue Name)")
			(",5", bool_switch(&no_cols[4]), "suppress column 5 (Residue Sequence)")
			(",6", bool_switch(&no_cols[5]), "suppress column 6 (Residue Numbering)")
			("color", value<string>(&coloring)->value_name("WHEN")->default_value(default_coloring), ("colorize the output; WHEN can be " + formatter(colorings) + "; default to 'auto' if omitted").c_str())
			("hide-headers,H", bool_switch(&no_headers), "do not display headers on the first line")
			("show-unmatched,u", bool_switch(&show_unmatched), "output unmatched numberings as well (will be labeled with a question mark ?)")
			("ignore-errors,E", bool_switch(&ignore_errors), "ignore errors and move on to the next query")
			;

		options_description misc_options("Misc options");
		misc_options.add_options()
			("list,L", value<string>(&listing)->value_name("TYPE"), ("show a supported list; TYPE can be " + formatter(listings)).c_str())
			("help", "this help information")
			("version", "version information")
			;

		options_description all_options;
		all_options.add(input_options).add(output_options).add(misc_options);

		positional_options_description positional;
		positional.add("query", -1);

		variables_map vm;
		store(command_line_parser(argc, argv).options(all_options).positional(positional).run(), vm);
		notify(vm);

		if (vm.count("help")) // program input
		{
			cout << "Usage: " << argv[0] << " [--query] <query1> [<query2> <query3> ...] [options]" << endl;
			cout << "       " << argv[0] << " --file <query-file> [options]" << endl;
			cout << "GPCR numbering tool by ryan@imozo.cn" << endl;
			cout << "All data are downloaded from https://GPCRdb.org/structure/" << endl;
			cout << all_options << endl;
			cout << "Examples:" << endl;
			cout << "  " << argv[0] << " 5HT2A:123     \tGet BW numbering for protein 5HT2A at 123." << endl;
			cout << "  " << argv[0] << " HTR2A:123     \tGet BW numbering for gene HTR2A at 123." << endl;
			cout << "  " << argv[0] << " P28223:123    \tGet BW numbering for uniprot id P28223 at 123." << endl;
			cout << "  " << argv[0] << " 6A93:123      \tGet BW numbering for pdb id 6A93 at 123." << endl;
			cout << "  " << argv[0] << " :123          \tGet BW numberings for all GPCR proteins at 123." << endl;
			cout << "  " << argv[0] << " 6A93:2.53     \tGet residue name in form Y139 for pdb 6A93 at BW 2.53." << endl;
			cout << "  " << argv[0] << " HTR2A:        \tGet all BW numberings for gene HTR2A." << endl;
			cout << "  " << argv[0] << " :2.53         \tGet residue sequence numbers for all GPCR at BW 2.53." << endl;
			cout << "  " << argv[0] << " HTR2A:123 -sWB\tGet Wootten numberings for gene HTR2A at 123." << endl;
			return 0;
		}

		if (vm.count("list"))
		{
			if (!listings.count(listing))
			{
				cerr << "ERROR: unrecognized argument '" << listing << "'; use " << formatter(listings) << endl;
				return 2;
			}

			if (listing == "schemes")
			{
				supported_schemes(default_scheme);
			}
			else if (listing == "residues")
			{
				for (auto [abbr, name] : res_names)
					cout << abbr << '\t' << name << endl;
			}
			else
			{
				set<string> results;
				if (listing == "symbol_species")
				{
					for (auto [key, ignore] : symbol_species_dict)
						results.insert(key);
				}
				else if (listing == "genes")
				{
					for (auto [key, ignore] : gene_name_dict)
						results.insert(key);
				}
				else if (listing == "symbols")
				{
					for (auto [key, ignore] : symbol_dict)
						results.insert(key);
				}
				else if (listing == "pdbids")
				{
					for (auto [key, ignore] : pdb_id_dict)
						results.insert(key);
				}
				else if (listing == "uniprots")
				{
					for (auto [key, ignore] : uniprot_dict)
						results.insert(key);
				}

				for (auto& s : results)
					cout << s << endl;
			}

			return 0;
		}

		if (vm.count("version"))
		{
			cout << "gpcrn version: 1.0.7 (2021-05-20)" << endl;
			cout << "GPCRdb version: 2021-05-14" << endl;
			return 0;
		}

		// find a scheme name match
		int scheme_id = match_scheme(scheme);
		if (scheme_id == -1)
		{
			set<string> keywords;
			for (auto [ignore, also_ignore, abbrs] : schemes)
				keywords.insert(abbrs[1]);
			cerr << "ERROR: unrecognized argument '" << scheme << "'; use " << formatter(keywords) << "; see more with '--list schemes'" << endl;
			return 2;
		}

		header_fmts.back().second = (get<2>(schemes[scheme_id]))[1];

		// apply coloring config
		if (!colorings.count(coloring))
		{
			cerr << "ERROR: unrecognized argument '" << coloring << "'; use " << formatter(colorings) << endl;
			return 2;
		}

		if (coloring == "always" || (coloring == "auto" && !is_redirected(stdout)))
		{
			hlcolor = default_hlcolor;
			init_console_coloring();
		}

		// find the last column number
		int last_col = 0;
		for (last_col = 5; last_col >= 0; --last_col)
			if (!no_cols[last_col])
				break;

		// start running
		int line_no = 0;
		bool any = false;
		if (queries.size())
		{
			for (auto& query : queries)
			{
				int retcode = process(no_cols, last_col, no_headers, show_unmatched, scheme_id, line_no, query, hlcolor);
				if (!ignore_errors && retcode)
					return retcode;
			}
			any = true;
		}

		if (vm.count("file"))
		{
			ifstream in(file);
			for (string line; safe_getline(in, line);)
			{
				trim(line);
				if (line.empty() || line[0] == '#')
					continue;
				int retcode = process(no_cols, last_col, no_headers, show_unmatched, scheme_id, line_no, line, hlcolor);
				if (!ignore_errors && retcode)
					return retcode;
			}
			any = true;
		}

		if (!any)
		{
			for (string line; safe_getline(cin, line);)
			{
				trim(line);
				if (line.empty() || line[0] == '#')
					continue;
				int retcode = process(no_cols, last_col, no_headers, show_unmatched, scheme_id, line_no, line, hlcolor);
				if (!ignore_errors && retcode)
					return retcode;
			}
		}
		return 0;
	}
	catch (exception& ex)
	{
		cerr << "ERROR: " << ex.what() << endl;
		return 2;
	}
}