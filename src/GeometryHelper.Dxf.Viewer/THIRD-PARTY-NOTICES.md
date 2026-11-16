# Third-party notices

GeometryHelper.Dxf.Viewer holds font data and a word list that come from other projects. Their notices follow.

## Hershey fonts

The letters of the presenter's own single-stroke font, GeometryHelper Simplex (`ghsimplex`), are the Roman Simplex set of
the Hershey fonts, taken from `rowmans.jhf` of https://github.com/kamalmostafa/hershey-fonts and turned into the bytes of
an SHX unifont in `src/GeometryHelper.Dxf.Viewer/Fonts/HersheySimplex.cs`; the plus-minus and diameter signs were drawn
for GeometryHelper in the same form. When the package of this project is made, this notice goes with it.

The terms of use of that distribution require these acknowledgements to be distributed with the font data:

- The Hershey Fonts were originally created by Dr. A. V. Hershey while working at the U. S. National Bureau of Standards.
- The format of the Font data in this distribution was originally created by James Hurt, Cognition, Inc., 900 Technology
  Park Drive, Billerica, MA 01821 (mit-eddie!ci-dandelion!hurt)

The font data may be converted into any other format except the format distributed by the U.S. NTIS; GeometryHelper
holds it as SHX shape bytes.

## English word list (SCOWL / ESDB)

The built-in English dictionary of Check Spelling and of the MTEXT editor's spell check,
`src/GeometryHelper.Dxf.Viewer/Text/english-words.txt.gz`, is made from the American and British spelling word lists of
size 35, 50 and 60 of the English Speller Database (ESDB, previously SCOWL) by Kevin Atkinson, https://wordlist.aspell.net,
generated from https://app.aspell.net/create. The words are lower-cased, possessives and acronyms taken out, and sorted by
the size of the list each first appears in. Its notice, which has to go with every copy:

```
Copyright 2000-2026 by Kevin Atkinson

Permission to use, copy, modify, distribute, and sell any part of the English
Speller Database (ESDB, previously known as SCOWLv2), or word lists
created from it, is hereby granted without fee, provided that the above
copyright notice appears in all copies and that both the above copyright
notice and this notice appear in supporting documentation.  Kevin Atkinson
makes no representations about the suitability of this database for any
purpose.  It is provided "as is" without express or implied warranty.

ESDB is derived from many sources, most of which are in the Public Domain.
Data from the Corpus of Contemporary American English (COCA) was also used.

All data from COCA comes from 3-gram data that is not freely available;
however, the usage is within the rights given by the NDA that was signed when
purchasing the data.  More information on COCA is available at
https://www.english-corpora.org/coca/.

The primary source of words for ESDB comes from 12dicts and ENABLE2K.  Both
are in the Public Domain, but Alan Beale <biljir@pobox.com> deserves special
credit as he is the author of 12dicts and a major contributor to ENABLE2K.  In
addition, he gave me an incredible amount of feedback and created a number of
special lists in order to help improve the overall quality of ESDB.
```
