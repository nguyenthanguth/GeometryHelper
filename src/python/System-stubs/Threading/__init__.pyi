# Hand-written stubs of System.Threading (see System-stubs/__init__.pyi).
from System import ValueType

class CancellationToken(ValueType):
    @property
    def CanBeCanceled(self) -> bool: ...
    @property
    def IsCancellationRequested(self) -> bool: ...
