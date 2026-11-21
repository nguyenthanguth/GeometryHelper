# Hand-written stubs of System.Reflection (see System-stubs/__init__.pyi).
from System import Object

class Assembly(Object):
    @property
    def FullName(self) -> str: ...
    @property
    def Location(self) -> str: ...
