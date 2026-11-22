"""The body of the acceptance command C1 (D1677), written once and used twice: ``@command("C1")`` in the app (the C#
test ``PythonAcceptanceTests`` loads it from a Pythons folder) and through ``connect()`` from outside
(``remote/test_c12_acceptance.py``). It asks for one object, then for a selection, and moves the selection to the layer of
the object; the whole of it is one undo step.
"""

from geometryhelper.cad import Drawing


def move_the_selection_to_the_layer_of_the_picked_object(drawing: Drawing) -> int:
    """Asks for the object whose layer to use and for the objects to move; gives how many were moved."""
    picked = drawing.pick_object("Pick the object whose layer to use")
    if picked is None:
        return 0
    chosen = drawing.pick_objects("Select the objects to move")
    for entity in chosen:
        entity.layer = picked.layer
    drawing.commit()
    return len(chosen)
