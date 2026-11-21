"""Type-checks clean: a .NET event takes a Python function with +=."""

from typing import Any

from GeometryHelper.Cad import CadDocument, CadDocumentChangedEventArgs


def on_changed(sender: Any, args: CadDocumentChangedEventArgs) -> None:
    print(sender, args)


def subscribe(document: CadDocument) -> None:
    document.Changed += on_changed
    document.Changed -= on_changed
