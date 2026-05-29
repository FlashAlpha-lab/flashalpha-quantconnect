"""Safe LEAN PythonData import.

``quantconnect-stubs``' ``QuantConnect/__init__.py`` mutates ``sys.path``
(removes site-packages, drops ``sys.modules["QuantConnect"]``, then attempts
``from clr import AddReference``). When pythonnet (``clr``) isn't installed —
i.e. anywhere outside the LEAN runtime — that import raises and
``sys.path`` is never restored, which then breaks every subsequent
third-party import in the process.

This module snapshots ``sys.path`` and the ``QuantConnect`` ``sys.modules``
entry around the LEAN import, so a failure is fully contained. Bar classes
import ``PythonDataBase`` from here instead of from ``QuantConnect.Python``
directly.

Use:

    from ._lean_import import PythonDataBase

    class GexBar(PythonDataBase):
        ...
"""

from __future__ import annotations

import sys as _sys


_saved_sys_path = _sys.path[:]
_saved_qc_module = _sys.modules.get("QuantConnect")

try:
    from QuantConnect.Python import PythonData as PythonDataBase  # noqa: F401
except Exception:
    class PythonDataBase:  # type: ignore[no-redef]
        """Stand-in base used outside the LEAN runtime — keeps the module
        importable for tests and IDE introspection. Bar classes that extend
        this will fail at LEAN-time if the real base couldn't load, which
        is the desired fail-loud behavior."""

    _sys.path = _saved_sys_path
    if _saved_qc_module is not None:
        _sys.modules["QuantConnect"] = _saved_qc_module
    elif "QuantConnect" in _sys.modules and _sys.modules["QuantConnect"] is None:
        _sys.modules.pop("QuantConnect", None)

del _sys, _saved_sys_path, _saved_qc_module
