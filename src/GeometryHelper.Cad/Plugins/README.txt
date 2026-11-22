GeometryHelper CAD - Plugins folder
===================================

Put command plugins here, and they are loaded when the app starts, as AutoCAD loads what its
Startup Suite names. A plugin is a .NET .dll with commands marked [CadCommand]; see the plugin
guide for how to write one.

What loads at start, in this order:

  1. Every .dll directly in this folder, in the order of their names, ignoring case.
  2. Every subfolder of this folder, in the order of their names, as one plugin: a plugin made
     of several .dll files (its own and the libraries it needs) lives in a folder of its own.
     If the folder has a plugin.json, that gives the plugin its name, version and API; if not,
     the plugin is named after the folder and all the .dll files directly in it are loaded.

A .dll that is not a plugin, or that cannot be loaded, is one line of the plugin load log (the
Plugin manager shows it) and the next one loads. A plugin loaded already is not loaded twice.
Files other than .dll, and .dll files in the subfolder of a subfolder, are not loaded.

This folder lies under the program folder, so it is a trusted folder: its plugins load with no
question. A plugin anywhere else is loaded with PLUGINLOAD (NETLOAD) for one session, or put on
the "Load at every start" list of the Plugin manager.

A plugin is loaded from a copy, so the file here is never locked: change it, then PLUGINRELOAD.
Loading a plugin runs its code with your rights: put here only what you trust.

This folder holds no search path: fonts, hatch patterns, linetypes and AutoLISP files are not
looked for here.
