"""Asks you to pick a point in a running GeometryHelper CAD, and draws a circle of radius 50 there.

Run outside the app, with "Allow other programs to connect" on in its Global settings: python pick_and_mark.py
It waits up to a minute for you to click on the canvas or type a point, then zooms to the extents. Esc in the app
cancels the pick: the script says so and returns 1.
"""

import sys

from System import TimeSpan

from geometryhelper.cad import App
from GeometryHelper.Cad.Remote import CadApplication, CadRemoteException

PICK_TIMEOUT = TimeSpan.FromSeconds(60)  # a pick with no time limit would wait for ever, and Ctrl+C cannot end it


def main(app: CadApplication) -> int:
    cad = App(app)  # the Drawing API over the connection, as connect() gives it
    drawing = cad.active_drawing
    if drawing is None:  # the app has no drawing open
        drawing = cad.new()
    try:
        # drawing.net is the CadRemoteDocument: its PickPoint takes the time limit that drawing.pick_point does not.
        centre = drawing.net.PickPoint("Pick the centre", timeout=PICK_TIMEOUT)
    except CadRemoteException as error:
        if not error.IsCancelled:
            raise
        print("Cancelled.")
        return 1
    drawing.add_circle(centre, 50)
    drawing.commit()
    cad.run_command(drawing, "ZOOM E\n")  # "\n" is Enter
    print(f"circle at {centre.X:.3f},{centre.Y:.3f}")
    return 0


if __name__ == "__main__":
    with CadApplication() as app:
        sys.exit(main(app))
