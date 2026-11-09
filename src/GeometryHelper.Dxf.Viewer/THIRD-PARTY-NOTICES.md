# Third-party notices

GeometryHelper.Dxf.Viewer holds font data that comes from another project. Its notice follows.

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
