"""Type-checks with exactly the errors marked by an expect comment on their lines."""

from GeometryHelper.Geometry import NoSuchType  # expect: reportAttributeAccessIssue
from GeometryHelper.NoSuchNamespace import Anything  # expect: reportMissingImports
