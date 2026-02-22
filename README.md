VB DevCheck GUI (Modern)
=======================

A modern-ish WinForms app (VB.NET) to check versions of common developer tools
(Python, Node, Java, .NET, compilers, etc.) — based on your PowerShell HasV script.

Run (PowerShell)
----------------
1) Open PowerShell in the repo folder
2) Run:

    dotnet restore
    dotnet build
    dotnet run --project .\src\DevCheckGui\DevCheckGui.vbproj

Features
--------
- One-click Scan (async + progress + cancel)
- Search + filters (category / status)
- Details panel (full stdout+stderr)
- Export: TXT / JSON / CSV
- Copy report to clipboard
- Light/Dark theme toggle

Notes
-----
- This app checks tools from PATH (cmd-style). If a tool exists only as a PowerShell function/alias,
  it may show as missing — which is typically what you want for “real” CLI availability.
