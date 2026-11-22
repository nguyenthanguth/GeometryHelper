"""Lists the drawings a running GeometryHelper CAD has open, with what is in their model space.

Run outside the app, with "Allow other programs to connect" on in its Global settings: python open_documents.py
One line for each drawing, in the app's order: its name (then " *" for the one shown), the number of objects in model
space, and the count of each class of object.
"""

from collections import Counter

from geometryhelper.cad import App
from GeometryHelper.Cad.Remote import CadApplication


def main(app: CadApplication) -> None:
    cad = App(app)  # the Drawing API over the connection, as connect() gives it
    for drawing in cad.documents:
        objects = drawing.entities(layout="Model")
        classes = Counter(type(entity.net).__name__ for entity in objects)
        mark = " *" if drawing.is_active else ""
        per_class = ", ".join(f"{name}:{classes[name]}" for name in sorted(classes))
        print(f"{drawing.name}{mark}\t{len(objects)}\t{per_class}")


if __name__ == "__main__":
    with CadApplication() as app:
        main(app)
