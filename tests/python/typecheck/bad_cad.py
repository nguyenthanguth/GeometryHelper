"""Type-checks with exactly the errors marked by an expect comment on their lines: what a script of GeometryHelper CAD gets
wrong at a prompt, a command or a decorator (P3 D709)."""

from geometryhelper import cad
from geometryhelper.cad import cad_command

# a message is text
cad.get_point(5)  # expect: reportArgumentType

# the default of get_integer is a whole number
cad.get_integer("How many", default="three")  # expect: reportArgumentType

# Enter gives None and a keyword gives text, so the answer is no plain int
count: int = cad.get_integer("How many")  # expect: reportAssignmentType

# command takes its answers by position only
cad.command(name="CIRCLE")  # expect: reportCallIssue

# get_keyword has no keywords to offer unless it is given them
cad.get_keyword("Mode")  # expect: reportCallIssue

# selection() is a Python list of handles, not a .NET list
total = cad.selection().Count  # expect: reportAttributeAccessIssue

# a base point is a GeoPoint2, not a tuple
cad.get_distance("Radius", (0, 0))  # expect: reportArgumentType


# the decorator is given the name of the command: it cannot stand alone
@cad_command  # expect: reportArgumentType
def broken() -> None: ...
