"""C9: Ctrl+C during a blocking call (D809). A call into the .NET client cannot be interrupted by a signal until it
returns, so the ``KeyboardInterrupt`` of a Ctrl+C comes after the call, not in the middle of it.

SIGINT is raised by a timer 0.3 s after a pick of 2 s starts, as Ctrl+C in a console does. Nobody answers the pick, so
it ends on its own timeout at 2 s with an exception, and the interrupt lands then: the time it is seen at must be at
least 1.5 s, 1.2 s after the signal and half a second short of the timeout.
"""

import signal
import threading
import time
from typing import Any

from System import TimeSpan

from GeometryHelper.Cad.Remote import CadRemoteException

SIGNAL_AT = 0.3
PICK_FOR = 2.0
EARLIEST = PICK_FOR - 0.5


def test_a_ctrl_c_during_a_pick_is_seen_only_after_the_pick_returned(drawing: Any) -> None:
    timer = threading.Timer(SIGNAL_AT, signal.raise_signal, [signal.SIGINT])
    start = time.perf_counter()
    seen_at = None
    timer.start()
    try:
        try:
            drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromSeconds(PICK_FOR))
        except CadRemoteException:
            pass  # the pick ending on its own timeout: the signal is handled once Python runs again
        timer.join()
        time.sleep(0.1)  # a pause that a signal interrupts, in case none was handled yet
    except KeyboardInterrupt:
        seen_at = time.perf_counter() - start

    assert seen_at is not None, "the SIGINT never became a KeyboardInterrupt"
    assert seen_at >= EARLIEST


def test_a_pick_interrupted_that_way_leaves_the_connection_good_for_the_next_request(drawing: Any, app: Any) -> None:
    timer = threading.Timer(SIGNAL_AT, signal.raise_signal, [signal.SIGINT])
    timer.start()
    try:
        try:
            drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromSeconds(PICK_FOR))
        except CadRemoteException:
            pass
        timer.join()
        time.sleep(0.1)
    except KeyboardInterrupt:
        pass

    assert app.Documents.Count == 1
