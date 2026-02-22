Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Public NotInheritable Class ToolCatalog

    Public Shared Function GetDefaultTools() As List(Of ToolSpec)
        Dim tools As New List(Of ToolSpec)()

        ' -----------------------
        ' General purpose
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "Python", .Command = "python", .Args = "--version", .Category = "General", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "Py launcher", .Command = "py", .Args = "--version", .Category = "General", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "C (gcc)", .Command = "gcc", .Args = "--version", .Category = "General", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "C++ (g++)", .Command = "g++", .Args = "--version", .Category = "General", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "Java", .Command = "java", .Args = "-version", .Category = "General", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = ".NET", .Command = "dotnet", .Args = "--version", .Category = "General", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Go", .Command = "go", .Args = "version", .Category = "General", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Swift", .Command = "swift", .Args = "--version", .Category = "General", .TimeoutMs = 15000})
        tools.Add(New ToolSpec With {.Name = "Kotlin (kotlinc)", .Command = "kotlinc", .Args = "-version", .Category = "General", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Ruby", .Command = "ruby", .Args = "--version", .Category = "General", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "Rust (rustc)", .Command = "rustc", .Args = "--version", .Category = "General", .TimeoutMs = 8000})

        ' -----------------------
        ' Web & JS
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "Node.js", .Command = "node", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "V8 (JS engine)", .Command = "node", .Args = "-p process.versions.v8", .Category = "Web & JS", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "npm", .Command = "npm", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "npx", .Command = "npx", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "TypeScript (tsc)", .Command = "tsc", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Vite (global)", .Command = "vite", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Deno", .Command = "deno", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Bun", .Command = "bun", .Args = "--version", .Category = "Web & JS", .TimeoutMs = 12000})

        ' -----------------------
        ' Data/Math/Science
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "R", .Command = "R", .Args = "--version", .Category = "Data/Science", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Julia", .Command = "julia", .Args = "--version", .Category = "Data/Science", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "SQLite", .Command = "sqlite3", .Args = "--version", .Category = "Data/Science", .TimeoutMs = 8000})

        ' -----------------------
        ' Functional & Academic
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "Haskell (ghc)", .Command = "ghc", .Args = "--version", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Scala", .Command = "scala", .Args = "--version", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Clojure (clj)", .Command = "clj", .Args = "-Sdescribe", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Elixir", .Command = "elixir", .Args = "--version", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Erlang (erl)", .Command = "erl", .Args = "-version", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "OCaml (ocamlc)", .Command = "ocamlc", .Args = "-version", .Category = "Functional", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Scheme (Guile)", .Command = "guile", .Args = "--version", .Category = "Functional", .TimeoutMs = 20000})

        ' -----------------------
        ' Scripting/System
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "Bash", .Command = "bash", .Args = "--version", .Category = "System", .TimeoutMs = 20000})
        tools.Add(New ToolSpec With {.Name = "PHP", .Command = "php", .Args = "--version", .Category = "System", .TimeoutMs = 8000})
        tools.Add(New ToolSpec With {.Name = "Lua", .Command = "lua", .Args = "-v", .Category = "System", .TimeoutMs = 8000})

        ' Tcl special: write to stdin (works even on Windows builds where --version / -c can be weird)
        tools.Add(New ToolSpec With {.Name = "Tcl (tclsh)", .Command = "tclsh", .Args = "", .Category = "System", .TimeoutMs = 8000, .StdinText = "puts [info patchlevel]" & vbCrLf})

        tools.Add(New ToolSpec With {.Name = "Nim", .Command = "nim", .Args = "--version", .Category = "System", .TimeoutMs = 12000})
        tools.Add(New ToolSpec With {.Name = "Crystal", .Command = "crystal", .Args = "--version", .Category = "System", .TimeoutMs = 12000})

        ' -----------------------
        ' Assembler
        ' -----------------------
        tools.Add(New ToolSpec With {.Name = "NASM", .Command = "nasm", .Args = "-v", .Category = "Assembler", .TimeoutMs = 8000})

        Return tools
    End Function

    Public Shared Function GetAllCategories(tools As IEnumerable(Of ToolSpec)) As List(Of String)
        Dim setCat As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each t In tools
            If Not String.IsNullOrWhiteSpace(t.Category) Then setCat.Add(t.Category.Trim())
        Next

        Dim list As New List(Of String)(setCat)
        list.Sort(StringComparer.OrdinalIgnoreCase)
        Return list
    End Function

End Class
