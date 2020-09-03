#include <map>
#include <string>
#include <vector>
#include <array>
#include <tuple>
using namespace std;

// string_index => string
extern array<string, 616> string_table;

// scheme_index => scheme_numbering_offset, scheme_numbering_length, scheme_names
extern array<tuple<size_t, size_t, vector<string>>, 14> schemes;

// uniprot_id => [symbol, species, gene_name, long_species]
extern map<string, tuple<string, string, string, string>> uniprot_dict;

// symbol => uniprot_id
extern multimap<string, string> symbol_dict;

// symbol_species => uniprot_id
extern map<string, string> symbol_species_dict;

// gene_name => uniprot_id
extern multimap<string, string> gene_name_dict;

// pdb_id => uniprot_id
extern map<string, string> pdb_id_dict;

// uniprot_id => [low, length, string_table_index...]
extern map<string, tuple<int, int, string, vector<short>>> numbering_data;
