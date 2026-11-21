"""Lists the drawings a running GeometryHelper CAD has open, with what is in their model space.

Run outside the app, with "Allow other programs to connect" on in its Global settings: python open_documents.py
One line for each drawing, in the app's order: its name (then " *" for the one shown), the number of objects in model
space, and the count of each class of object.
"""

from collections import Counter

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper.Cad.Remote import CadApplication, CadObjectFilter


def main(app: CadApplication) -> None:
    model_space = CadObjectFilter()
    model_space.Layout = "Model"
    for drawing in app.Documents:
        objects = drawing.GetObjects(model_space)
        classes = Counter(type(entity).__name__ for entity in objects)
        mark = " *" if drawing.IsActive else ""
        per_class = ", ".join(f"{name}:{classes[name]}" for name in sorted(classes))
        print(f"{drawing.Name}{mark}\t{objects.Count}\t{per_class}")


if __name__ == "__main__":
    with CadApplication() as app:
        main(app)
