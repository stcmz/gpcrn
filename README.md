gpcrn
=====

gpcrn is a cross-platform offline database for GPCR numbering.


Features
--------

gpcrn accepts flexible queries:
* allow single or multiple queries at a time
* allow query by targets and numberings (residues)
* empty target to match all proteins
* empty numbering to match all residues
* supports 4 kinds of target: Protein Symbol, Gene Name, UniProt ID and PDB Entry

gpcrn has knowledge of the following numbering schemes:
* Ballesteros-Weinstein (Class A)
* Wootten (Class B)
* Pin (Class C)
* Wang (Class F)
* Fungal (Class D)
* GPCRdb (Class A)
* GPCRdb (Class B)
* GPCRdb (Class C)
* GPCRdb (Class F)
* GPCRdb (Class D)
* Oliveira
* Baldwin-Schwartz

gpcrn accepts the following ways of input:
* standard input: `gpcrn`
* file input: `gpcrn -f queryfile`
* input redirect: `gpcrn -f <(cat queryfile)`
* pipe input: `cat queryfile | gpcrn`
* as script file with header: `#!/usr/bin/gpcrn -f` (Linux/Unix only)

Misc options:
* match colorization: `--color auto`
* header hiding: `-H`
* column hiding: `-123456`
* assets listing: `-L <asset_type>`
* unmatch numbering showing: `-u`
* ignore syntax errors: `-E`


Supported operating systems and compilers
-----------------------------------------

All systems with compilers in conformance with the C++17 standard, e.g.
* Linux x86_64 and g++ 8.3.1 or higher
* Mac OS X x86_64 and clang 7 or higher
* Windows x86_64 and msvc 19.14 or higher


Compilation from source code
----------------------------

### Compilation of Boost

gpcrn depends on the `Program Options` component in [Boost C++ Libraries]. Boost 1.74.0 was tested. To compile gpcrn, first download [Boost 1.74] and unpack the archive to `boost_1_74_0/include`.

Build on Linux run:
```
cd boost_1_74_0/include
./bootstrap.sh
./b2 --build-dir=../build/linux_x64 --stagedir=../ -j 8 link=static address-model=64
```

Or, on Windows run:
```
cd boost_1_74_0\include
bootstrap.bat
b2 --build-dir=../build/win_x64 --stagedir=../ -j 8 link=static address-model=64
```

Then add the path of the `boost_1_74_0` directory the to the BOOST_ROOT environment variable.

### Compilation on Linux

The Makefile uses g++ as the default compiler. To compile, simply run
```
make
```

One may modify the Makefile to use a different compiler or different compilation options.

The generated objects will be placed in the `obj` folder, and the generated executable will be placed in the `bin` folder.

Optionally, one may copy the output binary to `/usr/bin` by running
```
make setup
```

### Compilation on Windows

Visual Studio 2019 solution and project files are provided. To compile, simply run
```
msbuild /t:Build /p:Configuration=Release
```

Or one may open `gpcrn.sln` in Visual Studio 2019 and do a full rebuild.

The generated objects will be placed in the `obj` folder, and the generated executable will be placed in the `bin` folder.


Usage
-----

First add gpcrn to the PATH environment variable.

To display a full list of available options, simply run the program with the `--help` argument
```
gpcrn --help
```

See the [Features section](#features) above for usages in different input ways.


Author
--------------

[Maozi Chen]


[Boost C++ Libraries]: https://www.boost.org
[Maozi Chen]: https://www.linkedin.com/in/maozichen/
[Boost 1.74]: https://www.boost.org/users/history/version_1_74_0.html
