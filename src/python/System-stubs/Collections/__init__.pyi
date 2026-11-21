# Hand-written stubs of System.Collections (see System-stubs/__init__.pyi).
from collections.abc import Iterator
from typing import Any

from System import Object

class IEnumerator(Object):
    @property
    def Current(self) -> Any: ...
    def MoveNext(self) -> bool: ...

class IEnumerable(Object):
    def __iter__(self) -> Iterator[Any]: ...
