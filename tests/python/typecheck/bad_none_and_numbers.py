"""Type-checks with exactly the errors marked by an expect comment on their lines."""

from GeometryHelper.Cad.Remote import CadObjectFilter
from GeometryHelper.Dxf import DxfDatabase, DxfEraseFlags, DxfWriter

# only the members that say null take None: the others, and a wrong type, stay errors
database = DxfDatabase.Parse(DxfWriter().ToString())
database.Cmaterial = None  # expect: reportAttributeAccessIssue
everything = CadObjectFilter()
everything.Layer = 5  # expect: reportAttributeAccessIssue

# an enum is made from a whole number, and from one number
text_flags = DxfEraseFlags("3")  # expect: reportCallIssue|reportArgumentType
fraction_flags = DxfEraseFlags(1.5)  # expect: reportCallIssue|reportArgumentType
no_flags = DxfEraseFlags()  # expect: reportCallIssue
